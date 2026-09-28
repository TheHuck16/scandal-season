// Scandal Season — Hartwell Park estate repair game (v1.0 spec, Sep 28 2026).
// RevealCodaView: the staged reveal coda. Reuses the approved staged-reveal
// pattern from the fashion ritual: the final plate unveils in stages, then
// the zone flips to restored on the map and the exposure beat fires.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class RevealCodaView : MonoBehaviour
{
    [Header("Reveal")]
    public Image revealPlate;
    public Text revealTitle;
    public Text exposureText;
    public Button continueButton;

    [Header("Staging")]
    [Tooltip("Number of unveil stages (approved staged-reveal pattern)")]
    public int stages = 3;
    [Tooltip("Seconds per stage")]
    public float stageDuration = 1.2f;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void Play(EstateZoneSO zone)
    {
        gameObject.SetActive(true);
        if (revealTitle != null)
            revealTitle.text = $"{zone.displayName} — Restored";
        if (exposureText != null)
            exposureText.text = zone.exposureBeat;
        StartCoroutine(Unveil(zone));
    }

    private IEnumerator Unveil(EstateZoneSO zone)
    {
        if (revealPlate != null)
        {
            revealPlate.sprite = zone.finalPlate;
            // Staged unveil: wipe from dimmed to full across `stages` steps.
            for (int s = 0; s <= stages; s++)
            {
                float t = (float)s / stages;
                revealPlate.color = new Color(t, t, t, 1f);
                yield return new WaitForSeconds(stageDuration / stages);
            }
            revealPlate.color = Color.white;
        }
    }
}
