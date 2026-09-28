// Scandal Season — Runtime game layer.
// AvatarCreatorView: implements the approved avatar creator UI v3 (Sep 28, 2026).
// Bright Regency dressing room, live Rose preview, category-only labels
// (Face / Skin / Body / Hair Colour / Eyes — options unnamed per Beth's rule),
// RANDOMIZE / CONFIRM LOOK buttons. Always changeable, ungated, unmonetized.

using UnityEngine;
using UnityEngine.UI;

public sealed class AvatarCreatorView : MonoBehaviour
{
    [Header("Avatar system")]
    public AvatarSystem avatarSystem;

    [Header("Preview")]
    [Tooltip("Live Rose preview (uses the v3 face anchor styling).")]
    public Image previewImage;

    [Header("Category tabs (labels only, no option names)")]
    public Button faceTab;
    public Button skinTab;
    public Button bodyTab;
    public Button hairColourTab;
    public Button eyesTab;

    [Header("Option grid")]
    public Transform optionGrid;
    public GameObject optionButtonPrefab;

    [Header("Actions")]
    public Button randomizeButton;
    public Button confirmButton;

    private string _activeCategory = "Face";

    private void Start()
    {
        if (faceTab != null) faceTab.onClick.AddListener(() => ShowCategory("Face"));
        if (skinTab != null) skinTab.onClick.AddListener(() => ShowCategory("Skin"));
        if (bodyTab != null) bodyTab.onClick.AddListener(() => ShowCategory("Body"));
        if (hairColourTab != null) hairColourTab.onClick.AddListener(() => ShowCategory("Hair Colour"));
        if (eyesTab != null) eyesTab.onClick.AddListener(() => ShowCategory("Eyes"));

        if (randomizeButton != null) randomizeButton.onClick.AddListener(Randomize);
        if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmLook);

        ShowCategory("Face");
    }

    private void ShowCategory(string category)
    {
        _activeCategory = category;
        // TODO: Populate optionGrid with swatches/icons for the category.
        // Face: 5 silhouettes. Skin: 5 swatches. Body: 4 silhouettes.
        // Hair Colour: 6 swatches. Eyes: 6 swatches.
        // All unnamed — category label only.
        RefreshPreview();
    }

    private void Randomize()
    {
        if (avatarSystem == null) return;
        var rng = new System.Random();
        avatarSystem.faceIndex = rng.Next(5);
        avatarSystem.skinToneIndex = rng.Next(5);
        avatarSystem.bodyTypeIndex = rng.Next(4);
        avatarSystem.eyeColorIndex = rng.Next(6);
        // Hair colour is per-scene; not randomized here.
        ShowCategory(_activeCategory);
    }

    private void ConfirmLook()
    {
        // Avatar is saved automatically; this just closes the creator.
        // Always changeable — the player can return anytime.
        gameObject.SetActive(false);
    }

    private void RefreshPreview()
    {
        // TODO: Update previewImage with the current avatar combination.
        // Uses the v3 face anchor styling for Face A.
    }
}
