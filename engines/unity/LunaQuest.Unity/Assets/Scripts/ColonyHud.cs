using System;
using System.Collections.Generic;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;
using LunaQuest.Core.Time;
using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// Unity HUD, drawn with IMGUI so the project has no UI package dependency and opens
    /// cleanly in a fresh Unity 6 install. It mirrors the Godot HUD: five-second glance
    /// values, a short colony log, and one-line prompts. The resource-math panel shows
    /// exactly how each kilogram and litre is tied to the second.
    /// </summary>
    public sealed class ColonyHud
    {
        private sealed class FeedItem
        {
            public SimEvent Event;
            public double ExpiresAt;
        }

        private readonly LunaQuestGame _game;
        private readonly List<FeedItem> _feed = new List<FeedItem>();

        private Texture2D _white;
        private GUIStyle _text;
        private GUIStyle _button;
        private GUIStyle _field;
        private bool _stylesReady;
        private bool _focusSet;

        private string _name = "Amara Okafor";
        private string _toast = "";
        private string _subtitle = "";
        private float _toastTimer;
        private float _subtitleTimer;

        private bool _journalOpen;
        private bool _helpOpen;
        private bool _mathOpen;
        private bool _firstPerson;
        private Vector2 _journalScroll;
        private float _baseWidth = 1600f;
        private float _baseHeight = 900f;

        public bool OverlayOpen => _journalOpen || _helpOpen || _mathOpen;

        public ColonyHud(LunaQuestGame game)
        {
            _game = game;
        }

        public void Init()
        {
            _name = _game.Sim != null ? _game.Sim.PlayerName : "Amara Okafor";
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _white.hideFlags = HideFlags.HideAndDontSave;
        }

        public void Tick(float dt)
        {
            if (_toastTimer > 0f) _toastTimer = Mathf.Max(0f, _toastTimer - dt);
            if (_subtitleTimer > 0f) _subtitleTimer = Mathf.Max(0f, _subtitleTimer - dt);

            double now = Time.unscaledTimeAsDouble;
            for (int i = _feed.Count - 1; i >= 0; i--)
                if (now >= _feed[i].ExpiresAt)
                    _feed.RemoveAt(i);
        }

        public void ShowToast(string text, float seconds = 3f)
        {
            _toast = text ?? "";
            _toastTimer = Mathf.Max(0.1f, seconds);
        }

        public void ShowSubtitle(string text, float seconds = 4f)
        {
            _subtitle = text ?? "";
            _subtitleTimer = Mathf.Max(0.1f, seconds);
        }

        public void PushEvent(SimEvent e)
        {
            if (e == null) return;
            _feed.Add(new FeedItem { Event = e, ExpiresAt = Time.unscaledTimeAsDouble + 90.0 });
            while (_feed.Count > 4) _feed.RemoveAt(0);
        }

        public void ToggleJournal()
        {
            _journalOpen = !_journalOpen;
            if (_journalOpen) { _helpOpen = false; _mathOpen = false; _journalScroll = Vector2.zero; }
        }

        public void ToggleHelp()
        {
            _helpOpen = !_helpOpen;
            if (_helpOpen) { _journalOpen = false; _mathOpen = false; }
        }

        public void ToggleMath()
        {
            _mathOpen = !_mathOpen;
            if (_mathOpen) { _journalOpen = false; _helpOpen = false; }
        }

        public void CloseOverlays()
        {
            _journalOpen = false;
            _helpOpen = false;
            _mathOpen = false;
        }

        public void OnLanguageChanged()
        {
            _journalScroll = Vector2.zero;
        }

        public void HideStart()
        {
            // The start screen is drawn from LunaQuestGame.Started, so nothing to hide.
        }

        public void SetFirstPerson(bool firstPerson)
        {
            _firstPerson = firstPerson;
        }

        // ---- drawing ---------------------------------------------------------

        public void Draw()
        {
            if (_game.Sim == null || DrawWhite() == null) return;

            EnsureStyles();

            float scale = Mathf.Max(0.55f, Screen.height / 900f);
            float w = Screen.width / scale;
            const float h = 900f;
            _baseWidth = w;
            _baseHeight = h;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (!_game.Started)
            {
                DrawStart(w, h);
                GUI.matrix = Matrix4x4.identity;
                return;
            }

            DrawVisor(w, h);
            DrawTopBar(w);
            DrawStatusPanel();
            DrawLogPanel(w);
            DrawPromptAndSubtitles(w, h);

            if (_journalOpen) DrawJournal(new Rect(w * 0.16f, 92f, w * 0.68f, h - 184f));
            if (_helpOpen) DrawHelp(new Rect(w * 0.26f, 140f, w * 0.48f, h - 280f));
            if (_mathOpen) DrawMath(new Rect(w - 620f, 86f, 604f, 650f));

            if (_toastTimer > 0f && !OverlayOpen)
            {
                float a = Mathf.Clamp01(_toastTimer / 0.8f);
                Text(new Rect(w * 0.5f - 330f, 70f, 660f, 44f), _toast, 17,
                    new Color(0.78f, 0.95f, 1f, a), TextAnchor.MiddleCenter, true, true);
            }

            if (_game.Paused) DrawPause(w, h);

            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawPause(float w, float h)
        {
            GUI.DrawTexture(new Rect(0f, 0f, w, h), DrawWhite(), ScaleMode.StretchToFill, true, 0f,
                new Color(0f, 0f, 0f, 0.55f), 0f, 0f);

            var panel = new Rect(w * 0.5f - 230f, h * 0.5f - 140f, 460f, 280f);
            Panel(panel, new Color(0.02f, 0.035f, 0.055f, 0.98f), new Color(0.35f, 0.75f, 0.95f, 0.7f));
            Text(new Rect(panel.x, panel.y + 30f, panel.width, 40f),
                L.T("PAUSED", "متوقف مؤقتًا"), 28, new Color(0.94f, 0.97f, 1f), TextAnchor.MiddleCenter, true);
            Text(new Rect(panel.x + 30f, panel.y + 86f, panel.width - 60f, 56f),
                L.T("The colony clock is stopped. Nothing is consumed while you are away.",
                    "ساعة المستعمرة متوقفة. لا يُستهلك شيء أثناء غيابك."),
                13, new Color(0.65f, 0.78f, 0.9f), TextAnchor.MiddleCenter, false, true);
            if (Button(new Rect(panel.x + 130f, panel.y + 158f, panel.width - 260f, 42f),
                    L.T("Resume", "استئناف"), 15))
                _game.ResumeGame();
            Text(new Rect(panel.x, panel.y + 212f, panel.width, 24f),
                L.T("Esc  resume", "Esc  استئناف"), 12,
                new Color(0.5f, 0.65f, 0.8f), TextAnchor.MiddleCenter);
        }

        private Texture2D DrawWhite()
        {
            if (_white != null) return _white;
            Init();
            return _white;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _text = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = false,
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _field = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
            };
            _stylesReady = true;
        }

        private void SyncFonts()
        {
            var font = L.Font;
            if (font == null) return;
            if (_text.font != font)
            {
                _text.font = font;
                _button.font = font;
                _field.font = font;
            }
        }

        private void Text(Rect rect, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
        {
            SyncFonts();
            _text.fontSize = size;
            _text.alignment = Rtl(anchor);
            _text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _text.wordWrap = wrap;
            _text.normal.textColor = color;
            GUI.Label(rect, value ?? "", _text);
        }

        private static TextAnchor Rtl(TextAnchor anchor)
        {
            if (!L.Arabic) return anchor;
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAnchor.UpperRight;
                case TextAnchor.MiddleLeft: return TextAnchor.MiddleRight;
                case TextAnchor.LowerLeft: return TextAnchor.LowerRight;
                default: return anchor;
            }
        }

        private bool Button(Rect rect, string label, int size = 14)
        {
            SyncFonts();
            _button.fontSize = size;
            return GUI.Button(rect, label, _button);
        }

        private void Panel(Rect rect, Color background, Color border)
        {
            GUI.DrawTexture(rect, _white, ScaleMode.StretchToFill, true, 0f, background, 0f, 0f);
            if (border.a <= 0.001f) return;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _white, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), _white, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), _white, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), _white, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
        }

        private void Bar(Rect rect, double fraction, Color fill, bool warn)
        {
            Panel(rect, new Color(0.07f, 0.09f, 0.12f, 0.95f), new Color(0f, 0f, 0f, 0f));
            float width = rect.width * Mathf.Clamp01((float)fraction);
            if (width > 0f)
                GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), _white,
                    ScaleMode.StretchToFill, true, 0f,
                    warn ? new Color(1f, 0.38f, 0.34f, 0.95f) : fill, 0f, 0f);
        }

        // ---- start screen ----------------------------------------------------

        private void DrawStart(float w, float h)
        {
            GUI.DrawTexture(new Rect(0f, 0f, w, h), _white, ScaleMode.StretchToFill, true, 0f,
                new Color(0.005f, 0.008f, 0.016f, 0.86f), 0f, 0f);

            var panel = new Rect(w * 0.5f - 330f, h * 0.5f - 260f, 660f, 520f);
            Panel(panel, new Color(0.035f, 0.06f, 0.095f, 0.98f), new Color(0.32f, 0.68f, 0.9f, 0.65f));

            Text(new Rect(panel.x + 30f, panel.y + 32f, panel.width - 60f, 52f),
                "LUNA-QUEST", 40, new Color(0.92f, 0.96f, 1f), TextAnchor.MiddleCenter, true);

            Text(new Rect(panel.x + 30f, panel.y + 88f, panel.width - 60f, 26f),
                L.T("The Living Moon Colony · Shackleton Rim",
                    "المستعمرة القمرية الحية · حافة شاكلتون"),
                16, new Color(0.6f, 0.78f, 0.94f), TextAnchor.MiddleCenter);

            Text(new Rect(panel.x + 48f, panel.y + 126f, panel.width - 96f, 44f),
                L.T("30 Earth days. Nine crew. Every kilogram and every second counted.",
                    "٣٠ يومًا أرضيًا. تسعة أفراد. كل كيلوغرام وكل ثانية محسوبة."),
                14, new Color(0.72f, 0.82f, 0.92f), TextAnchor.MiddleCenter, false, true);

            Text(new Rect(panel.x + 48f, panel.y + 192f, panel.width - 96f, 24f),
                L.T("How should the crew address you?", "كيف يناديك الطاقم؟"),
                14, new Color(0.82f, 0.9f, 1f));

            GUI.SetNextControlName("lq_name");
            _name = GUI.TextField(new Rect(panel.x + 48f, panel.y + 220f, panel.width - 96f, 40f),
                _name, 28, _field);

            if (!_focusSet)
            {
                GUI.FocusControl("lq_name");
                _focusSet = true;
            }

            float rowY = panel.y + 282f;
            if (Button(new Rect(panel.x + 96f, rowY, 140f, 34f), "English", 14))
                _game.SetLanguage(false);
            if (Button(new Rect(panel.x + 246f, rowY, 140f, 34f), "العربية", 14))
                _game.SetLanguage(true);

            if (Button(new Rect(panel.x + 48f, rowY + 56f, panel.width - 96f, 44f),
                    L.T("Begin the mission", "ابدأ المهمة"), 17))
                Begin();

            Text(new Rect(panel.x + 48f, rowY + 112f, panel.width - 96f, 22f),
                L.T("Your name enters the shared save file and appears in crew lines.",
                    "يُحفظ اسمك في ملف الحفظ المشترك ويظهر في كلام الطاقم."),
                12, new Color(0.52f, 0.64f, 0.77f), TextAnchor.MiddleCenter, false, true);

            Text(new Rect(panel.x + 40f, panel.yMax - 64f, panel.width - 80f, 40f),
                L.T("WASD move · mouse look · E use · V camera · J journal · G resource math · H help",
                    "WASD حركة · الفأرة نظر · E استخدام · V كاميرا · J دفتر · G حسابات الموارد · H مساعدة"),
                12, new Color(0.48f, 0.61f, 0.74f), TextAnchor.MiddleCenter, false, true);

            if (!string.IsNullOrEmpty(_game.ContentError))
                Text(new Rect(panel.x + 40f, panel.yMax - 104f, panel.width - 80f, 40f),
                    _game.ContentError, 12, new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter, false, true);

            if (Event.current != null && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                Begin();
        }

        private void Begin()
        {
            if (_game.Started) return;
            _game.BeginGame(_name);
        }

        // ---- top bar ---------------------------------------------------------

        private void DrawVisor(float w, float h)
        {
            if (!_firstPerson) return;

            var rim = new Color(0.01f, 0.025f, 0.04f, 0.66f);
            float band = Mathf.Max(16f, h * 0.03f);
            GUI.DrawTexture(new Rect(0f, 0f, w, band), DrawWhite(), ScaleMode.StretchToFill, true, 0f, rim, 0f, 0f);
            GUI.DrawTexture(new Rect(0f, h - band, w, band), DrawWhite(), ScaleMode.StretchToFill, true, 0f, rim, 0f, 0f);
            GUI.DrawTexture(new Rect(0f, 0f, band, h), DrawWhite(), ScaleMode.StretchToFill, true, 0f, rim, 0f, 0f);
            GUI.DrawTexture(new Rect(w - band, 0f, band, h), DrawWhite(), ScaleMode.StretchToFill, true, 0f, rim, 0f, 0f);

            float cx = w * 0.5f;
            float cy = h * 0.5f;
            var mark = new Color(0.72f, 0.95f, 1f, 0.82f);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), DrawWhite(), ScaleMode.StretchToFill, true, 0f, mark, 0f, 0f);
            GUI.DrawTexture(new Rect(cx - 13f, cy - 1f, 8f, 2f), DrawWhite(), ScaleMode.StretchToFill, true, 0f, mark, 0f, 0f);
            GUI.DrawTexture(new Rect(cx + 5f, cy - 1f, 8f, 2f), DrawWhite(), ScaleMode.StretchToFill, true, 0f, mark, 0f, 0f);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 13f, 2f, 8f), DrawWhite(), ScaleMode.StretchToFill, true, 0f, mark, 0f, 0f);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 5f, 2f, 8f), DrawWhite(), ScaleMode.StretchToFill, true, 0f, mark, 0f, 0f);
        }

        private void DrawTopBar(float w)
        {
            Panel(new Rect(0f, 0f, w, 58f), new Color(0.012f, 0.017f, 0.028f, 0.92f),
                new Color(0.22f, 0.36f, 0.5f, 0.55f));

            Text(new Rect(20f, 10f, 210f, 26f), "LUNA-QUEST", 20,
                new Color(0.9f, 0.95f, 1f), TextAnchor.MiddleLeft, true);
            Text(new Rect(118f, 30f, 210f, 16f), "The Living Moon Colony", 11,
                new Color(0.52f, 0.68f, 0.84f), TextAnchor.MiddleLeft);

            Text(new Rect(w * 0.5f - 230f, 20f, 460f, 18f),
                L.T("SHACKLETON RIM · 89.9° S · 9 CREW", "حافة شاكلتون · ٨٩.٩° ج · ٩ أفراد"),
                11, new Color(0.5f, 0.66f, 0.8f), TextAnchor.MiddleCenter);

            var sim = _game.Sim;
            Text(new Rect(w - 400f, 9f, 300f, 24f), sim.Clock.Stamp(), 16,
                new Color(0.88f, 0.94f, 1f), TextAnchor.MiddleRight);
            Bar(new Rect(w - 400f, 36f, 300f, 6f), sim.Clock.MissionProgress,
                new Color(0.3f, 0.8f, 1f, 0.9f), false);
            Text(new Rect(w - 400f, 44f, 300f, 12f),
                L.T($"30-day mission · {sim.Clock.MissionProgress * 100f:0}%",
                    $"مهمة ٣٠ يومًا · {sim.Clock.MissionProgress * 100f:0}%"),
                9, new Color(0.45f, 0.6f, 0.75f), TextAnchor.MiddleRight);

            if (Button(new Rect(w - 86f, 12f, 66f, 30f), L.Arabic ? "EN" : "العربية", 12))
                _game.ToggleLanguage();
        }

        // ---- status ----------------------------------------------------------

        private void DrawStatusPanel()
        {
            var sim = _game.Sim;
            var s = sim.State;
            var panel = new Rect(16f, 68f, 310f, 402f);
            Panel(panel, new Color(0.025f, 0.043f, 0.068f, 0.88f),
                new Color(0.2f, 0.35f, 0.5f, 0.45f));

            Text(new Rect(panel.x + 14f, panel.y + 12f, 240f, 18f),
                L.T("COLONY STATUS", "حالة المستعمرة"), 12,
                new Color(0.55f, 0.72f, 0.9f), TextAnchor.MiddleLeft, true);

            float y = panel.y + 42f;
            Row("PWR", L.T("PWR", "طاقة"), s.BatteryPercent, 1.0, new Color(1f, 0.78f, 0.32f),
                $"{s.BatteryPercent * 100:0}% · {s.SolarKw:0.0} kW", s.BatteryPercent < 0.25, ref y);
            Row("WTR", L.T("WTR", "ماء"), s.WaterPercent, 1.0, new Color(0.32f, 0.62f, 1f),
                $"{s.WaterL:0} / {s.WaterCapacityL:0} L", s.WaterPercent < 0.2, ref y);
            Row("O2", L.T("O2", "أكسجين"), s.O2Percent, 1.0, new Color(0.35f, 0.9f, 0.95f),
                $"{s.O2Percent * 100:0}% · {s.O2Kg:0} kg", s.O2Percent < 0.35, ref y);
            Row("CO2", "CO2", s.Co2Ppm / 10000.0, 1.0, new Color(0.75f, 0.5f, 0.95f),
                $"{s.Co2Ppm:0} ppm", s.Co2Ppm > 4000, ref y);
            Row("TEMP", L.T("TEMP", "حرارة"), (s.CabinTempC + 20.0) / 60.0, 1.0,
                new Color(1f, 0.55f, 0.3f), $"{s.CabinTempC:0.0} °C",
                s.CabinTempC < 18 || s.CabinTempC > 27, ref y);
            Row("FOOD", L.T("FOOD", "غذاء"), s.FoodPercent, 1.0, new Color(0.45f, 0.9f, 0.45f),
                $"{s.FoodPercent * 100:0}%", s.FoodPercent < 0.25, ref y);
            Row("DOSE", L.T("DOSE", "جرعة"), s.DoseRateMicroGyH / 200.0, 1.0,
                new Color(1f, 0.35f, 0.35f), $"{s.DoseRateMicroGyH:0.0} µGy/h",
                s.SolarParticleEvent, ref y);
            Row("LINK", L.T("LINK", "رابط"), s.SignalQuality, 1.0, new Color(0.35f, 0.9f, 0.7f),
                $"{s.SignalQuality * 100:0}%", s.SignalQuality < 0.5, ref y);

            Text(new Rect(panel.x + 14f, panel.yMax - 34f, panel.width - 28f, 22f),
                L.T("G  resource math   ·   J  journal", "G  حسابات الموارد   ·   J  السجل"),
                11, new Color(0.45f, 0.6f, 0.75f), TextAnchor.MiddleLeft);
        }

        private void Row(string key, string label, double value, double max, Color color,
            string reading, bool warn, ref float y)
        {
            var panel = new Rect(16f, y - 2f, 310f, 31f);
            Text(new Rect(panel.x + 14f, panel.y, 58f, 14f), label, 11,
                new Color(0.72f, 0.83f, 0.93f), TextAnchor.MiddleLeft);
            Text(new Rect(panel.x + 150f, panel.y, 146f, 14f), reading, 11,
                warn ? new Color(1f, 0.45f, 0.4f) : new Color(0.82f, 0.9f, 1f),
                TextAnchor.MiddleRight);
            Bar(new Rect(panel.x + 14f, panel.y + 16f, panel.width - 28f, 7f),
                value / Math.Max(0.0001, max), color, warn);
            y += 33f;
        }

        // ---- log -------------------------------------------------------------

        private void DrawLogPanel(float w)
        {
            var panel = new Rect(w - 390f, 68f, 374f, 402f);
            Panel(panel, new Color(0.025f, 0.043f, 0.068f, 0.8f),
                new Color(0.2f, 0.35f, 0.5f, 0.4f));

            Text(new Rect(panel.x + 14f, panel.y + 12f, 240f, 18f),
                L.T("COLONY LOG", "سجل المستعمرة"), 12,
                new Color(0.55f, 0.72f, 0.9f), TextAnchor.MiddleLeft, true);

            if (_feed.Count == 0)
            {
                var brief = new Rect(panel.x + 14f, panel.y + 38f, panel.width - 28f, 100f);
                Panel(brief, new Color(0.05f, 0.09f, 0.12f, 0.92f),
                    new Color(0.3f, 0.65f, 0.8f, 0.45f));
                Text(new Rect(brief.x + 10f, brief.y + 8f, brief.width - 20f, 16f),
                    L.T("FIRST WATCH · SOL 1", "الوردية الأولى · اليوم ١"), 11,
                    new Color(0.5f, 0.9f, 1f), TextAnchor.MiddleLeft, true);
                Text(new Rect(brief.x + 10f, brief.y + 28f, brief.width - 20f, 64f),
                    L.T("Walk the colony. At every console press E and read the number before you change it. Airlock, power, water, air.",
                        "امشِ في المستعمرة. عند كل وحدة اضغط E واقرأ الرقم قبل أن تغيّره. بوابة الهواء، الطاقة، الماء، الهواء."),
                    11, new Color(0.72f, 0.82f, 0.92f), TextAnchor.UpperLeft, false, true);
            }
            else
            {
                float y = panel.y + 38f;
                for (int i = _feed.Count - 1; i >= 0; i--)
                {
                    var item = _feed[i];
                    bool body = item.Event.Kind == SimEventKind.Warning ||
                                item.Event.Kind == SimEventKind.Critical ||
                                item.Event.Kind == SimEventKind.Mission ||
                                item.Event.Kind == SimEventKind.Discovery;
                    float height = body ? 62f : 38f;
                    if (y + height > panel.yMax - 34f) break;

                    var card = new Rect(panel.x + 14f, y, panel.width - 28f, height);
                    Panel(card, new Color(0.02f, 0.035f, 0.055f, 0.94f), KindColor(item.Event.Kind, 0.5f));
                    Text(new Rect(card.x + 9f, card.y + 5f, card.width - 18f, 16f),
                        L.T(item.Event.TitleEn, item.Event.TitleAr), 12,
                        KindColor(item.Event.Kind, 1f), TextAnchor.MiddleLeft, true);
                    if (body)
                        Text(new Rect(card.x + 9f, card.y + 23f, card.width - 18f, 34f),
                            L.T(item.Event.BodyEn, item.Event.BodyAr), 10,
                            new Color(0.76f, 0.85f, 0.94f), TextAnchor.UpperLeft, false, true);
                    y += height + 6f;
                }
            }

            Text(new Rect(panel.x + 14f, panel.yMax - 30f, panel.width - 28f, 20f),
                L.T("H  controls   ·   F5 save   ·   F9 load", "H  التحكم   ·   F5 حفظ   ·   F9 تحميل"),
                10, new Color(0.45f, 0.6f, 0.75f), TextAnchor.MiddleLeft);
        }

        private static Color KindColor(SimEventKind kind, float alpha)
        {
            switch (kind)
            {
                case SimEventKind.Critical: return new Color(1f, 0.38f, 0.34f, alpha);
                case SimEventKind.Warning: return new Color(1f, 0.72f, 0.3f, alpha);
                case SimEventKind.Mission: return new Color(0.4f, 0.85f, 1f, alpha);
                case SimEventKind.Discovery: return new Color(0.55f, 0.95f, 0.6f, alpha);
                default: return new Color(0.62f, 0.8f, 0.95f, alpha);
            }
        }

        // ---- prompts ---------------------------------------------------------

        private void DrawPromptAndSubtitles(float w, float h)
        {
            if (!OverlayOpen)
            {
                string prompt = _game.Player != null ? _game.Player.FocusPrompt : "";
                if (!string.IsNullOrEmpty(prompt))
                {
                    var rect = new Rect(w * 0.5f - 280f, h - 132f, 560f, 42f);
                    Panel(rect, new Color(0.05f, 0.09f, 0.13f, 0.94f),
                        new Color(0.35f, 0.8f, 0.95f, 0.8f));
                    Text(rect, "E   " + prompt, 15, new Color(0.85f, 0.96f, 1f),
                        TextAnchor.MiddleCenter, false, true);
                }
            }

            if (_subtitleTimer > 0f)
            {
                float a = Mathf.Clamp01(_subtitleTimer / 0.8f);
                var rect = new Rect(w * 0.14f, h - 84f, w * 0.72f, 54f);
                Text(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height),
                    _subtitle, 16, new Color(0f, 0f, 0f, a * 0.85f),
                    TextAnchor.MiddleCenter, false, true);
                Text(rect, _subtitle, 16, new Color(1f, 1f, 1f, a),
                    TextAnchor.MiddleCenter, false, true);
            }
        }

        // ---- overlays --------------------------------------------------------

        private void OverlayBase(Rect rect, string titleEn, string titleAr)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _baseWidth, _baseHeight), _white,
                ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.55f), 0f, 0f);
            Panel(rect, new Color(0.025f, 0.045f, 0.07f, 0.97f),
                new Color(0.35f, 0.7f, 0.9f, 0.7f));
            Text(new Rect(rect.x + 20f, rect.y + 14f, rect.width - 130f, 30f),
                L.T(titleEn, titleAr), 22, new Color(0.9f, 0.96f, 1f),
                TextAnchor.MiddleLeft, true);
            if (Button(new Rect(rect.xMax - 104f, rect.y + 14f, 84f, 28f),
                    L.T("close", "إغلاق"), 12))
                CloseOverlays();
            Text(new Rect(rect.x + 20f, rect.yMax - 28f, rect.width - 40f, 18f),
                L.T("Esc closes · the colony keeps running while you read",
                    "Esc يغلق · تستمر المستعمرة أثناء القراءة"),
                10, new Color(0.45f, 0.6f, 0.75f), TextAnchor.MiddleLeft);
        }

        private void DrawJournal(Rect rect)
        {
            OverlayBase(rect, "FIELD JOURNAL", "دفتر الميدان");
            Text(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 20f),
                L.T("Evidence of work done, not a quiz score.", "دليل على عمل أُنجز، لا نتيجة اختبار."),
                11, new Color(0.55f, 0.7f, 0.84f));

            var entries = _game.Sim.Learning.Journal;
            var view = new Rect(rect.x + 20f, rect.y + 74f, rect.width - 40f, rect.height - 116f);

            if (entries.Count == 0)
            {
                Text(view, L.T("Nothing logged yet. Do the work, then read it here.",
                        "لا شيء مسجل بعد. اعمل أولًا ثم اقرأ هنا."),
                    14, new Color(0.65f, 0.78f, 0.9f), TextAnchor.UpperLeft, false, true);
            }
            else
            {
                const float cardH = 122f;
                var content = new Rect(0f, 0f, view.width - 22f, entries.Count * (cardH + 8f));
                _journalScroll = GUI.BeginScrollView(view, _journalScroll, content, false, true);
                float y = 0f;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var card = new Rect(0f, y, content.width, cardH);
                    Panel(card, new Color(0.05f, 0.08f, 0.12f, 0.97f),
                        new Color(0.3f, 0.55f, 0.75f, 0.5f));
                    Text(new Rect(card.x + 12f, card.y + 8f, card.width - 24f, 22f),
                        L.T(entry.TitleEn, entry.TitleAr), 15,
                        new Color(0.86f, 0.95f, 1f), TextAnchor.MiddleLeft, true);
                    Text(new Rect(card.x + 12f, card.y + 30f, card.width - 24f, 58f),
                        L.T(entry.BodyEn, entry.BodyAr), 11,
                        new Color(0.74f, 0.84f, 0.93f), TextAnchor.UpperLeft, false, true);
                    Text(new Rect(card.x + 12f, card.y + 94f, card.width - 24f, 18f),
                        $"{(L.Arabic ? "دليل" : "evidence")} {entry.EvidenceClass} · {entry.Skill} · {entry.Source}",
                        9, new Color(0.45f, 0.6f, 0.72f), TextAnchor.MiddleLeft);
                    y += cardH + 8f;
                }
                GUI.EndScrollView();
            }
        }

        private void DrawHelp(Rect rect)
        {
            OverlayBase(rect, "CONTROLS", "التحكم");
            string[] lines =
            {
                L.T("WASD  move · Shift  run · Space  jump", "WASD  حركة · Shift  جري · Space  قفز"),
                L.T("Mouse  look · V  first / third person", "الفأرة  النظر · V  منظور أول / ثالث"),
                L.T("E  use the station you are facing", "E  استخدام المحطة المقابلة"),
                L.T("J  journal · G  resource math · H  this panel", "J  الدفتر · G  حسابات الموارد · H  هذه اللوحة"),
                L.T("F5  save · F9  load · L  language", "F5  حفظ · F9  تحميل · L  اللغة"),
                L.T("Esc  release the mouse / close a panel", "Esc  تحرير الفأرة / إغلاق لوحة"),
            };
            float y = rect.y + 70f;
            foreach (var line in lines)
            {
                Text(new Rect(rect.x + 24f, y, rect.width - 48f, 24f), line, 14,
                    new Color(0.8f, 0.89f, 0.97f), TextAnchor.MiddleLeft);
                y += 30f;
            }

            y += 14f;
            Text(new Rect(rect.x + 24f, y, rect.width - 48f, 22f),
                L.T("Aim", "الهدف"), 14, new Color(0.55f, 0.85f, 1f),
                TextAnchor.MiddleLeft, true);
            y += 26f;
            Text(new Rect(rect.x + 24f, y, rect.width - 48f, rect.height - (y - rect.y) - 50f),
                L.T("Keep the crew alive with measured numbers, not guesses. The airlock is the only way outside. Plants grow indoors because the surface has no atmosphere and no shielding. Read each console before you touch it.",
                    "أبقِ الطاقم حيًا بأرقام مقاسة، لا بالتخمين. بوابة الهواء هي الطريق الوحيد للخارج. تنمو النباتات في الداخل لأن السطح بلا غلاف جوي وبلا حماية. اقرأ كل وحدة قبل أن تلمسها."),
                12, new Color(0.7f, 0.8f, 0.9f), TextAnchor.UpperLeft, false, true);
        }

        private void DrawMath(Rect rect)
        {
            OverlayBase(rect, "RESOURCE MATH", "حسابات الموارد");

            var s = _game.Sim.State;
            var c = _game.Sim.Clock;
            double crew = s.CrewCount;
            double dayFraction = GameClock.MinutesPerSecond / GameClock.MinutesPerDay; // game days per real second

            double o2Day = AirModel.O2KgPerPersonDay * crew;
            double co2Day = AirModel.CO2KgPerPersonDay * crew;
            double waterDay = WaterModel.LitresPerPersonDay * crew;
            double waterNetDay = WaterModel.NetLossLitresPerDay(s);
            double foodDay = FoodModel.KcalPerPersonDay * crew;
            double load = s.BaseLoadKw + s.HeaterKw + (s.O2GeneratorRunning ? s.O2GeneratorKw : 0)
                          + (s.GrowLightsOn ? 2.0 : 0);
            double powerDay = load * 24.0;
            double powerSecond = load * (GameClock.MinutesPerSecond / 60.0);
            double cropWaterDay = CropModel.PodAreaM2 * CropModel.WaterLPerM2Day;
            double cropCo2Day = CropModel.PodAreaM2 * CropModel.Co2KgPerM2Day;
            double harvest = CropModel.HarvestKcalPerM2 * CropModel.PodAreaM2;
            double doseDay = s.DoseRateMicroGyH * 24.0 / 1000.0;

            float y = rect.y + 56f;
            void Line(string text, Color color, int size = 12, float step = 20f)
            {
                Text(new Rect(rect.x + 20f, y, rect.width - 40f, step + 2f), text, size,
                    color, TextAnchor.MiddleLeft, false, true);
                y += step;
            }

            Line(L.T("CLOCK · the whole sim is tied to real seconds",
                    "الساعة · كل المحاكاة مربوطة بالثواني الحقيقية"),
                new Color(0.55f, 0.88f, 1f), 12, 22f);
            Line(L.T($"12 game-min per real second · 1 game day = 120 real s · 30 days = 60 real min",
                    $"١٢ دقيقة لعب لكل ثانية حقيقية · يوم اللعب = ١٢٠ ثانية · ٣٠ يومًا = ٦٠ دقيقة"),
                new Color(0.68f, 0.8f, 0.92f), 11, 26f);

            Line(L.T("CREW · nine people, per Earth day and per real second",
                    "الطاقم · تسعة أفراد، لكل يوم أرضي ولكل ثانية حقيقية"),
                new Color(0.55f, 0.95f, 0.65f), 12, 22f);
            Line(L.T($"oxygen  {o2Day:0.00} kg/day  →  {o2Day * dayFraction:0.0000} kg/s",
                    $"أكسجين  {o2Day:0.00} كغ/يوم  →  {o2Day * dayFraction:0.0000} كغ/ث"),
                new Color(0.82f, 0.9f, 1f), 11);
            Line(L.T($"CO2 out {co2Day:0.00} kg/day  →  {co2Day * dayFraction:0.0000} kg/s",
                    $"CO2 خارج  {co2Day:0.00} كغ/يوم  →  {co2Day * dayFraction:0.0000} كغ/ث"),
                new Color(0.82f, 0.9f, 1f), 11);
            Line(L.T($"water   {waterDay:0} L/day gross · net loss {waterNetDay:0.00} L/day ({waterNetDay * dayFraction:0.0000} L/s)",
                    $"ماء  {waterDay:0} لتر/يوم إجمالي · الفقد الصافي {waterNetDay:0.00} لتر/يوم ({waterNetDay * dayFraction:0.0000} لتر/ث)"),
                new Color(0.82f, 0.9f, 1f), 11);
            Line(L.T($"food    {foodDay:0} kcal/day  →  {foodDay * dayFraction:0} kcal/s",
                    $"غذاء  {foodDay:0} سعرة/يوم  →  {foodDay * dayFraction:0} سعرة/ث"),
                new Color(0.82f, 0.9f, 1f), 11);
            Line(L.T($"dose    {s.DoseRateMicroGyH:0.0} µGy/h · {doseDay:0.000} mSv/day",
                    $"جرعة  {s.DoseRateMicroGyH:0.0} ميكروغراي/ساعة · {doseDay:0.000} ملي سيفرت/يوم"),
                new Color(0.82f, 0.9f, 1f), 11, 26f);

            Line(L.T("POWER · habitat load, never off",
                    "الطاقة · حمل الموئل، لا ينطفئ"),
                new Color(1f, 0.78f, 0.32f), 12, 22f);
            Line(L.T($"load {load:0.0} kW · {powerDay:0} kWh/day · {powerSecond:0.00} kWh per real second",
                    $"الحمل {load:0.0} كيلوواط · {powerDay:0} كيلوواط·ساعة/يوم · {powerSecond:0.00} كيلوواط·ساعة/ث"),
                new Color(0.9f, 0.86f, 0.72f), 11);
            Line(L.T($"battery {s.BatteryKwh:0}/{s.BatteryCapacityKwh:0} kWh · array {s.SolarKw:0.0} kW now",
                    $"البطارية {s.BatteryKwh:0}/{s.BatteryCapacityKwh:0} كيلوواط·ساعة · المصفوفة {s.SolarKw:0.0} كيلوواط الآن"),
                new Color(0.9f, 0.86f, 0.72f), 11, 26f);

            Line(L.T("PLANTS · sealed grow pod, no direct Sun",
                    "النباتات · وحدة نمو مغلقة، بلا شمس مباشرة"),
                new Color(0.55f, 0.95f, 0.65f), 12, 22f);
            Line(L.T($"light   {(s.GrowLightsOn ? "2.0" : "0.0")} kW from colony power · air {cropCo2Day:0.00} kg CO2/day from cabin",
                    $"الضوء  {(s.GrowLightsOn ? "2.0" : "0.0")} كيلوواط من طاقة المستعمرة · هواء {cropCo2Day:0.00} كغ CO2/يوم من المقصورة"),
                new Color(0.78f, 0.94f, 0.82f), 11);
            Line(L.T($"water   {cropWaterDay:0} L/day from the tank · 90% recovered by condensate",
                    $"ماء  {cropWaterDay:0} لتر/يوم من الخزان · ٩٠٪ يُستعاد بالتكاثف"),
                new Color(0.78f, 0.94f, 0.82f), 11);
            Line(L.T($"food    +{harvest:0} kcal per {CropModel.CycleDays:0}-day cycle · crop {CropModel.Stage(s)} · health {s.CropHealth * 100:0}%",
                    $"غذاء  +{harvest:0} سعرة لكل دورة {CropModel.CycleDays:0} يومًا · المحصول {CropModel.Stage(s)} · الصحة {s.CropHealth * 100:0}%"),
                new Color(0.78f, 0.94f, 0.82f), 11, 26f);

            Line(L.T($"day {c.Day} of 30 · lunar phase {c.LunarPhase * 100:0}% · Sun elevation {c.SunElevationDeg:0.00}°",
                    $"اليوم {c.Day} من ٣٠ · طور القمر {c.LunarPhase * 100:0}% · ارتفاع الشمس {c.SunElevationDeg:0.00}°"),
                new Color(0.55f, 0.72f, 0.88f), 10, 22f);

            Text(new Rect(rect.x + 20f, rect.yMax - 34f, rect.width - 40f, 16f),
                L.T("Every engine step consumes the same numbers; the clock only changes presentation.",
                    "كل خطوة محاكاة تستهلك الأرقام نفسها؛ الساعة تغيّر العرض فقط."),
                9, new Color(0.45f, 0.6f, 0.72f), TextAnchor.MiddleLeft, false, true);
        }
    }
}
