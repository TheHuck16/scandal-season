// Scandal Season — Runtime game layer.
// MergeBoardView: playable 6x6 merge board. Rendering only — all rules live in
// ScandalSeason.Domain.Merge.MergeBoard. Chain display names and level counts
// come from ItemChainDefinitionSO (Board F chains). No invented content.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ScandalSeason.Domain.Merge;

[RequireComponent(typeof(RectTransform))]
public sealed class MergeBoardView : MonoBehaviour
{
    [Header("Prefabs / layout")]
    public GameObject cellPrefab; // Must have Button + Image + Text children.
    public Transform gridParent;
    public GridLayoutGroup gridLayout;

    [Header("Controls")]
    public Button spawnButton;
    public Button mergeButton;
    public Text statusText;
    public Text selectionText;

    private GameManager _game;
    private readonly List<BoardPosition> _selection = new List<BoardPosition>();
    private readonly Dictionary<BoardPosition, GameObject> _cellViews =
        new Dictionary<BoardPosition, GameObject>();
    private readonly Dictionary<BoardPosition, Text> _cellLabels =
        new Dictionary<BoardPosition, Text>();

    private void Start()
    {
        _game = GameManager.Instance;
        BuildGrid();
        if (spawnButton != null) spawnButton.onClick.AddListener(OnSpawnPressed);
        if (mergeButton != null) mergeButton.onClick.AddListener(OnMergePressed);
        RefreshAll();
    }

