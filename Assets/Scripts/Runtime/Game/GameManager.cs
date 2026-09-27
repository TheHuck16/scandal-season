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
    public List<ItemChainDefinitionSO> mergeChains = new List<ItemChainDefinitionSO>();

    [Header("Board config (SCAFFOLD default — board size not locked)")]
    public int boardWidth = 6;
    public int boardHeight = 6;

    /// <summary>
    /// SCAFFOLD: story scene coin cost. Locked: 120–290 coins, never Crowns.
    /// The per-season figure is UNDECIDED — this uses the locked range floor
    /// as a placeholder until Beth locks the curve.
    /// </summary>
    public const int ScaffoldSceneCostCoins = 120;

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
    /// energy cap 200, 1 per 3 minutes. New-player wallet and chain unlocks are
    /// SCAFFOLD defaults (both undecided) — zero wallet, all chains unlocked.
    /// </summary>
    public void InitializeSession(
        List<SceneDefinitionSO> s1,
        List<SceneDefinitionSO> s2,
        List<ItemChainDefinitionSO> chains)
    {
        seasonOneScenes = s1 ?? new List<SceneDefinitionSO>();
        seasonTwoScenes = s2 ?? new List<SceneDefinitionSO>();
        mergeChains = chains ?? new List<ItemChainDefinitionSO>();

        _chainsById.Clear();
        foreach (var c in mergeChains)
            if (c != null && !string.IsNullOrEmpty(c.chainId))
                _chainsById[c.chainId] = c;

        Wallet = new Wallet(0, 0);
        Energy = new EnergySystem(
            maxEnergy: 200,
            regenInterval: TimeSpan.FromMinutes(3),
            startEnergy: 200,
            startUtc: DateTime.UtcNow);
        Progression = new SeasonProgression();
        Board = new MergeBoard(boardWidth, boardHeight);

        var unlocked = new List<string>(_chainsById.Keys);
        Orders = new OrderQueue(unlocked);

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
        var source = season == 1 ? seasonOneScenes : seasonTwoScenes;
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

    /// <summary>
    /// Story scenes cost coins (locked: 120–290, never Crowns). The per-season
    /// figure is undecided — this build uses the scaffold placeholder.
    /// Returns false when the wallet can't cover it.
    /// </summary>
    public bool TryPaySceneCost()
    {
        return Wallet.TrySpend(Currency.Coins, ScaffoldSceneCostCoins);
    }

    public void Tick(DateTime nowUtc)
    {
        Orders?.Refresh(nowUtc);
        // Energy regen is pull-based in the domain; HUD reads Current.
    }
}
