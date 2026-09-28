// Scandal Season — Runtime game layer.
// UIRoot: shows/hides panels as the GameManager state machine moves.
// SCAFFOLD: Title → story (opens S1C1) ↔ merge board. Chapter select and
// results screens are not built yet — the state machine covers only what exists.

using UnityEngine;
using UnityEngine.UI;

public sealed class UIRoot : MonoBehaviour
{
    [Header("Panels")]
    public GameObject titlePanel;
    public GameObject storyPanel;
    public GameObject boardPanel;
    public GameObject hudPanel;

    [Header("Title")]
    public Text titleText;
    public Button startButton;
    [Tooltip("Approved app icon (Icon A: emerald cameo).")]
    public Image titleIconImage;

    [Header("Views")]
    public StorySceneView storyView;
    public MergeBoardView boardView;
    public OrderQueueView ordersView;

    private GameManager _game;

    private void Start()
    {
        _game = GameManager.Instance;
        if (_game != null)
            _game.OnStateChanged += OnStateChanged;

        if (titleText != null)
            titleText.text = "SCANDAL SEASON";
        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        ShowAll(false);
    }

    private void OnDestroy()
    {
        if (_game != null)
            _game.OnStateChanged -= OnStateChanged;
    }

    private void OnStartPressed()
    {
        // This slice opens Season 1, Chapter 1, Scene 1.
        _game.SetState(GameState.StoryScene);
    }

    private void OnStateChanged(GameState state)
    {
        ShowAll(false);
        if (hudPanel != null) hudPanel.SetActive(state != GameState.Boot && state != GameState.Title);

        switch (state)
        {
            case GameState.Title:
                if (titlePanel != null) titlePanel.SetActive(true);
                break;
            case GameState.StoryScene:
                if (storyPanel != null) storyPanel.SetActive(true);
                if (storyView != null)
                    storyView.ShowScene(_game.CurrentSeason, _game.CurrentChapter, _game.CurrentSceneNumber);
                break;
            case GameState.MergeBoard:
                if (boardPanel != null) boardPanel.SetActive(true);
                if (boardView != null) boardView.RefreshAll();
                if (ordersView != null) ordersView.Refresh();
                break;
        }
    }

    private void ShowAll(bool visible)
    {
        if (titlePanel != null) titlePanel.SetActive(visible);
        if (storyPanel != null) storyPanel.SetActive(visible);
        if (boardPanel != null) boardPanel.SetActive(visible);
    }
}
