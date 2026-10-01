// Scandal Season — Runtime game layer.
// OrderQueueView: renders the standing order queue (the free player's coin
// engine). Payouts and slot counts come from the domain; chain names from the
// imported chain definitions.

using UnityEngine;
using UnityEngine.UI;

public sealed class OrderQueueView : MonoBehaviour
{
    [Header("UI")]
    public Text ordersText;
    public Text headerText;
    public Button refreshButton;

    [Header("Board F visual identity")]
    public ChainStyleSO chainStyle;

    private GameManager _game;
    private static int _instanceCounter = 0;
    private int _instanceId;

    private void Start()
    {
        _instanceId = ++_instanceCounter;
        Debug.LogWarning($"[OrderQueueView] Instance #{_instanceId} on '{gameObject.name}' path='{GetPath(transform)}'. Total instances: {_instanceCounter}");
        _game = GameManager.Instance;
        // v9.26: Static scene UI only. HeaderText and OrdersText GameObjects exist
        // in the scene; we get-or-add the Text component exactly once here.
        // No dynamic GameObject creation — that caused the duplication cascade.
        var hdrT = transform.Find("HeaderText");
        if (hdrT != null)
        {
            headerText = hdrT.GetComponent<Text>();
            if (headerText == null) headerText = hdrT.gameObject.AddComponent<Text>();
        }
        var ordT = transform.Find("OrdersText");
        if (ordT != null)
        {
            ordersText = ordT.GetComponent<Text>();
            if (ordersText == null) ordersText = ordT.gameObject.AddComponent<Text>();
        }
        if (ordersText == null || headerText == null)
            Debug.LogWarning("[OrderQueueView] HeaderText/OrdersText not found in scene. Orders UI will not display.");
        // Assign font at runtime (scene can't reference the WebGL-safe font).
        var font = ScandalSeason.Runtime.Game.UIFontHelper.GetFont();
        if (headerText != null)
        {
            if (headerText.font == null) headerText.font = font;
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleLeft;
        }
        if (ordersText != null)
        {
            if (ordersText.font == null) ordersText.font = font;
            ordersText.fontSize = 18;
            ordersText.alignment = TextAnchor.UpperLeft;
        }
        if (refreshButton == null)
            refreshButton = transform.Find("RefreshButton")?.GetComponent<Button>();
        if (refreshButton != null)
            refreshButton.onClick.AddListener(Refresh);
        Refresh();
    }

    private static string GetPath(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        while (t != null) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (_game == null || _game.Orders == null) return;
        _game.Tick(System.DateTime.UtcNow);

        if (headerText != null)
        {
            headerText.text = $"ORDERS ({_game.Orders.StandingOrderCount}/{_game.Orders.MaxStandingOrders}) [#{_instanceId}]";
            if (chainStyle != null)
                headerText.color = chainStyle.uiGold;
        }

        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < _game.Orders.MaxStandingOrders; i++)
        {
            var order = _game.Orders.GetOrder(i);
            if (order == null)
            {
                sb.AppendLine($"[{i + 1}] — refilling…");
                continue;
            }
            var chain = _game.GetChain(order.ChainId);
            string name = chain != null && !string.IsNullOrEmpty(chain.displayName)
                ? chain.displayName
                : order.ChainId;
            string kind = order.IsCommission ? "Commission" : "Custom";
            // Board F: roman numerals for stage.
            string numeral = chainStyle != null ? chainStyle.RomanNumeral(order.Level) : $"Lv{order.Level}";
            sb.AppendLine($"[{i + 1}] {kind}: {name} {numeral} → {order.CoinPayout} coins");
        }

        if (ordersText != null)
            ordersText.text = sb.ToString();
    }
}
