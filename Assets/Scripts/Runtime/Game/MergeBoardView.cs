// Scandal Season — Runtime game layer.
// MergeBoardView: playable 8x8 merge board. Rendering only — all rules live in
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

    [Header("Board F visual identity")]
    public ChainStyleSO chainStyle;

    [Header("Chain legend (Board F)")]
    [Tooltip("Parent transform for auto-built legend entries. Each gets Image + Text.")]
    public Transform legendParent;
    public GameObject legendEntryPrefab;

    [Header("Controls")]
    public Button spawnButton;
    public Button mergeButton;
    public Text statusText;
    public Text selectionText;

    [Header("Spawn tier (LOCKED Sep 27: 1/2/4/8 energy -> L1/L2/L3/L4)")]
    [Tooltip("Player-selected spawn tier. 1=1 energy (L1), 2=2 energy (L2), 3=4 energy (L3), 4=8 energy (L4). UI TBD.")]
    [Range(1, 4)]
    public int spawnTierSelection = 1;

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
        BuildLegend();
        if (spawnButton != null) spawnButton.onClick.AddListener(OnSpawnPressed);
        if (mergeButton != null) mergeButton.onClick.AddListener(OnMergePressed);
        CreateBackButton();
        RefreshAll();
    }

    /// <summary>
    /// Creates a back button programmatically (Sep 29: scene lacks one, players were trapped).
    /// </summary>
    private void CreateBackButton()
    {
        var backBtnGO = new GameObject("BackButton", typeof(RectTransform));
        backBtnGO.transform.SetParent(transform, false);
        var rect = backBtnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(80, -40);
        rect.sizeDelta = new Vector2(140, 50);

        var image = backBtnGO.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        var button = backBtnGO.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            if (_game != null)
                _game.SetState(GameState.StoryScene);
        });

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(backBtnGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var text = textGO.AddComponent<Text>();
        text.text = "← Story";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = 24;
    }

    /// <summary>
    /// Board F: builds the chain legend from ChainStyleSO. Each entry shows the
    /// chain family color swatch and display name. Per the chain bible, the legend
    /// lives on the board.
    /// </summary>
    private void BuildLegend()
    {
        if (legendParent == null || chainStyle == null || chainStyle.chains == null) return;

        foreach (Transform child in legendParent)
            Destroy(child.gameObject);

        foreach (var chain in chainStyle.chains)
        {
            if (chain == null) continue;
            GameObject entry;
            if (legendEntryPrefab != null)
            {
                entry = Instantiate(legendEntryPrefab, legendParent);
            }
            else
            {
                // Fallback: build a minimal entry programmatically.
                entry = new GameObject($"Legend_{chain.chainId}", typeof(RectTransform));
                entry.transform.SetParent(legendParent, false);
                var bg = entry.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0.1f);
                var text = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
                text.transform.SetParent(entry.transform, false);
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.alignment = TextAnchor.MiddleLeft;
            }
            entry.name = $"Legend_{chain.chainId}";

            var swatch = entry.GetComponentInChildren<Image>();
            // First Image is the swatch; recolor it to the family color.
            // (If the prefab has a dedicated swatch child named "Swatch", prefer it.)
            var swatchT = entry.transform.Find("Swatch");
            if (swatchT != null) swatch = swatchT.GetComponent<Image>();
            if (swatch != null) swatch.color = chain.familyColor;

            var label = entry.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = chain.displayName;
                label.color = new Color(0.25f, 0.2f, 0.15f); // warm dark for ivory bg
            }
        }
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

        // LOCKED Sep 27: atelier multiplier IS the energy spend — 1 energy spawns
        // L1, 2 energy spawns L2, 4 energy spawns L3, 8 energy spawns L4.
        // Not a yield boost. (Acquisition: mastery milestones + Crown purchase.)
        int spawnTier = Mathf.Clamp(spawnTierSelection, 1, 4);
        int spawnEnergyCost = 1 << (spawnTier - 1); // 1, 2, 4, 8
        if (_game.Energy == null || !_game.Energy.TryConsume(spawnEnergyCost, DateTime.UtcNow))
        {
            SetStatus($"Not enough energy to spawn L{spawnTier} ({spawnEnergyCost} energy) — wait for regen.");
            return;
        }

        string chainId = chains[UnityEngine.Random.Range(0, chains.Count)];
        if (_game.Board.TrySpawn(chainId, spawnTier, out var pos))
        {
            SetStatus($"Spawned {ChainDisplayName(chainId)} L{spawnTier} at {pos}.");
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
        // Abbreviate to fit the cell; Board F: roman-numeral stage badge.
        if (shortName.Length > 8) shortName = shortName.Substring(0, 8);
        string numeral = chainStyle != null ? chainStyle.RomanNumeral(item.Level) : $"Lv{item.Level}";
        return $"{shortName}\n{numeral}";
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
                {
                    // Board F: ivory silk empty cells
                    image.color = chainStyle != null ? chainStyle.cellEmpty : new Color(1f, 1f, 1f, 0.5f);
                }
                else if (selected)
                {
                    image.color = chainStyle != null ? chainStyle.uiGold : new Color(0.95f, 0.80f, 0.35f, 1f);
                }
                else
                {
                    // Board F: chain-family color; higher stages glow more
                    var style = chainStyle != null ? chainStyle.GetStyle(item.ChainId) : null;
                    if (style != null)
                    {
                        float glow = 0.7f + (item.Level * 0.06f); // L1=0.76 → L5=1.0
                        image.color = new Color(
                            Mathf.Min(1f, style.familyColor.r * glow + 0.2f),
                            Mathf.Min(1f, style.familyColor.g * glow + 0.2f),
                            Mathf.Min(1f, style.familyColor.b * glow + 0.2f),
                            1f);
                    }
                    else
                    {
                        image.color = new Color(0.28f, 0.42f, 0.30f, 1f); // fallback estate green
                    }
                }
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