    private void BuildGrid()
    {
        if (gridParent == null || cellPrefab == null || _game == null) return;

        foreach (Transform child in gridParent)
            Destroy(child.gameObject);
        _cellViews.Clear();
        _cellLabels.Clear();

        int w = _game.Board.Width;
        int h = _game.Board.Height;
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = w;
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var pos = new BoardPosition(x, y);
                var go = Instantiate(cellPrefab, gridParent);
                go.name = $"Cell_{x}_{y}";

                var button = go.GetComponent<Button>();
                var label = go.GetComponentInChildren<Text>();
                if (button != null)
                {
                    var captured = pos;
                    button.onClick.AddListener(() => OnCellTapped(captured));
                }
                _cellViews[pos] = go;
                if (label != null) _cellLabels[pos] = label;
            }
        }
    }

    private void OnCellTapped(BoardPosition pos)
    {
        var item = _game.Board.GetItem(pos.X, pos.Y);
        if (item == null)
        {
            SetStatus("Empty cell. Spawn items to fill the board.");
            return;
        }

        if (_selection.Contains(pos))
        {
            _selection.Remove(pos);
        }
        else
        {
            // Selection must stay homogeneous: same chain and level.
            if (_selection.Count > 0)
            {
                var first = _game.Board.GetItem(_selection[0].X, _selection[0].Y);
                if (first == null || first.ChainId != item.ChainId || first.Level != item.Level)
                {
                    _selection.Clear();
                    SetStatus("Selection cleared — merges need matching chain and level.");
                }
            }
            if (_selection.Count < 5)
                _selection.Add(pos);
            else
                SetStatus("Selection full (5 max). Merge or clear it.");
        }
        RefreshAll();
    }

    private void OnSpawnPressed()
    {
        var chains = _game.UnlockedChainIds;
        if (chains == null || chains.Count == 0)
        {
            SetStatus("No chains unlocked.");
            return;
        }

        // SCAFFOLD: free level-1 spawn from a random unlocked chain.
        // Spawn energy cost is UNDECIDED — this is a placeholder, not a rule.
        string chainId = chains[UnityEngine.Random.Range(0, chains.Count)];
        if (_game.Board.TrySpawn(chainId, 1, out var pos))
        {
            SetStatus($"Spawned {ChainDisplayName(chainId)} L1 at {pos}.");
        }
        else
        {
            SetStatus("Board is full — merge to make space.");
        }
        RefreshAll();
    }

    private void OnMergePressed()
    {
        if (_selection.Count != 3 && _selection.Count != 5)
        {
            SetStatus($"Select 3 or 5 matching items to merge (have {_selection.Count}).");
            return;
        }

        var result = _game.Board.TryMerge(_selection);
        if (result.Success)
        {
            var item = _game.Board.GetItem(result.ResultPosition.X, result.ResultPosition.Y);
            string name = item != null ? ChainDisplayName(item.ChainId) : "?";
            SetStatus(result.MergedCount == 5
                ? $"5-merge bonus! Two {name} L{result.ResultLevel}."
                : $"Merged into {name} L{result.ResultLevel}.");
            CheckOrderFulfillment(result.ResultPosition);
        }
        else
        {
            SetStatus($"Merge failed: {result.Error}");
        }
        _selection.Clear();
        RefreshAll();
    }

    /// <summary>
    /// SCAFFOLD: auto-checks order fulfillment after a merge. Fulfillment UX
    /// (auto vs. explicit) is UNDECIDED. Payouts come from the domain's locked
    /// formulas; only the trigger is placeholder.
    /// </summary>
    private void CheckOrderFulfillment(BoardPosition pos)
    {
        var item = _game.Board.GetItem(pos.X, pos.Y);
        if (item == null || _game.Orders == null) return;

        for (int i = 0; i < _game.Orders.MaxStandingOrders; i++)
        {
            var order = _game.Orders.GetOrder(i);
            if (order == null) continue;
            if (order.ChainId == item.ChainId && order.Level == item.Level)
            {
                if (_game.Orders.TryFulfillOrder(i, item.ChainId, item.Level,
                    DateTime.UtcNow, out int payout))
                {
                    _game.Wallet.Grant(
                        ScandalSeason.Domain.Economy.Currency.Coins, payout);
                    // Consume the delivered item via snapshot round-trip
                    // (the domain has no direct remove; merges reject duplicates).
                    var snap = _game.Board.GetSnapshot();
                    snap.Cells.RemoveAll(c => c.X == pos.X && c.Y == pos.Y);
                    _game.Board.LoadSnapshot(snap);
                    SetStatus($"Order fulfilled! +{payout} coins.");
                    return;
                }
            }
        }
    }

    private string ChainDisplayName(string chainId)
    {
        var chain = _game.GetChain(chainId);
        return chain != null && !string.IsNullOrEmpty(chain.displayName)
            ? chain.displayName
            : chainId;
    }

    private string CellLabel(MergeItem item)
    {
        if (item == null) return "";
        var chain = _game.GetChain(item.ChainId);
        string shortName = chain != null && !string.IsNullOrEmpty(chain.displayName)
            ? chain.displayName
            : item.ChainId;
        // Abbreviate to fit the cell; level as roman-ish numeral.
        if (shortName.Length > 8) shortName = shortName.Substring(0, 8);
        return $"{shortName}\nLv{item.Level}";
    }

    public void RefreshAll()
    {
        if (_game == null || _game.Board == null) return;

        foreach (var kvp in _cellViews)
        {
            var pos = kvp.Key;
            var go = kvp.Value;
            var item = _game.Board.GetItem(pos.X, pos.Y);

            if (_cellLabels.TryGetValue(pos, out var label) && label != null)
                label.text = CellLabel(item);

            var image = go.GetComponent<Image>();
            if (image != null)
            {
                bool selected = _selection.Contains(pos);
                if (item == null)
                    image.color = new Color(0.16f, 0.16f, 0.18f, 1f); // empty
                else if (selected)
                    image.color = new Color(0.95f, 0.80f, 0.35f, 1f); // selected gold
                else
                    image.color = new Color(0.28f, 0.42f, 0.30f, 1f); // occupied estate green
            }
        }

        if (selectionText != null)
            selectionText.text = _selection.Count == 0
                ? "Tap items to select (3 or 5 matching)."
                : $"Selected: {_selection.Count}";

        if (mergeButton != null)
            mergeButton.interactable = _selection.Count == 3 || _selection.Count == 5;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    /// <summary>Clears the current selection (e.g. when leaving the board).</summary>
    public void ClearSelection()
    {
        _selection.Clear();
        RefreshAll();
    }
}
