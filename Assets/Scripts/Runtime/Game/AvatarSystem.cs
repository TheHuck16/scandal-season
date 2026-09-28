// Scandal Season — Rendering layer.
// AvatarSystem: player avatar from 3 independent axes (APPROVED Sep 28, 2026).
// - 5 face structures (Face A = canon Rose, the default)
// - 5 skin tones (independent of face)
// - 4 body types, same height (every garment fits all four)
// - Hair color + hairstyle are per-scene player choices (HairPaletteSO)
// Changeable anytime, never locked or gated. The FULL beauty suite
// (hair color, eye color, eyebrows, hairstyle, all makeup) is per-scene.

using UnityEngine;

public sealed class AvatarSystem : MonoBehaviour
{
    [Header("Avatar axes (APPROVED Sep 28, 2026)")]
    public FaceStructureSO faceStructures;
    public SkinToneSO skinTones;
    public BodyTypeSO bodyTypes;
    public HairPaletteSO hairPalette;

    [Header("Current avatar (player-chosen, changeable anytime)")]
    public int faceIndex = 0; // 0 = Face A (the default)
    public int skinToneIndex = 0;
    public int bodyTypeIndex = 1; // 1 = Classic (default)

    public FaceStructureSO.FaceStructure CurrentFace =>
        faceStructures != null && faceIndex >= 0 && faceIndex < faceStructures.structures.Length
            ? faceStructures.structures[faceIndex] : null;

    public SkinToneSO.SkinTone CurrentSkinTone =>
        skinTones != null && skinToneIndex >= 0 && skinToneIndex < skinTones.tones.Length
            ? skinTones.tones[skinToneIndex] : null;

    public BodyTypeSO.BodyType CurrentBodyType =>
        bodyTypes != null && bodyTypeIndex >= 0 && bodyTypeIndex < bodyTypes.types.Length
            ? bodyTypes.types[bodyTypeIndex] : null;

    /// <summary>Resets to the default avatar (Face A). Rose looks like what the player chooses; this is just the starting point.</summary>
    public void ResetToDefault()
    {
        faceIndex = 0;
        skinToneIndex = 0;
        bodyTypeIndex = 1;
    }

    /// <summary>Validates indices are in range. Returns false if any axis is unset.</summary>
    public bool IsValid()
    {
        return CurrentFace != null && CurrentSkinTone != null && CurrentBodyType != null;
    }
}
