// Scandal Season — Runtime game layer.
// UILabelRepair: ensures UI Text components exist on label GameObjects.
// The Main.unity scene has dangling Text component references (missing
// definition blocks). This adds them at runtime so the UI is visible.
// Runs before scene load to ensure labels exist when views initialize.
using UnityEngine;
using UnityEngine.UI;

public sealed class UILabelRepair : MonoBehaviour
{
    // Runs AFTER scene load so scene GameObjects exist. (BeforeSceneLoad
    // runs too early — FindObjectsOfType finds nothing and the repair is a
    // no-op, leaving all labels invisible. v9.28 shipped this way.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Repair()
    {
        RepairLabels();
    }

    /// <summary>
    /// Public entry so views can re-run the repair if they initialize before
    /// the AfterSceneLoad callback (e.g. DontDestroyOnLoad roots).
    /// Idempotent: skips GameObjects that already have a Text component.
    /// Returns the number of Text components added.
    /// </summary>
    public static int RepairLabels()
    {
        // GameObject names that need Text components (dangling in scene)
        string[] labelNames = {
            "TitleText", "Selection", "Chapter", "Status", "TypeBadge",
            "Crowns", "Fair", "Synopsis", "SceneHdr", "Turns",
            "Body", "Coins", "Energy", "Label", "Text"
        };

        var texts = Object.FindObjectsOfType<Text>(true);
        var hasText = new System.Collections.Generic.HashSet<GameObject>();
        foreach (var t in texts) hasText.Add(t.gameObject);

        int fixed_ = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (System.Array.IndexOf(labelNames, go.name) < 0) continue;
            if (hasText.Contains(go)) continue;
            // Only fix UI labels (must have RectTransform)
            if (go.GetComponent<RectTransform>() == null) continue;
            var txt = go.AddComponent<Text>();
            txt.font = ScandalSeason.Runtime.Game.UIFontHelper.GetFont();
            txt.fontSize = 18;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            // Set default text based on name
            txt.text = GetDefaultText(go.name);
            fixed_++;
        }
        if (fixed_ > 0)
            Debug.LogWarning($"[UILabelRepair] Added Text to {fixed_} GameObjects");
        return fixed_;
    }

    private static string GetDefaultText(string name)
    {
        switch (name)
        {
            case "TitleText": return "SCANDAL SEASON";
            case "Chapter": return "Chapter";
            case "Status": return "";
            case "Crowns": return "0";
            case "Coins": return "0";
            case "Energy": return "0";
            case "Turns": return "";
            case "Body": return "";
            case "Label": return "";
            case "Text": return "";
            default: return "";
        }
    }
}
