using Godot;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Bilingual helper. Arabic is the design language of the source GDD; English is the
    /// default for the shared code comments and for players who do not read Arabic.
    /// </summary>
    public static class L
    {
        public static bool Arabic;

        public static string T(string en, string ar) => Arabic ? ar : en;

        public static Font? ArabicFont;
        public static Font? LatinFont;

        public static Font FontFor(string text)
            => Arabic && ArabicFont != null ? ArabicFont : (LatinFont ?? ThemeDB.FallbackFont);
    }
}
