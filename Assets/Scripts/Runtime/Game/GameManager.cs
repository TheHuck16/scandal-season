// Scandal Season — Runtime game layer.
// GameManager: owns the app state machine and session state (wallet, energy,
// progression, merge board, order queue). All rules come from the domain core;
// all content comes from the generated ScriptableObjects. Nothing invented.

using System;
using System.Collections.Generic;
using UnityEngine;
using ScandalSeason.Domain.Economy;
using ScandalSeason.Domain.Merge;
using ScandalSeason.Domain.Progression;

public enum GameState
{
    Boot,
    Title,
    StoryScene,
    MergeBoard
}

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Content (wired by Boot)")]
    public List<SceneDefinitionSO> seasonOneScenes = new List<SceneDefinitionSO>();
    public List<SceneDefinitionSO> seasonTwoScenes = new List<SceneDefinitionSO>();
    public List<SceneDefinitionSO> seasonThreeScenes = new List<SceneDefinitionSO>();
    public List<ItemChainDefinitionSO> mergeChains = new List<ItemChainDefinitionSO>();

    [Header("Board config (LOCKED Sep 28, 2026: 8x8)")]
    public int boardWidth = 8;
    public int boardHeight = 8;

    [Header("Chain unlocks (LOCKED Sep 27: 5 at launch, rest at 5/10/15/20)")]
    [Tooltip("LOCKED Sep 27: 5 chains at launch (Needlework, Pearls, Ribbon, Lace, Posy); remaining 4 unlock at player levels 5/10/15/20. ID-to-name mapping TBD — placeholder IDs below, do not treat as canonical.")]
    public List<string> launchChainIds = new List<string>();

    [Header("Chain unlock levels (LOCKED Sep 27: 5 / 10 / 15 / 20)")]
    [Tooltip("Player levels at which the 4 post-launch chains unlock, in order.")]
    public int[] chainUnlockLevels = new int[] { 5, 10, 15, 20 };

    public GameState CurrentState { get; private set; } = GameState.Boot;

    // Session state — domain objects, plain C#.
    public Wallet Wallet { get; private set; }
    public EnergySystem Energy { get; private set; }
    public SeasonProgression Progression { get; private set; }
    public MergeBoard Board { get; private set; }
    public OrderQueue Orders { get; private set; }

    // Story progress: which scene the player is on.
    public int CurrentSeason { get; private set; } = 1;
    public int CurrentChapter { get; private set; } = 1;
    public int CurrentSceneNumber { get; private set; } = 1;

    // Player level drives chain unlocks (LOCKED Sep 27). Thresholds TBD.
    public int PlayerLevel { get; private set; } = 1;

    public event Action<GameState> OnStateChanged;

    private readonly Dictionary<string, ItemChainDefinitionSO> _chainsById =
        new Dictionary<string, ItemChainDefinitionSO>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Called once by Boot after content is loaded. Uses locked economy figures:
    /// energy cap 200, 1 per 3 minutes. Chain unlocks: 5 at launch by player
    /// level (LOCKED Sep 27); which chains and level thresholds are TBD.
    /// New-player wallet (LOCKED Sep 27): 100 Crowns, 200 energy, 0 coins.
    /// </summary>
    public void InitializeSession(
        List<SceneDefinitionSO> s1,
        List<SceneDefinitionSO> s2,
        List<SceneDefinitionSO> s3,
        List<ItemChainDefinitionSO> chains)
    {
        seasonOneScenes = s1 ?? new List<SceneDefinitionSO>();
        seasonTwoScenes = s2 ?? new List<SceneDefinitionSO>();
        seasonThreeScenes = s3 ?? new List<SceneDefinitionSO>();
        mergeChains = chains ?? new List<ItemChainDefinitionSO>();

        _chainsById.Clear();
        foreach (var c in mergeChains)
            if (c != null && !string.IsNullOrEmpty(c.chainId))
                _chainsById[c.chainId] = c;

        Wallet = new Wallet(crowns: 100, coins: 0);
        Energy = new EnergySystem(
            maxEnergy: 200,
            regenInterval: TimeSpan.FromMinutes(3),
            startEnergy: 200,
            startUtc: DateTime.UtcNow);
        Progression = new SeasonProgression();
        var chainMaxLevels = new Dictionary<string, int>();
        foreach (var kv in _chainsById)
            chainMaxLevels[kv.Key] = kv.Value.levels.Length;
        Board = new MergeBoard(boardWidth, boardHeight, chainMaxLevels: chainMaxLevels);

        // Level-gated unlocks (LOCKED Sep 27): 5 at launch, remaining 4 at
        // player levels 5/10/15/20. Chain ID lists are TBD pending name mapping.
        var unlocked = new List<string>();
        foreach (var id in launchChainIds)
            if (_chainsById.ContainsKey(id))
                unlocked.Add(id);
        Orders = new OrderQueue(unlocked, chainMaxLevels);

        SetState(GameState.Title);
    }

    public void SetState(GameState next)
    {
        if (CurrentState == next) return;
        CurrentState = next;
        OnStateChanged?.Invoke(next);
    }

    public ItemChainDefinitionSO GetChain(string chainId)
    {
        _chainsById.TryGetValue(chainId, out var chain);
        return chain;
    }

    public IReadOnlyList<string> UnlockedChainIds => Orders?.UnlockedChainIds;

    /// <summary>All scenes for the current season/chapter, ordered by scene number.</summary>
    public List<SceneDefinitionSO> GetChapterScenes(int season, int chapter)
    {
        var source = season == 1 ? seasonOneScenes : season == 2 ? seasonTwoScenes : seasonThreeScenes;
        var result = new List<SceneDefinitionSO>();
        foreach (var s in source)
            if (s != null && s.chapter == chapter)
                result.Add(s);
        result.Sort((a, b) => a.sceneNumber.CompareTo(b.sceneNumber));
        return result;
    }

    public SceneDefinitionSO GetScene(int season, int chapter, int sceneNumber)
    {
        var scenes = GetChapterScenes(season, chapter);
        foreach (var s in scenes)
            if (s.sceneNumber == sceneNumber)
                return s;
        return null;
    }

    public void AdvanceStory()
    {
        // 40 scenes per chapter, 30 chapters per season (locked structure).
        if (CurrentSceneNumber < GameRules.ScenesPerChapter)
        {
            CurrentSceneNumber++;
        }
        else if (CurrentChapter < GameRules.ChaptersPerSeason)
        {
            CurrentChapter++;
            CurrentSceneNumber = 1;
        }
        else
        {
            Progression.AdvanceSeason();
            CurrentSeason = Progression.CurrentSeasonNumber;
            CurrentChapter = 1;
            CurrentSceneNumber = 1;
        }
    }

    // Key decision persistence: which option the player chose per scene.
    // Keyed "decision_S{C}C{Ch}S{S}" -> option index. Persisted via PlayerPrefs.
    private readonly Dictionary<string, int> _decisionChoices = new Dictionary<string, int>();

    private static string DecisionKey(int season, int chapter, int sceneNumber)
        => $"decision_S{season}C{chapter}S{sceneNumber}";

    public void RecordDecision(int season, int chapter, int sceneNumber, int optionIndex)
    {
        string key = DecisionKey(season, chapter, sceneNumber);
        _decisionChoices[key] = optionIndex;
        PlayerPrefs.SetInt(key, optionIndex);
        PlayerPrefs.Save();
    }

    public int GetDecisionChoice(int season, int chapter, int sceneNumber)
    {
        string key = DecisionKey(season, chapter, sceneNumber);
        if (_decisionChoices.TryGetValue(key, out int cached))
            return cached;
        if (PlayerPrefs.HasKey(key))
        {
            int saved = PlayerPrefs.GetInt(key, -1);
            _decisionChoices[key] = saved;
            return saved;
        }
        return -1;
    }

    /// <summary>
    /// Story scenes cost coins (locked: 120–290 computed at import, never Crowns).
    /// Uses the scene's imported price. Returns false when the wallet can't cover it.
    /// Tutorial: S1 C1 opening scenes (1-5) are free — the player gets a taste
    /// with no coins from the start, then the coin economy begins.
    /// </summary>
    public bool TryPaySceneCost(SceneDefinitionSO scene)
    {
        // Tutorial-free pricing (Beth-locked Sep 27, 2026): "Season One's
        // tutorial-free pricing" / "the tutorial ritual is FREE — onboarding
        // teaches the system before the coin band applies." Season 1 Chapter 1
        // is the tutorial chapter: all 40 scenes free. The coin band applies
        // from Chapter 2 onward. (Boundary: full C1; narrow per Beth's review.)
        if (scene != null && scene.season == 1 && scene.chapter == 1)
            return true;
        int cost = scene != null && scene.coinPrice > 0 ? scene.coinPrice : 120;
        return Wallet.TrySpend(Currency.Coins, cost);
    }

    public void Tick(DateTime nowUtc)
    {
        Orders?.Refresh(nowUtc);
        // Energy regen is pull-based in the domain; HUD reads Current.
    }
}
