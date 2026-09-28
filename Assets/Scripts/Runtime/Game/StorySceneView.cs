// Scandal Season — Runtime game layer.
// StorySceneView: renders a SceneDefinitionSO. Displays ONLY structured data
// from the import (titles, turn counts, decision options, ritual briefs,
// stings). Full scene prose lives in the chapter Markdown, not in the build.

using UnityEngine;
using UnityEngine.UI;

public sealed class StorySceneView : MonoBehaviour
{
    [Header("UI")]
    public Text chapterTitleText;
    public Text sceneHeaderText;
    public Text typeBadgeText;
    public Text turnsText;
    public Text synopsisText;
    public Text bodyText; // decisions / ritual / sting / fashion
    public Button continueButton;
    public Button toBoardButton;

    [Header("Editorial styling (visual lock v1)")]
    [Tooltip("Background image for estate plates / scene art.")]
    public Image backgroundImage;
    [Tooltip("Estate plates by scene context (arrival, orangery, folly).")]
    public Sprite arrivalPlate;
    public Sprite orangeryPlate;
    public Sprite follyPlate;
    [Tooltip("Warm dark text for ivory backgrounds.")]
    public Color bodyTextColor = new Color(0.25f, 0.2f, 0.15f);
    [Tooltip("Gold accent for headers and badges.")]
    public Color goldAccent = new Color(0.83f, 0.69f, 0.35f);

    private GameManager _game;
    private SceneDefinitionSO _scene;

    private void Start()
    {
        _game = GameManager.Instance;
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);
        if (toBoardButton != null)
            toBoardButton.onClick.AddListener(() => _game.SetState(GameState.MergeBoard));
    }

    /// <summary>Loads and renders a scene. Returns false if the scene is missing.</summary>
    public bool ShowScene(int season, int chapter, int sceneNumber)
    {
        _scene = _game.GetScene(season, chapter, sceneNumber);
        if (_scene == null) return false;

        if (chapterTitleText != null)
            chapterTitleText.text = $"S{_scene.season} · Chapter {_scene.chapter}: {_scene.chapterTitle}";
        if (sceneHeaderText != null)
            sceneHeaderText.text = $"Scene {_scene.sceneNumber} of 40";
        if (typeBadgeText != null)
        {
            typeBadgeText.text = TypeLabel(_scene.type);
            typeBadgeText.color = goldAccent;
        }
        if (turnsText != null)
            turnsText.text = _scene.playerTurns > 0
                ? $"{_scene.playerTurns} player turns"
                : "Turns: see chapter text";
        if (synopsisText != null)
            synopsisText.text = _scene.synopsis;
        if (bodyText != null)
            bodyText.text = BuildBody(_scene);

        // Set background plate by chapter (S1: arrival→orangery→folly rotation).
        // Full plate-to-scene mapping is content work; this is the scaffold.
        if (backgroundImage != null)
        {
            Sprite plate = null;
            int chapterMod = _scene.chapter % 3;
            if (chapterMod == 1 && arrivalPlate != null) plate = arrivalPlate;
            else if (chapterMod == 2 && orangeryPlate != null) plate = orangeryPlate;
            else if (chapterMod == 0 && follyPlate != null) plate = follyPlate;
            if (plate != null)
            {
                backgroundImage.sprite = plate;
                backgroundImage.color = new Color(1f, 1f, 1f, 0.25f); // subtle backdrop
            }
        }

        return true;
    }

    private static string TypeLabel(SceneType type)
    {
        switch (type)
        {
            case SceneType.Dialogue: return "Dialogue";
            case SceneType.FashionSelection: return "Fashion selection";
            case SceneType.ChapterClimax: return "Dressing ritual";
            case SceneType.Texture: return "Texture";
            case SceneType.PlotBeat: return "Story beat";
            case SceneType.GazetteSting: return "Gazette sting";
            case SceneType.Cliffhanger: return "Cliffhanger";
            default: return type.ToString();
        }
    }

    /// <summary>
    /// Builds the interactive body from structured data only.
    /// Key decisions list their authored options; rituals show the brief,
    /// directions, and pin counts; stings show Bell's verdict.
    /// </summary>
    private static string BuildBody(SceneDefinitionSO scene)
    {
        var sb = new System.Text.StringBuilder();

        if (scene.keyDecision != null)
        {
            sb.AppendLine($"KEY DECISION {scene.keyDecision.number}: {scene.keyDecision.title}");
            sb.AppendLine();
            char opt = 'A';
            foreach (var o in scene.keyDecision.options)
            {
                sb.AppendLine($"{opt}. {o.label}");
                if (!string.IsNullOrEmpty(o.detail))
                    sb.AppendLine($"   {o.detail}");
                opt++;
            }
            sb.AppendLine();
            sb.AppendLine("The game remembers how she does it — tone, relationships, Gazette flavor.");
        }

        if (scene.ritual != null)
        {
            var r = scene.ritual;
            if (!string.IsNullOrEmpty(r.part))
                sb.AppendLine(r.part);
            if (!string.IsNullOrEmpty(r.occasionBrief))
            {
                sb.AppendLine("Occasion:");
                sb.AppendLine(r.occasionBrief);
                sb.AppendLine();
            }
            if (r.directions != null && r.directions.Length > 0)
            {
                sb.AppendLine("Directions:");
                char d = 'A';
                foreach (var dir in r.directions)
                {
                    sb.AppendLine($"{d}. {dir}");
                    d++;
                }
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(r.stepsSummary))
                sb.AppendLine(r.stepsSummary);
            if (r.steps != null && r.steps.Length > 0)
                sb.AppendLine($"{r.steps.Length} ritual pins · {r.coinPerDecision} coins each");
            else if (r.coinTotal > 0)
                sb.AppendLine($"Ritual total: {r.coinTotal} coins");
        }

        if (scene.fashionChoices != null && scene.fashionChoices.Length > 0)
        {
            sb.AppendLine("Fashion choices (remembered, no coin cost):");
            foreach (var f in scene.fashionChoices)
                sb.AppendLine($"• {f.label}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(scene.sting))
        {
            sb.AppendLine("— The Gazette —");
            sb.AppendLine(scene.sting);
        }

        if (scene.participants != null && scene.participants.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("With: " + string.Join(", ", scene.participants));
        }

        string result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result)
            ? "Play the scene — full prose is in the chapter text."
            : result;
    }

    private void OnContinue()
    {
        // Story scenes cost coins, never Crowns (locked). Price is computed at
        // import per scene. Energy is the only throttle; plot is never time-gated.
        if (!_game.TryPaySceneCost(_scene))
        {
            if (bodyText != null)
                bodyText.text = "Not enough coins — earn them on the merge board, then continue the story.";
            _game.SetState(GameState.MergeBoard);
            return;
        }
        _game.AdvanceStory();
        if (!ShowScene(_game.CurrentSeason, _game.CurrentChapter, _game.CurrentSceneNumber))
        {
            // No more authored scenes (e.g. past S2) — fall back to the board.
            _game.SetState(GameState.MergeBoard);
        }
    }
}
