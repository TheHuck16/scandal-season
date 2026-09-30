using UnityEngine;
using UnityEngine.UI;

namespace ScandalSeason.Runtime.Game
{
    /// <summary>
    /// Provides a working Font for dynamically-created Text components.
    /// Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") returns null
    /// in Unity 6 WebGL builds, causing all dynamic Text to be invisible.
    /// This helper falls back to finding a font from existing scene Text.
    /// </summary>
    public static class UIFontHelper
    {
        private static Font _cachedFont;

        public static Font GetFont()
        {
            if (_cachedFont != null)
                return _cachedFont;

            // Try the builtin resource first (works in Editor, may fail in WebGL)
            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_cachedFont != null)
                return _cachedFont;

            // Fallback: find a Text component in the scene with a valid font
            var texts = Object.FindObjectsOfType<Text>();
            foreach (var t in texts)
            {
                if (t != null && t.font != null)
                {
                    _cachedFont = t.font;
                    return _cachedFont;
                }
            }

            return null;
        }

        /// <summary>
        /// Assign a working font to a Text component if it doesn't have one.
        /// </summary>
        public static void EnsureFont(Text text)
        {
            if (text == null || text.font != null)
                return;
            text.font = GetFont();
        }
    }
}
