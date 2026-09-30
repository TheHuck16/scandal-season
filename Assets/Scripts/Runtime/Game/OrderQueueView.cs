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

    private void Start()
    {
        _game = GameManager.Instance;
        // Dynamic UI discovery: if Inspector fields aren't assigned, find them.
        if (ordersText == null)
            ordersText = transform.Find("OrdersText")?.GetComponent<Text>()
                ?? GetComponentInChildren<Text>();
        if (headerText == null)
            headerText = transform.Find("HeaderText")?.GetComponent<Text>();
        if (refreshButton == null)
            refreshButton = GetComponentInChildren<Button>();
        if (refreshButton != null)
            refreshButton.onClick.AddListener(Refresh);
        Refresh();
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
            headerText.text = $"ORDERS ({_game.Orders.StandingOrderCount}/{_game.Orders.MaxStandingOrders})";
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
