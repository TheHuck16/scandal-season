// Scandal Season — Domain core. PLAIN C# ONLY: no UnityEngine references.
// Merge-3 board rules: spawn, merge chains (3 or 5), serializable board state.

using System;
using System.Collections.Generic;

namespace ScandalSeason.Domain.Merge
{
    /// <summary>Immutable grid coordinate.</summary>
    public readonly struct BoardPosition : IEquatable<BoardPosition>
    {
        public int X { get; }
        public int Y { get; }

        public BoardPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BoardPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is BoardPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>One item sitting on the board: a merge-chain id plus its level (1-based).</summary>
    public sealed class MergeItem
    {
        public string ChainId { get; }
        public int Level { get; }

        public MergeItem(string chainId, int level)
        {
            if (string.IsNullOrWhiteSpace(chainId))
                throw new ArgumentException("Chain id is required.", nameof(chainId));
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level), "Level is 1-based.");
            ChainId = chainId;
            Level = level;
        }
    }

    /// <summary>Outcome of a merge attempt. Never throws for rule violations — check <see cref="Success"/>.</summary>
    public sealed class MergeResult
    {
        public bool Success { get; }
        public string? Error { get; }
        public BoardPosition ResultPosition { get; }
        public int ResultLevel { get; }
        public int MergedCount { get; }

        private MergeResult(bool success, string? error, BoardPosition resultPosition, int resultLevel, int mergedCount)
        {
            Success = success;
            Error = error;
            ResultPosition = resultPosition;
            ResultLevel = resultLevel;
            MergedCount = mergedCount;
        }

        public static MergeResult Ok(BoardPosition resultPosition, int resultLevel, int mergedCount) =>
            new MergeResult(true, null, resultPosition, resultLevel, mergedCount);

        public static MergeResult Fail(string error) =>
            new MergeResult(false, error, default, 0, 0);
    }

    /// <summary>Plain-data board snapshot for save/load. No Unity types.</summary>
    public sealed class BoardCellState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string ChainId { get; set; } = "";
        public int Level { get; set; }
    }

    public sealed class BoardSnapshot
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<BoardCellState> Cells { get; set; } = new List<BoardCellState>();
    }

    /// <summary>
    /// Merge-3 board. Rules:
    /// <list type="bullet">
    ///   <item>Merging requires exactly 3 or 5 items of the same chain AND level.</item>
    ///   <item>3 merge into 1 item of level+1; 5 merge into 2 items of level+1 (bonus).</item>
    ///   <item>Results are placed on the first (and second) selected positions.</item>
    /// </list>
    /// Deterministic when constructed with a seed (tests, replays).
    /// </summary>
    public sealed class MergeBoard
    {
        /// <summary>
        /// Locked engine rule (Sep 27 2026): every chain has exactly 10 levels.
        /// Merging items already at the max level is rejected.
        /// </summary>
        public const int MaxChainLevel = 10;

        public int Width { get; }
        public int Height { get; }

        private readonly MergeItem?[,] _cells;
        private readonly Random _random;

        public MergeBoard(int width, int height, int? seed = null)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _cells = new MergeItem?[width, height];
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public MergeItem? GetItem(int x, int y)
        {
            if (!IsInBounds(x, y)) throw new ArgumentOutOfRangeException($"Position ({x},{y}) is off the board.");
            return _cells[x, y];
        }

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (_cells[x, y] != null) count++;
                return count;
            }
        }

        public bool IsFull => OccupiedCount >= Width * Height;

        /// <summary>Spawns an item onto a random empty cell. Returns false when the board is full.</summary>
        public bool TrySpawn(string chainId, int level, out BoardPosition position)
        {
            position = default;
            if (IsFull) return false;

            var empty = new List<BoardPosition>(Width * Height - OccupiedCount);
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (_cells[x, y] == null) empty.Add(new BoardPosition(x, y));

            var pick = empty[_random.Next(empty.Count)];
            _cells[pick.X, pick.Y] = new MergeItem(chainId, level);
            position = pick;
            return true;
        }

        /// <summary>Attempts to merge the items at the given positions.</summary>
        public MergeResult TryMerge(IReadOnlyList<BoardPosition> positions)
        {
            if (positions == null)
                return MergeResult.Fail("No positions supplied.");
            if (positions.Count != 3 && positions.Count != 5)
                return MergeResult.Fail($"Merge requires exactly 3 or 5 items, got {positions.Count}.");

            var seen = new HashSet<BoardPosition>();
            foreach (var p in positions)
            {
                if (!IsInBounds(p.X, p.Y))
                    return MergeResult.Fail($"Position {p} is off the board.");
                if (!seen.Add(p))
                    return MergeResult.Fail($"Position {p} was selected twice.");
                if (_cells[p.X, p.Y] == null)
                    return MergeResult.Fail($"Position {p} is empty.");
            }

            var first = _cells[positions[0].X, positions[0].Y]!;
            foreach (var p in positions)
            {
                var item = _cells[p.X, p.Y]!;
                if (item.ChainId != first.ChainId || item.Level != first.Level)
                    return MergeResult.Fail("All merged items must share the same chain and level.");
            }

            if (first.Level >= MaxChainLevel)
                return MergeResult.Fail($"Items are already at the max level ({MaxChainLevel}); merging is rejected.");

            int resultCount = positions.Count == 5 ? 2 : 1;
            int resultLevel = first.Level + 1;

            foreach (var p in positions)
                _cells[p.X, p.Y] = null;

            for (int i = 0; i < resultCount; i++)
            {
                var p = positions[i];
                _cells[p.X, p.Y] = new MergeItem(first.ChainId, resultLevel);
            }

            return MergeResult.Ok(positions[0], resultLevel, positions.Count);
        }

        public BoardSnapshot GetSnapshot()
        {
            var snapshot = new BoardSnapshot { Width = Width, Height = Height };
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    var item = _cells[x, y];
                    if (item != null)
                        snapshot.Cells.Add(new BoardCellState { X = x, Y = y, ChainId = item.ChainId, Level = item.Level });
                }
            return snapshot;
        }

        public void LoadSnapshot(BoardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Width != Width || snapshot.Height != Height)
                throw new ArgumentException("Snapshot dimensions do not match this board.");
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _cells[x, y] = null;
            foreach (var cell in snapshot.Cells)
            {
                if (!IsInBounds(cell.X, cell.Y))
                    throw new ArgumentException($"Snapshot cell ({cell.X},{cell.Y}) is off the board.");
                _cells[cell.X, cell.Y] = new MergeItem(cell.ChainId, cell.Level);
            }
        }
    }
}
