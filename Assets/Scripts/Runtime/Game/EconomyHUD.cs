// Scandal Season — Runtime game layer.
// EconomyHUD: persistent wallet + energy readout. Figures are locked:
// Crowns (premium) / coins (soft), energy cap 200, 1 per 3 minutes.
// Iron rule: time earns everything money can.

using UnityEngine;
using UnityEngine.UI;
using ScandalSeason.Domain.Economy;

public sealed class EconomyHUD : MonoBehaviour
{
    [Header("UI")]
    public Text crownsText;
    public Text coinsText;
    public Text energyText;

    [Header("Premium styling (visual lock v1)")]
    [Tooltip("Gold for Crowns (premium currency).")]
    public Color crownsGold = new Color(0.83f, 0.69f, 0.35f);
    [Tooltip("Warm copper for coins (soft currency).")]
    public Color coinsCopper = new Color(0.72f, 0.45f, 0.25f);
    [Tooltip("Soft green for energy.")]
    public Color energyGreen = new Color(0.45f, 0.65f, 0.45f);

    [Header("Nav")]
    public Button storyButton;
    public Button boardButton;

    private GameManager _game;

    private void Start()
    {
        _game = GameManager.Instance;
        if (storyButton != null)
            storyButton.onClick.AddListener(() => _game.SetState(GameState.StoryScene));
        if (boardButton != null)
            boardButton.onClick.AddListener(() => _game.SetState(GameState.MergeBoard));

        // Apply premium styling once at startup.
        if (crownsText != null) crownsText.color = crownsGold;
        if (coinsText != null) coinsText.color = coinsCopper;
        if (energyText != null) energyText.color = energyGreen;
    }

    private void Update()
    {
        if (_game == null || _game.Wallet == null) return;

        if (crownsText != null)
            crownsText.text = $"♛ {_game.Wallet.Crowns}";
        if (coinsText != null)
            coinsText.text = $"◉ {_game.Wallet.Coins}";
        if (energyText != null && _game.Energy != null)
            energyText.text = $"⚡ {_game.Energy.Current}/{_game.Energy.MaxEnergy}";
    }
}
