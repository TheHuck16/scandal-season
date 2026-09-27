using System.Collections.Generic;
using ScandalSeason.Domain.Merge;
using Xunit;

namespace ScandalSeason.Domain.Tests.Merge;

public sealed class MergeBoardTests
{
    private static MergeBoard BoardWith(params (int x, int y, string chain, int level)[] cells)
    {
        var board = new MergeBoard(4, 4);
        var snapshot = new BoardSnapshot { Width = 4, Height = 4 };
        foreach (var (x, y, chain, level) in cells)
            snapshot.Cells.Add(new BoardCellState { X = x, Y = y, ChainId = chain, Level = level });
        board.LoadSnapshot(snapshot);
        return board;
    }

    private static List<BoardPosition> Pos(params (int x, int y)[] ps)
    {
        var list = new List<BoardPosition>();
        foreach (var (x, y) in ps) list.Add(new BoardPosition(x, y));
        return list;
    }

    [Fact]
    public void Spawn_AddsItemToEmptyCell()
    {
        var board = new MergeBoard(4, 4, seed: 42);
        Assert.True(board.TrySpawn("placeholder-roses", 1, out var pos));
        var item = board.GetItem(pos.X, pos.Y);
        Assert.NotNull(item);
        Assert.Equal("placeholder-roses", item!.ChainId);
        Assert.Equal(1, item.Level);
        Assert.Equal(1, board.OccupiedCount);
    }

    [Fact]
    public void Spawn_ReturnsFalseWhenFull()
    {
        var board = new MergeBoard(1, 1, seed: 1);
        Assert.True(board.TrySpawn("a", 1, out _));
        Assert.True(board.IsFull);
        Assert.False(board.TrySpawn("a", 1, out _));
    }

    [Fact]
    public void Merge3_ProducesSingleItemOfNextLevel()
    {
        var board = BoardWith((0, 0, "roses", 1), (1, 0, "roses", 1), (2, 0, "roses", 1));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (2, 0)));

        Assert.True(result.Success);
        Assert.Equal(2, result.ResultLevel); // merge of three level-1 items -> one level-2 item
        Assert.Equal(new BoardPosition(0, 0), result.ResultPosition);
        var item = board.GetItem(0, 0);
        Assert.NotNull(item);
        Assert.Equal(2, item!.Level);
        Assert.Null(board.GetItem(1, 0));
        Assert.Null(board.GetItem(2, 0));
        Assert.Equal(1, board.OccupiedCount);
    }

    [Fact]
    public void Merge5_ProducesTwoItemsOfNextLevel()
    {
        var board = BoardWith((0, 0, "roses", 2), (1, 0, "roses", 2), (2, 0, "roses", 2), (3, 0, "roses", 2), (0, 1, "roses", 2));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (2, 0), (3, 0), (0, 1)));

        Assert.True(result.Success);
        Assert.Equal(3, result.ResultLevel);
        Assert.Equal(2, board.OccupiedCount);
        Assert.Equal(3, board.GetItem(0, 0)!.Level);
        Assert.Equal(3, board.GetItem(1, 0)!.Level);
    }

    [Fact]
    public void Merge_RejectsMixedChains()
    {
        var board = BoardWith((0, 0, "roses", 1), (1, 0, "tulips", 1), (2, 0, "roses", 1));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (2, 0)));
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(3, board.OccupiedCount); // board untouched
    }

    [Fact]
    public void Merge_RejectsMixedLevels()
    {
        var board = BoardWith((0, 0, "roses", 1), (1, 0, "roses", 2), (2, 0, "roses", 1));
        Assert.False(board.TryMerge(Pos((0, 0), (1, 0), (2, 0))).Success);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Merge_RejectsCountsOtherThan3Or5(int count)
    {
        var board = new MergeBoard(4, 4, seed: 7);
        var positions = new List<BoardPosition>();
        for (int i = 0; i < count; i++)
        {
            board.TrySpawn("roses", 1, out var p);
            positions.Add(p);
        }
        // Force same chain/level by reloading a matching snapshot when possible.
        var result = board.TryMerge(positions);
        Assert.False(result.Success);
        Assert.Contains("3 or 5", result.Error);
    }

    [Fact]
    public void Merge_RejectsEmptyCell()
    {
        var board = BoardWith((0, 0, "roses", 1), (1, 0, "roses", 1));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (2, 0)));
        Assert.False(result.Success);
    }

    [Fact]
    public void Merge_RejectsDuplicatePosition()
    {
        var board = BoardWith((0, 0, "roses", 1), (1, 0, "roses", 1), (2, 0, "roses", 1));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (0, 0)));
        Assert.False(result.Success);
    }

    [Fact]
    public void Merge_RejectsOutOfBounds()
    {
        var board = BoardWith((0, 0, "roses", 1), (1, 0, "roses", 1), (2, 0, "roses", 1));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (9, 9)));
        Assert.False(result.Success);
    }

    [Fact]
    public void Snapshot_RoundTripPreservesState()
    {
        var board = BoardWith((0, 0, "roses", 1), (3, 3, "tulips", 4));
        var snapshot = board.GetSnapshot();

        var restored = new MergeBoard(4, 4);
        restored.LoadSnapshot(snapshot);

        Assert.Equal(2, restored.OccupiedCount);
        Assert.Equal("roses", restored.GetItem(0, 0)!.ChainId);
        Assert.Equal(4, restored.GetItem(3, 3)!.Level);
    }

    [Fact]
    public void LoadSnapshot_RejectsMismatchedDimensions()
    {
        var board = new MergeBoard(4, 4);
        Assert.Throws<System.ArgumentException>(() =>
            board.LoadSnapshot(new BoardSnapshot { Width = 5, Height = 5 }));
    }

    [Fact]
    public void Merge_RejectsItemsAtMaxChainLevel()
    {
        // Locked engine rule (Sep 27 2026): every chain has exactly 10 levels;
        // merging level-10 items is rejected.
        Assert.Equal(10, MergeBoard.MaxChainLevel);
        var board = BoardWith(
            (0, 0, "atelier.notions", 10),
            (1, 0, "atelier.notions", 10),
            (2, 0, "atelier.notions", 10));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (2, 0)));
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        // Items stay on the board untouched.
        Assert.Equal(3, board.OccupiedCount);
        Assert.Equal(10, board.GetItem(0, 0)!.Level);
    }

    [Fact]
    public void Merge_AllowsMergingLevelNineIntoLevelTen()
    {
        var board = BoardWith(
            (0, 0, "atelier.notions", 9),
            (1, 0, "atelier.notions", 9),
            (2, 0, "atelier.notions", 9));
        var result = board.TryMerge(Pos((0, 0), (1, 0), (2, 0)));
        Assert.True(result.Success);
        Assert.Equal(10, result.ResultLevel);
    }
}
