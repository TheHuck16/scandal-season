// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// EstateMapView: the estate map screen. Seven zone slots on the approved
// aerial plate; zones 1-6 with approved art, zone 7 (Ballroom) gated on art.
// Rendering only — state lives in EstateRepairSystem.

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class EstateMapView : MonoBehaviour
{
    [Header("Map")]
    public Image aerialPlate;
    public Transform zoneSlotParent;
    public GameObject zoneSlotPrefab; // Must have Button + Image + Text children.

    [Header("Systems (wired by Boot)")]
    public EstateRepairSystem repairSystem;
    public EstateRegistrySO registry;

    [Header("Zone detail")]
    public EstateZoneDetailView detailView;

    private void OnEnable()
    {
        if (repairSystem != null)
            repairSystem.ZoneRevealed += OnZoneRevealed;
        Refresh();
    }

    private void OnDisable()
    {
        if (repairSystem != null)
            repairSystem.ZoneRevealed -= OnZoneRevealed;
    }

    public void Refresh()
    {
        if (registry == null || zoneSlotParent == null) return;

        // Clear existing slots.
        foreach (Transform child in zoneSlotParent)
            Destroy(child.gameObject);

        foreach (var zone in registry.zones)
        {
            if (zone == null) continue;
            var slot = Instantiate(zoneSlotPrefab, zoneSlotParent);
            var button = slot.GetComponent<Button>();
            var image = slot.GetComponentInChildren<Image>();
            var label = slot.GetComponentInChildren<Text>();

            var state = repairSystem != null
                ? repairSystem.GetState(zone.zoneId)
                : EstateRepairSystem.ZoneState.Locked;

            // Before plate while ruined; final plate once revealed.
            // Ballroom shows a dimmed treatment until its art is approved.
            if (image != null)
            {
                if (state == EstateRepairSystem.ZoneState.Revealed && zone.finalPlate != null)
                    image.sprite = zone.finalPlate;
                else if (zone.beforePlate != null)
                    image.sprite = zone.beforePlate;

                if (!zone.finalArtApproved)
                    image.color = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Dimmed until art lands.
            }

            if (label != null)
                label.text = $"{zone.displayName}\n{zone.tierCost} Estate Funds";

            if (button != null)
            {
                button.interactable = state != EstateRepairSystem.ZoneState.Locked;
                string capturedId = zone.zoneId;
                button.onClick.AddListener(() => OpenZoneDetail(capturedId));
            }
        }
    }

    private void OpenZoneDetail(string zoneId)
    {
        if (detailView != null)
            detailView.Show(zoneId);
    }

    private void OnZoneRevealed(string zoneId)
    {
        Refresh();
    }
}
