// UNITY-DEPENDENT — thin Unity glue over ScandalSeason.Domain.Economy.
// No game logic here. Crowns = premium, coins = soft. No ads, ever.

using System;
using UnityEngine;
using ScandalSeason.Domain.Economy;

public sealed class EconomyView : MonoBehaviour
{
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private float regenMinutes = 5f;
    [SerializeField] private int startingCrowns;
    [SerializeField] private int startingCoins;

    private Wallet _wallet = null!;
    private EnergySystem _energy = null!;

    public Wallet Wallet => _wallet;
    public EnergySystem Energy => _energy;

    private void Awake()
    {
        _wallet = new Wallet(startingCrowns, startingCoins);
        _energy = new EnergySystem(maxEnergy, TimeSpan.FromMinutes(regenMinutes), maxEnergy, DateTime.UtcNow);
    }

    private void Update() => _energy.Refresh(DateTime.UtcNow);
}
