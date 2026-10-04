using System.Collections.Generic;
using UnityEngine;

namespace ScandalSeason.Runtime.Game
{
    /// <summary>
    /// Maps character names (as they appear in scene prose dialogue tags)
    /// to their approved identity plates in Resources/Plates.
    /// Plates are full-figure period engraved plates on aged paper, captioned
    /// "Name — Epithet" with the "J. BELL — LONDON" imprint. They are for
    /// collectibles and cut screens only — never the in-story portrait slot.
    /// </summary>
    public static class CharacterPlates
    {
        private static readonly Dictionary<string, string> _nameToPath = new Dictionary<string, string>
        {
            // Key: name as it appears after the em-dash in prose (case-insensitive match)
            // Value: Resources path (without extension)
            { "cecilia", "Plates/character-plate-cecilia-vane-v1-329c8262" },
            { "vane", "Plates/character-plate-cecilia-vane-v1-329c8262" },
            { "nance", "Plates/character-plate-nance-bell-v2-c86e305d" },
            { "bell", "Plates/character-plate-nance-bell-v2-c86e305d" },
            { "quill", "Plates/character-plate-quill-v1-3945bf4c" },
            { "lucien", "Plates/character-plate-lucien-valcourt-v1-799c4fe0" },
            { "valcourt", "Plates/character-plate-lucien-valcourt-v1-799c4fe0" },
            { "verity", "Plates/character-plate-verity-sloane-v2-7c4a2f1d" },
            { "sloane", "Plates/character-plate-verity-sloane-v2-7c4a2f1d" },
            { "hugh", "Plates/character-plate-hugh-hartwell-v1-8ea68fb6" },
            { "hartwell", "Plates/character-plate-hugh-hartwell-v1-8ea68fb6" },
            { "james", "Plates/character-plate-james-harrow-v1-7ddfac6c" },
            { "harrow", "Plates/character-plate-james-harrow-v1-7ddfac6c" },
            { "marquis", "Plates/character-plate-marquis-saint-ange-v1-6c30006f" },
            { "saint-ange", "Plates/character-plate-marquis-saint-ange-v1-6c30006f" },
            { "henry", "Plates/character-plate-henry-beaumont-v1-08ee8323" },
            { "beaumont", "Plates/character-plate-henry-beaumont-v1-08ee8323" },
            { "elise", "Plates/character-plate-elise-v1-6688a966" },
            { "lavinia", "Plates/character-plate-lavinia-crane-v2-2cbf2765" },
            { "crane", "Plates/character-plate-lavinia-crane-v2-2cbf2765" },
            { "letitia", "Plates/character-plate-letitia-hartwell-v6-a8beccda" },
        };

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Get the identity plate sprite for a character name. Returns null if not found.
        /// Name matching is case-insensitive. Loads as Texture2D and creates a Sprite
        /// (avoids requiring Sprite import type).
        /// </summary>
        public static Sprite GetPlate(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                return null;

            string key = characterName.ToLowerInvariant().Trim();

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            if (!_nameToPath.TryGetValue(key, out var path))
                return null;

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
                return null;

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// True when an approved identity plate exists for the character name.
        /// </summary>
        public static bool HasPlate(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                return false;
            return _nameToPath.ContainsKey(characterName.ToLowerInvariant().Trim());
        }
    }
}
