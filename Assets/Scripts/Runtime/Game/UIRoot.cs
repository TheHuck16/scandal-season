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

        FixTitleLayout();
        ShowAll(false);
    }

    /// <summary>
    /// The scene builder's VerticalLayoutGroup collapses Text children to
    /// zero width (the title rendered as a lone "S"). Position the title
    /// elements with explicit anchors instead.
    /// </summary>
    private void FixTitleLayout()
    {
        if (titlePanel == null) return;
        var vlg = titlePanel.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false; // synchronous: Destroy() lags a frame

        if (titleText != null)
        {
            var rt = titleText.rectTransform;
            rt.anchorMin = new Vector2(0.05f, 0.72f);
            rt.anchorMax = new Vector2(0.95f, 0.90f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            titleText.alignment = TextAnchor.MiddleCenter;
        }
        if (startButton != null)
        {
            var rt = startButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.28f, 0.45f);
            rt.anchorMax = new Vector2(0.72f, 0.55f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        var fair = titlePanel.transform.Find("Fair");
        if (fair != null)
        {
            var rt = fair as RectTransform;
            rt.anchorMin = new Vector2(0.05f, 0.32f);
            rt.anchorMax = new Vector2(0.95f, 0.40f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = fair.GetComponent<Text>();
            if (t != null) t.alignment = TextAnchor.MiddleCenter;
        }
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
