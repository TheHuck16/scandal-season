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
    public Button refreshButton;

    private GameManager _game;

    private void Start()
    {
        _game = GameManager.Instance;
        if (refreshButton != null)
            refreshButton.onClick.AddListener(Refresh);
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (_game == null || _game.Orders == null) return;
        _game.Tick(System.DateTime.UtcNow);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ORDERS ({_game.Orders.StandingOrderCount}/{_game.Orders.MaxStandingOrders})");
        sb.AppendLine();

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
            sb.AppendLine($"[{i + 1}] {kind}: {name} Lv{order.Level} → {order.CoinPayout} coins");
        }

        if (ordersText != null)
            ordersText.text = sb.ToString();
    }
}
