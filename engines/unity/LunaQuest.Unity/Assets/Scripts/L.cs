using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>Shared UI language switch and the fonts the two adapters ship with.</summary>
    public static class L
    {
        public static bool Arabic;

        private static Font _latin;
        private static Font _arabic;

        public static Font LatinFont
        {
            get
            {
                if (_latin == null) Init();
                return _latin;
            }
        }

        public static Font ArabicFont
        {
            get
            {
                if (_arabic == null) Init();
                return _arabic;
            }
        }

        public static Font Font => Arabic ? ArabicFont : LatinFont;

        public static void Init()
        {
            if (_latin != null) return;
            _latin = Resources.Load<Font>("Fonts/DejaVuSans");
            if (_latin == null)
                _latin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _arabic = Resources.Load<Font>("Fonts/NotoNaskhArabic");
            if (_arabic == null)
                _arabic = _latin;
        }

        public static string T(string en, string ar) => Arabic ? ar : en;
    }
}
