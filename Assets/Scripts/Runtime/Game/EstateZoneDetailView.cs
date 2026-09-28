// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// EstateZoneDetailView: per-zone repair screen. Lists the zone's 3-5 tasks
// with coin costs and material requirements; completing all tasks fires the
// staged reveal coda.

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class EstateZoneDetailView : MonoBehaviour
{
    [Header("Zone header")]
    public Image zonePlate;
    public Text zoneTitle;
    public Text zoneCost;

    [Header("Tasks")]
    public Transform taskListParent;
    public GameObject taskRowPrefab; // Must have Button + Text children.

    [Header("Reveal")]
    public RevealCodaView revealCoda;

    [Header("Systems (wired by Boot)")]
    public EstateRepairSystem repairSystem;
    public EstateRegistrySO registry;

    private string _currentZoneId;

    public void Show(string zoneId)
    {
        _currentZoneId = zoneId;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        var zone = registry?.GetZone(_currentZoneId);
        if (zone == null) return;

        var state = repairSystem.GetState(_currentZoneId);

        if (zoneTitle != null)
            zoneTitle.text = zone.displayName;
        if (zoneCost != null)
            zoneCost.text = $"{zone.tierCost} coins";
        if (zonePlate != null)
        {
            zonePlate.sprite = state == EstateRepairSystem.ZoneState.Revealed && zone.finalPlate != null
                ? zone.finalPlate
                : zone.beforePlate;
        }

        foreach (Transform child in taskListParent)
            Destroy(child.gameObject);

        for (int i = 0; i < zone.tasks.Length; i++)
        {
            var task = zone.tasks[i];
            var row = Instantiate(taskRowPrefab, taskListParent);
            var button = row.GetComponent<Button>();
            var label = row.GetComponentInChildren<Text>();

            bool done = repairSystem.IsTaskComplete(_currentZoneId, i);
            if (label != null)
                label.text = done
                    ? $"✓ {task.taskName}"
                    : $"{task.taskName} — {task.coinCost} coins + {task.materialCount}× {task.materialItemId}";

            if (button != null)
            {
                button.interactable = !done && state != EstateRepairSystem.ZoneState.Locked;
                int captured = i;
                button.onClick.AddListener(() => OnTaskClicked(captured));
            }
        }
    }

    private void OnTaskClicked(int taskIndex)
    {
        if (repairSystem.TryCompleteTask(_currentZoneId, taskIndex))
            Refresh();
    }

    private void OnEnable()
    {
        if (repairSystem != null)
            repairSystem.ZoneRevealed += OnZoneRevealed;
    }

    private void OnDisable()
    {
        if (repairSystem != null)
            repairSystem.ZoneRevealed -= OnZoneRevealed;
    }

    private void OnZoneRevealed(string zoneId)
    {
        if (zoneId != _currentZoneId) return;
        var zone = registry.GetZone(zoneId);
        if (revealCoda != null && zone != null)
            revealCoda.Play(zone);
        Refresh();
    }
}
