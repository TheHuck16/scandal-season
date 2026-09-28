// Scandal Season — Runtime game layer.
// IapManager: Unity Purchasing (StoreKit 2 on iOS) wiring. Product IDs match
// StoreCatalog. Purchases validate via Supabase validate-purchase edge function.
// Money buys Crowns and energy ONLY — never coins, never story (the firewall).

using System;
using UnityEngine;
// using UnityEngine.Purchasing; // Uncomment when Unity Purchasing is initialized.

public sealed class IapManager : MonoBehaviour
{
    public static IapManager Instance { get; private set; }

    [Header("Product IDs (must match App Store Connect)")]
    public string debutantesChestId = "debutantes_chest";
    public string pinMoneyId = "pin_money";
    public string dailyOfferId = "daily_offer";

    [Header("Supabase")]
    [Tooltip("Edge function URL for purchase validation.")]
    public string validatePurchaseUrl = "";

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
    /// Initiates a purchase. The transaction flows: StoreKit → Unity →
    /// Supabase validate-purchase → Apple App Store Server API → credit.
    /// Idempotent: the edge function dedupes by transaction ID.
    /// </summary>
    public void Purchase(string productId)
    {
        Debug.Log($"[IAP] Purchase requested: {productId}");
        // TODO: Initialize Unity Purchasing (Codeless IAP or manual).
        // TODO: On purchase complete, POST the signed transaction to
        //       validatePurchaseUrl for server-side validation.
        // TODO: On validation success, credit Crowns/energy via GameManager.
    }

    /// <summary>Restores previous purchases (iOS requirement).</summary>
    public void RestorePurchases()
    {
        Debug.Log("[IAP] Restore requested");
        // TODO: Unity Purchasing restore.
    }
}
