// Scandal Season — Editor probe (NOT shipped in the player).
// Headless layout diagnostic: opens Main.unity, runs the real Boot ->
// Title -> StoryScene flow, forces a canvas rebuild, and dumps the
// resulting UI state to the log. Run via:
//   Unity -batchmode -nographics -projectPath <project> \
//     -executeMethod StoryLayoutProbe.Probe -quit -logFile <log>

using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StoryLayoutProbe
{
    public static void Probe()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

        var game = Object.FindObjectOfType<GameManager>();
        var uiRoot = Object.FindObjectOfType<UIRoot>();
        var storyView = Object.FindObjectOfType<StorySceneView>();
        var canvas = Object.FindObjectOfType<Canvas>();

        Debug.Log($"[Probe] game={game != null} uiRoot={uiRoot != null} storyView={storyView != null} canvas={canvas != null}");

        var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>("Assets/Resources/ContentRegistry.asset");
        Debug.Log($"[Probe] registry={registry != null} S1={registry.seasonOneScenes.Count}");

        // Invoke private UIRoot.Start via reflection (subscribes + ShowAll(false)).
        var start = typeof(UIRoot).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        // GameManager.Awake never runs in batchmode editor — set Instance manually.
        typeof(GameManager).GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(game, game);
        start.Invoke(uiRoot, null);
        Debug.Log("[Probe] UIRoot.Start invoked (Instance injected)");

        // Boot equivalent.
        game.InitializeSession(registry.seasonOneScenes, registry.seasonTwoScenes,
            registry.seasonThreeScenes, registry.mergeChains);
        Debug.Log($"[Probe] session init, state={game.CurrentState}");

        // Simulate Begin button via the real event path.
        game.SetState(GameState.StoryScene);
        Debug.Log($"[Probe] state={game.CurrentState}");

        // Belt and braces: if the event path failed, call ShowScene directly.
        var storyPanel0 = uiRoot.storyPanel;
        var storyView0 = uiRoot.storyView;
        if (!storyPanel0.activeInHierarchy)
        {
            Debug.Log("[Probe] event path did not activate panel; calling ShowScene directly");
            storyPanel0.SetActive(true);
            bool ok = storyView0.ShowScene(1, 1, 1);
            Debug.Log($"[Probe] direct ShowScene returned {ok}");
        }

        Canvas.ForceUpdateCanvases();

        Debug.Log("[Probe] === STORY LAYOUT AFTER FIX ===");
        Dump("chapterTitle", storyView.chapterTitleText);
        Dump("sceneHeader", storyView.sceneHeaderText);
        Dump("typeBadge", storyView.typeBadgeText);
        Dump("turns", storyView.turnsText);
        Dump("synopsis", storyView.synopsisText);
        Dump("prose", storyView.proseText);
        Dump("body", storyView.bodyText);
        var sr2 = storyView.proseScrollRect;
        if (sr2 != null)
        {
            Debug.Log($"[Probe] scroll rect={RectStr(sr2.GetComponent<RectTransform>())}");
            Debug.Log($"[Probe] content rect={RectStr(sr2.content)} contentH={sr2.content.rect.height:F0}");
        }
        Debug.Log($"[Probe] panel VLG present={uiRoot.storyPanel.GetComponent<VerticalLayoutGroup>() != null}");
        Debug.Log($"[Probe] decisionContainer children={storyView.decisionButtonContainer.childCount}");

        // Advance to scene 2 to exercise ShowScene a second time.
        bool ok2 = storyView.ShowScene(1, 1, 2);
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[Probe] ShowScene(1,1,2)={ok2} chapter='{storyView.chapterTitleText.text}' proseLen={storyView.proseText.text.Length} contentH={storyView.proseScrollRect.content.rect.height:F0}");

        // Scene 12 has a real key decision — exercise the decision buttons.
        bool ok12 = storyView.ShowScene(1, 1, 12);
        Canvas.ForceUpdateCanvases();
        int btnCount = 0;
        foreach (Transform c in storyView.decisionButtonContainer)
            if (c.gameObject.activeSelf) btnCount++;
        Debug.Log($"[Probe] ShowScene(1,1,12)={ok12} activeDecisionBtns={btnCount} body='{storyView.bodyText.text}'");

        // Title layout check.
        var titlePanel = uiRoot.titlePanel;
        var tvlg = titlePanel.GetComponent<VerticalLayoutGroup>();
        Debug.Log($"[Probe] title VLG present={tvlg != null} titleRect={RectStr(uiRoot.titleText.rectTransform)}");
        return;
    }

    private static void Dump(string name, Text t)
    {
        if (t == null) { Debug.Log($"[Probe] {name}: NULL"); return; }
        string preview = t.text.Length > 60 ? t.text.Substring(0, 60) + "..." : t.text;
        Debug.Log($"[Probe] {name}: active={t.gameObject.activeInHierarchy} rect={RectStr(t.rectTransform)} text='{preview.Replace('\n', ' ')}'");
    }

    private static string RectStr(RectTransform rt)
    {
        if (rt == null) return "no-rt";
        return $"pos={rt.anchoredPosition} size={rt.rect.size} anchors=({rt.anchorMin},{rt.anchorMax})";
    }
}
