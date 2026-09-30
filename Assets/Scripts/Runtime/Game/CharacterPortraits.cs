using System.Collections.Generic;
using UnityEngine;

namespace ScandalSeason.Runtime.Game
{
    /// <summary>
    /// Maps character names (as they appear in scene prose dialogue tags)
    /// to their locked portrait renders in Resources/Portraits.
    /// </summary>
    public static class CharacterPortraits
    {
        private static readonly Dictionary<string, string> _nameToPath = new Dictionary<string, string>
        {
            // Key: name as it appears after the em-dash in prose (case-insensitive match)
            // Value: Resources path (without extension)
            { "letitia", "Portraits/character-3d-letitia-hartwell-v1-88a12cbd" },
            { "julian", "Portraits/character-3d-lord-julian-ashcombe-v3-4ee230b3" },
            { "ashcombe", "Portraits/character-3d-lord-julian-ashcombe-v3-4ee230b3" },
            { "laurent", "Portraits/character-3d-comte-laurent-de-varenne-32293420" },
            { "varenne", "Portraits/character-3d-comte-laurent-de-varenne-32293420" },
            { "lavinia", "Portraits/character-3d-lavinia-crane-v2d-41c853ee" },
            { "augusta", "Portraits/character-3d-augusta-ravencroft-94e9b6ad" },
            { "dowager", "Portraits/character-3d-augusta-ravencroft-94e9b6ad" },
            { "cecilia", "Portraits/character-3d-cecilia-vane-v6-a7f2c91d" },
            { "vane", "Portraits/character-3d-cecilia-vane-v6-a7f2c91d" },
            { "bell", "Portraits/character-3d-nance-bell-v4-f209be39" },
            { "nance", "Portraits/character-3d-nance-bell-v4-f209be39" },
            { "elise", "Portraits/character-3d-elise-v3-c9034810" },
            { "quill", "Portraits/character-3d-quill-v5-84940c06" },
            { "lucien", "Portraits/character-3d-lucien-valcourt-v1-42c8b7a1" },
            { "valcourt", "Portraits/character-3d-lucien-valcourt-v1-42c8b7a1" },
            { "sloane", "Portraits/character-3d-verity-sloane-v2c-9b3e7c1a" },
            { "verity", "Portraits/character-3d-verity-sloane-v2c-9b3e7c1a" },
            { "hugh", "Portraits/character-3d-hugh-hartwell-v1-649383b9" },
            { "harrow", "Portraits/character-3d-james-harrow-v4-3312334a" },
            { "marquis", "Portraits/character-3d-marquis-saint-ange-v3-4acda44f" },
            { "saint-ange", "Portraits/character-3d-marquis-saint-ange-v3-4acda44f" },
        };

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Get the portrait sprite for a character name. Returns null if not found.
        /// Name matching is case-insensitive and matches against the dialogue tag name.
        /// Loads as Texture2D and creates a Sprite (avoids requiring Sprite import type).
        /// </summary>
        public static Sprite GetPortrait(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                return null;

            string key = characterName.ToLowerInvariant().Trim();

            // Check cache first
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            // Look up the resource path
            if (!_nameToPath.TryGetValue(key, out var path))
                return null;

            // Load as Texture2D (works regardless of import type)
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
                return null;

            // Create a Sprite from the texture
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
        /// Parse speaker names from scene prose. Looks for em-dash + Name patterns
        /// at the end of dialogue lines (e.g. '"Oh, my love" — Letitia').
        /// Returns unique speaker names in order of first appearance.
        /// </summary>
        public static List<string> ParseSpeakers(string prose)
        {
            var speakers = new List<string>();
            var seen = new HashSet<string>();

            if (string.IsNullOrEmpty(prose))
                return speakers;

            // Pattern: em-dash (U+2014) or hyphen followed by a capitalized name
            // The prose uses \u2014 (em-dash) before speaker names
            var matches = System.Text.RegularExpressions.Regex.Matches(
                prose,
                @"[\u2014\-]\s*([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)"
            );

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string name = m.Groups[1].Value.Trim();
                // Only include if we have a portrait for this character
                // (check first word for multi-word names like "Letitia Hartwell")
                string firstWord = name.Split(' ')[0];
                if (_nameToPath.ContainsKey(firstWord.ToLowerInvariant()) && !seen.Contains(firstWord))
                {
                    seen.Add(firstWord);
                    speakers.Add(firstWord);
                }
            }

            return speakers;
        }
    }
}
