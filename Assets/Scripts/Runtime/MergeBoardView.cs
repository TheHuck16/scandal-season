// UNITY-DEPENDENT — thin Unity glue over ScandalSeason.Domain.Merge.
// No game logic here: construction + forwarding only. All rules live in Domain.

using System.Collections.Generic;
using UnityEngine;
using ScandalSeason.Domain.Merge;

public sealed class MergeBoardView : MonoBehaviour
{
    [SerializeField] private int width = 7;
    [SerializeField] private int height = 9;
    [Tooltip("Non-zero seed = deterministic board (tests, replays). Zero = random.")]
    [SerializeField] private int seed;

    private MergeBoard _board = null!;

    /// <summary>Domain board. UI scripts read state from here; they never mutate cells directly.</summary>
    public MergeBoard Board => _board;

    private void Awake()
    {
        _board = seed != 0 ? new MergeBoard(width, height, seed) : new MergeBoard(width, height);
    }

    public bool TrySpawn(string chainId, int level, out BoardPosition position) =>
        _board.TrySpawn(chainId, level, out position);

    public MergeResult TryMerge(IReadOnlyList<BoardPosition> positions) =>
        _board.TryMerge(positions);
}
