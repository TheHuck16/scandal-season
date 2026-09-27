// Scandal Season — Editor tools.
// MainSceneBuilder: constructs the Main game scene (Boot → Title → Story ↔
// Board) with all UI wired. Run via the menu; saves to Assets/Scenes/Main.unity.
//
// SCAFFOLD STYLING: colors, sizes, and layout are placeholders, not validated
// against the brand rules. The visual pass comes later.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainSceneBuilder
{
    [MenuItem("Scandal Season/Build Main Scene")]
    public static void BuildMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Main";

        // --- Managers ---
        var managers = new GameObject("Managers");
        var gameManager = managers.AddComponent<GameManager>();
        gameManager.boardWidth = 6;
        gameManager.boardHeight = 6;
        managers.AddComponent<Boot>();

        // --- Event system + Canvas ---
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        var uiRootGO = new GameObject("UIRoot");
        uiRootGO.transform.SetParent(canvasGO.transform, false);
        var uiRoot = uiRootGO.AddComponent<UIRoot>();

        // --- HUD (persistent) ---
        var hud = Panel(canvasGO.transform, "HUD", new Color(0.08f, 0.08f, 0.10f, 0.92f));
        var hudComp = hud.AddComponent<EconomyHUD>();
        hudComp.crownsText = Label(hud.transform, "Crowns", 28, TextAnchor.MiddleLeft);
        hudComp.coinsText = Label(hud.transform, "Coins", 28, TextAnchor.MiddleLeft);
        hudComp.energyText = Label(hud.transform, "Energy", 28, TextAnchor.MiddleLeft);
        hudComp.storyButton = Button(hud.transform, "StoryBtn", "Story").GetComponent<Button>();
        hudComp.boardButton = Button(hud.transform, "BoardBtn", "Board").GetComponent<Button>();
        LayoutHorizontal(hud);
        uiRoot.hudPanel = hud;

        // --- Title panel ---
        var title = Panel(canvasGO.transform, "TitlePanel", new Color(0.10f, 0.16f, 0.12f, 1f));
        var titleText = BigLabel(title.transform, "TitleText", "SCANDAL SEASON", 64);
        var startBtn = Button(title.transform, "StartBtn", "Begin — S1 Chapter 1");
        var fairText = Label(title.transform, "Fair", 22, TextAnchor.MiddleCenter);
        fairText.text = "No ads. Time earns everything.";
        uiRoot.titlePanel = title;
        uiRoot.titleText = titleText;
        uiRoot.startButton = startBtn.GetComponent<Button>();

        // --- Story panel ---
        var story = Panel(canvasGO.transform, "StoryPanel", new Color(0.12f, 0.10f, 0.14f, 1f));
        var storyView = story.AddComponent<StorySceneView>();
        storyView.chapterTitleText = Label(story.transform, "Chapter", 30, TextAnchor.MiddleCenter);
        storyView.sceneHeaderText = Label(story.transform, "SceneHdr", 24, TextAnchor.MiddleCenter);
        storyView.typeBadgeText = Label(story.transform, "TypeBadge", 24, TextAnchor.MiddleCenter);
        storyView.turnsText = Label(story.transform, "Turns", 24, TextAnchor.MiddleCenter);
        storyView.synopsisText = Label(story.transform, "Synopsis", 26, TextAnchor.UpperLeft);
        storyView.bodyText = Label(story.transform, "Body", 24, TextAnchor.UpperLeft);
        var contBtn = Button(story.transform, "ContinueBtn", "Continue →");
        storyView.continueButton = contBtn.GetComponent<Button>();
        var toBoardBtn = Button(story.transform, "ToBoardBtn", "Merge Board");
        storyView.toBoardButton = toBoardBtn.GetComponent<Button>();
        uiRoot.storyPanel = story;
        uiRoot.storyView = storyView;

        // --- Board panel ---
        var board = Panel(canvasGO.transform, "BoardPanel", new Color(0.08f, 0.12f, 0.10f, 1f));
        var boardView = board.AddComponent<MergeBoardView>();

        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(board.transform, false);
        var gridLayout = gridGO.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(96, 96);
        gridLayout.spacing = new Vector2(6, 6);
        boardView.gridParent = gridGO.transform;
        boardView.gridLayout = gridLayout;

        // Cell prefab (built in code, stored under the scene root for the build).
        var cellPrefab = BuildCellPrefab();
        boardView.cellPrefab = cellPrefab;

        var spawnBtn = Button(board.transform, "SpawnBtn", "Spawn");
        boardView.spawnButton = spawnBtn.GetComponent<Button>();
        var mergeBtn = Button(board.transform, "MergeBtn", "Merge");
        boardView.mergeButton = mergeBtn.GetComponent<Button>();
        boardView.statusText = Label(board.transform, "Status", 24, TextAnchor.MiddleCenter);
        boardView.selectionText = Label(board.transform, "Selection", 24, TextAnchor.MiddleCenter);

        var ordersView = board.AddComponent<OrderQueueView>();
        ordersView.ordersText = Label(board.transform, "Orders", 22, TextAnchor.UpperLeft);
        var refreshBtn = Button(board.transform, "RefreshOrdersBtn", "Refresh Orders");
        ordersView.refreshButton = refreshBtn.GetComponent<Button>();

        uiRoot.boardPanel = board;
        uiRoot.boardView = boardView;
        uiRoot.ordersView = ordersView;

        // Hide the prefab from the scene hierarchy view clutter (keep it functional).
        cellPrefab.hideFlags = HideFlags.HideInHierarchy;

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        Debug.Log("[Scandal Season] Main scene built at Assets/Scenes/Main.unity.");
    }

    private static GameObject Panel(Transform parent, string name, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = bg;
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 12;
        layout.childControlHeight = false;
        return go;
    }

    private static Text Label(Transform parent, string name, int size, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.text = "";
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, size * 1.6f);
        return t;
    }

    private static Text BigLabel(Transform parent, string name, string text, int size)
    {
        var t = Label(parent, name, size, TextAnchor.MiddleCenter);
        t.text = text;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(0.95f, 0.82f, 0.45f); // warm gold
        return t;
    }

    private static GameObject Button(Transform parent, string name, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320, 72);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.24f, 0.36f, 0.26f, 1f);
        var btn = go.AddComponent<Button>();
        var label = Label(go.transform, "Text", 28, TextAnchor.MiddleCenter);
        label.text = text;
        var labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        return go;
    }

    private static GameObject BuildCellPrefab()
    {
        var go = new GameObject("CellPrefab");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(96, 96);
        go.AddComponent<Image>();
        go.AddComponent<Button>();
        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var t = label.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 20;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        return go;
    }

    private static void LayoutHorizontal(GameObject hud)
    {
        // HUD uses a horizontal strip at the top.
        var rt = hud.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, -110); rt.offsetMax = new Vector2(0, 0);
        var vlg = hud.GetComponent<VerticalLayoutGroup>();
        Object.DestroyImmediate(vlg);
        var hlg = hud.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 12, 12);
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleCenter;
    }
}
