// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// EstateRepairSystem: tracks zone state, completes repair tasks, fires the
// reveal coda and exposure beats. No timers, ever — Estate Funds + merge
// materials are the only throttles. Coins are entirely outside the estate.

using System;
using System.Collections.Generic;
using UnityEngine;
using ScandalSeason.Domain.Economy;

public sealed class EstateRepairSystem : MonoBehaviour
{
    public enum ZoneState { Locked, Before, InRepair, Revealed }

    [Header("Content")]
    public EstateRegistrySO registry;

    [Header("Wallet (wired by Boot)")]
    public GameManager gameManager;

    // zoneId -> state
    private readonly Dictionary<string, ZoneState> _states = new();
    // zoneId -> completed task indexes
    private readonly Dictionary<string, HashSet<int>> _completedTasks = new();

    public event Action<string> ZoneRevealed;       // zoneId
    public event Action<string, int> TaskCompleted; // zoneId, taskIndex

    private void Awake()
    {
        if (registry == null) return;
        foreach (var z in registry.zones)
        {
            if (z == null) continue;
            _states[z.zoneId] = z.zoneOrder == 0 ? ZoneState.Before : ZoneState.Locked;
            _completedTasks[z.zoneId] = new HashSet<int>();
        }
    }

    public ZoneState GetState(string zoneId)
        => _states.TryGetValue(zoneId, out var s) ? s : ZoneState.Locked;

    public bool IsTaskComplete(string zoneId, int taskIndex)
        => _completedTasks.TryGetValue(zoneId, out var set) && set.Contains(taskIndex);

    /// <summary>
    /// Attempts a repair task. Costs Estate Funds only (never Crowns, never coins).
    /// Material availability is checked by the caller via the merge inventory.
    /// </summary>
    public bool TryCompleteTask(string zoneId, int taskIndex)
    {
        var zone = registry?.GetZone(zoneId);
        if (zone == null || taskIndex < 0 || taskIndex >= zone.tasks.Length) return false;
        if (GetState(zoneId) != ZoneState.Before && GetState(zoneId) != ZoneState.InRepair) return false;
        if (IsTaskComplete(zoneId, taskIndex)) return false;

        var task = zone.tasks[taskIndex];
        if (gameManager?.Wallet == null || !gameManager.Wallet.TrySpend(Currency.EstateFunds, task.fundCost)) return false;

        _completedTasks[zoneId].Add(taskIndex);
        _states[zoneId] = ZoneState.InRepair;
        TaskCompleted?.Invoke(zoneId, taskIndex);

        if (_completedTasks[zoneId].Count >= zone.tasks.Length)
            RevealZone(zoneId);

        return true;
    }

    private void RevealZone(string zoneId)
    {
        _states[zoneId] = ZoneState.Revealed;
        ZoneRevealed?.Invoke(zoneId);

        // Unlock the next zone in tier order.
        var zone = registry.GetZone(zoneId);
        foreach (var z in registry.zones)
        {
            if (z != null && z.zoneOrder == zone.zoneOrder + 1 && GetState(z.zoneId) == ZoneState.Locked)
            {
                // Ballroom (zone 7) stays gated until its art is approved.
                if (z.finalArtApproved)
                    _states[z.zoneId] = ZoneState.Before;
                break;
            }
        }
    }

    public string GetExposure(string zoneId)
        => registry?.GetZone(zoneId)?.exposureBeat ?? string.Empty;
}
