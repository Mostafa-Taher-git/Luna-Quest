using System;
using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Diegetic-leaning HUD: bars for the five life-support numbers, a short colony log, and
    /// one-line prompts. The full rules live on physical consoles and in the journal, not in a
    /// permanent wall of text.
    /// </summary>
    public partial class Hud : CanvasLayer
    {
        private Main _main = null!;
        private Control _root = null!;
        private Label _day = null!, _mission = null!, _prompt = null!, _subtitle = null!, _toast = null!, _sector = null!;
        private Label _feedTitle = null!, _journalTitle = null!, _helpTitle = null!, _plantsTitle = null!;
        private Label _startTitle = null!, _startSub = null!, _nameLabel = null!, _startHint = null!, _startControls = null!;
        private Label _dialogueName = null!, _dialogueRole = null!, _dialogueGreeting = null!, _dialogueLine = null!;
        private Label _dialogueSkills = null!, _dialogueResp = null!;
        private ProgressBar _missionBar = null!;
        private VBoxContainer _feed = null!, _journalList = null!, _plantsBox = null!, _dialogueTopics = null!;
        private PanelContainer _promptPanel = null!, _startOverlay = null!, _journalPanel = null!, _helpPanel = null!, _plantsPanel = null!, _pausePanel = null!, _dialoguePanel = null!, _settingsPanel = null!;
        private VisorOverlay _visor = null!;
        private ColorRect _pauseDim = null!;
        private LineEdit _nameEdit = null!;
        private Button _begin = null!, _lang = null!, _resume = null!, _dialogueLeave = null!;
        private NpcProfile? _dialogueNpc;
        private DialogueTopic? _dialogueCurrent;
        private readonly List<DialogueTopic> _dialogueList = new List<DialogueTopic>();
        private readonly Dictionary<string, ProgressBar> _bars = new Dictionary<string, ProgressBar>();
        private readonly Dictionary<string, Label> _vals = new Dictionary<string, Label>();
        private readonly Dictionary<Label, (string en, string ar)> _texts = new Dictionary<Label, (string en, string ar)>();
        private readonly List<(PanelContainer node, double life)> _feedItems = new List<(PanelContainer, double)>();
        private double _subtitleTimer;
        private bool _missionShown;

        // ---- shift task board ------------------------------------------------
        private PanelContainer _boardPanel = null!;
        private VBoxContainer _boardBox = null!;
        private Label _boardHeader = null!;
        private readonly List<Label> _boardRows = new List<Label>();
        private string _boardShift = "";

        /// <summary>A finished row reads as a calm teal rather than a highlight: a done task is
        /// settled, not urgent. Same teal as the other two engines.</summary>
        public static readonly Color DoneColor = new Color(0.45f, 0.80f, 0.72f);
        private static readonly Color RowColor = new Color(0.82f, 0.90f, 1f);
        private static readonly Color RowHeadColor = new Color(0.55f, 0.72f, 0.90f);

        /// <summary>
        /// One row of the shift task board. <c>Kind</c> says how done is read (a journal entry,
        /// one of the rover's own milestones, or the mission line), <c>Shift</c> says where the
        /// row leads. The eleven objectives match content/learning.json exactly; learning.json
        /// itself stays at 11 objectives — the board is 14 rows.
        /// </summary>
        public readonly struct BoardRowSpec
        {
            public readonly string Id, Kind, Shift, En, Ar, Hint;
            public BoardRowSpec(string id, string kind, string shift, string en, string ar, string hint)
            {
                Id = id; Kind = kind; Shift = shift; En = en; Ar = ar; Hint = hint;
            }
        }

        /// <summary>Which shift the clock is in, with its bilingual header.</summary>
        public readonly struct ShiftSpec
        {
            public readonly string Id, En, Ar;
            public ShiftSpec(string id, string en, string ar) { Id = id; En = en; Ar = ar; }
        }

        /// <summary>
        /// The shift task board — 14 rows. Eleven are the learning objectives from
        /// content/learning.json (done = a journal entry), two are the crew rover's own
        /// milestones, and one is the mission line. Ported from the browser edition's
        /// archive/web/src/ui/hud.js taskList(); wording identical to the GDScript edition's hud.gd.
        /// </summary>
        public static readonly BoardRowSpec[] BoardRows =
        {
            new BoardRowSpec("obj_power_budget", "journal", "morning",
                "Read the colony board in Mission Control", "اقرأ لوحة المستعمرة في مركز التحكم",
                "E at the board · Mission Control"),
            new BoardRowSpec("obj_solar_dust", "journal", "morning",
                "Clean the solar array when dust cuts output", "نظّف الألواح الشمسية",
                "Power farm"),
            new BoardRowSpec("obj_water_filter", "journal", "morning",
                "Check the water loop and back-flush the filter", "افحص الماء والفلتر",
                "Life support"),
            new BoardRowSpec("obj_sample", "journal", "afternoon",
                "Collect a regolith sample at the ice site", "اجمع عينة من التربة في موقع الجليد",
                "Ice site · sampler console"),
            new BoardRowSpec("obj_rover_take", "rover_driven", "afternoon",
                "Take the crew rover from the garage apron", "خذ مركبة الطاقم من أرضية المرآب",
                "Garage · E to drive"),
            new BoardRowSpec("obj_rover_ice", "rover_ice", "afternoon",
                "Drive the crew rover out to the ice prospecting site",
                "قد المركبة إلى موقع استكشاف الجليد", "Ice site · 52, -34"),
            new BoardRowSpec("obj_sensor_dust", "journal", "afternoon",
                "Brush the sensor heads and the antenna", "نظّف رؤوس الحساسات والهوائي",
                "Comms tower"),
            new BoardRowSpec("obj_plant_care", "journal", "afternoon",
                "Harvest the grow pod", "احصد المحصول", "Greenhouse"),
            new BoardRowSpec("obj_airlock_dust", "journal", "evening",
                "Sweep the airlock dust before the next EVA", "نظّف غرفة المعادل قبل الخروج التالي",
                "Airlock"),
            new BoardRowSpec("obj_comms_relay", "journal", "evening",
                "Realign the comms relay", "أعد ضبط مرحّل الاتصالات",
                "Mission Control · comms desk"),
            new BoardRowSpec("obj_air_scrubber", "journal", "evening",
                "Restart electrolysis and log the scrubber",
                "أعد تشغيل التحليل الكهربائي وسجّل الغسّال", "Life support"),
            new BoardRowSpec("obj_crew_rest", "journal", "night",
                "Rebalance the crew's rest plan", "أعد خطة الراحة للطاقم", "Habitat"),
            new BoardRowSpec("obj_radiation", "journal", "night",
                "Shelter the crew during a particle event", "احمِ الطاقم عند حدث الجسيمات",
                "Habitat · shielded section"),
            new BoardRowSpec("mission", "mission", "any",
                "Survive the mission — day %d of 30", "أكمل المهمة — اليوم %d من 30",
                ""),
        };

        public void Bind(Main main)
        {
            _main = main;
            BuildUi();
        }

        public override void _Process(double delta)
        {
            if (_subtitleTimer > 0)
            {
                _subtitleTimer -= delta;
                _subtitle.Modulate = new Color(1, 1, 1, Mathf.Clamp((float)_subtitleTimer, 0, 1));
                if (_subtitleTimer <= 0) _subtitle.Text = "";
            }

            for (int i = _feedItems.Count - 1; i >= 0; i--)
            {
                var item = _feedItems[i];
                item.life -= delta;
                if (item.life <= 0)
                {
                    item.node.QueueFree();
                    _feedItems.RemoveAt(i);
                }
                else if (item.life < 1.2)
                    item.node.Modulate = new Color(1, 1, 1, Mathf.Clamp((float)item.life / 1.2f, 0, 1));
            }

            if (_main?.Started == true && _main.Sim.Clock.IsMissionComplete && !_missionShown)
            {
                _missionShown = true;
                ShowToast(L.T("MISSION COMPLETE · 30 days", "اكتملت المهمة · 30 يومًا"), 12);
            }
        }

        // ---- build -----------------------------------------------------------

        private void BuildUi()
        {
            Layer = 10;
            _root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);
            ApplyTheme();

            BuildVisor();
            BuildTopBar();
            BuildResourcePanel();
            BuildBoard();
            BuildFeed();
            BuildPrompt();
            BuildSubtitle();
            BuildOverlays();
            BuildDialogue();
            BuildPause();
            BuildSettings();
            BuildStart();
        }

        private void ApplyTheme()
        {
            var theme = new Theme
            {
                DefaultFont = L.Arabic ? L.ArabicFont : L.LatinFont,
                DefaultFontSize = 14,
            };
            _root.Theme = theme;
            _root.LayoutDirection = L.Arabic ? Control.LayoutDirectionEnum.Rtl : Control.LayoutDirectionEnum.Ltr;
        }

        private void BuildVisor()
        {
            _visor = new VisorOverlay { Name = "Visor", Visible = false };
            _visor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_visor);
        }

        private void BuildTopBar()
        {
            var panel = Panel(new Color(0.03f, 0.05f, 0.08f, 0.86f), new Color(0.2f, 0.35f, 0.5f, 0.6f));
            panel.AnchorRight = 1;
            panel.OffsetLeft = 0;
            panel.OffsetTop = 0;
            panel.OffsetRight = 0;
            panel.OffsetBottom = 58;
            _root.AddChild(panel);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 18);
            margin.AddThemeConstantOverride("margin_right", 18);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            panel.AddChild(margin);

            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
            row.AddThemeConstantOverride("separation", 16);
            margin.AddChild(row);

            var title = new Label { Text = "LUNA-QUEST", Modulate = new Color(0.95f, 0.97f, 1f) };
            title.AddThemeFontSizeOverride("font_size", 20);
            row.AddChild(title);

            var sub = Txt(new Label(), "The Living Moon Colony", "المستعمرة القمرية الحية");
            sub.Modulate = new Color(0.55f, 0.7f, 0.85f);
            sub.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(sub);

            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(spacer);

            _sector = Txt(new Label(), "SHACKLETON RIM · 89.9° S · 7 CREW + NEXO", "حافة شاكلتون · ٨٩.٩° ج · ٧ أفراد + نيكسو");
            _sector.AddThemeFontSizeOverride("font_size", 12);
            _sector.Modulate = new Color(0.5f, 0.66f, 0.8f);
            row.AddChild(_sector);

            var spacer2 = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(spacer2);

            _day = new Label { Text = "Day 1 · 00:00" };
            _day.AddThemeFontSizeOverride("font_size", 16);
            row.AddChild(_day);

            _mission = new Label { Text = "Day 1 / 30" };
            _mission.AddThemeFontSizeOverride("font_size", 13);
            _mission.Modulate = new Color(0.6f, 0.75f, 0.9f);
            row.AddChild(_mission);

            _missionBar = new ProgressBar
            {
                MinValue = 0, MaxValue = 100, Value = 0, ShowPercentage = false,
                CustomMinimumSize = new Vector2(160, 10),
                TooltipText = "Mission progress",
            };
            StyleBar(_missionBar, new Color(0.3f, 0.8f, 1f, 0.9f));
            row.AddChild(_missionBar);

            _lang = new Button { Text = L.Arabic ? "EN" : "العربية", CustomMinimumSize = new Vector2(72, 30) };
            _lang.Pressed += () => _main.ToggleLanguage();
            row.AddChild(_lang);
        }

        private void BuildResourcePanel()
        {
            var panel = Panel(new Color(0.03f, 0.05f, 0.08f, 0.82f), new Color(0.2f, 0.35f, 0.5f, 0.45f));
            panel.AnchorTop = 0;
            panel.AnchorBottom = 0;
            panel.OffsetLeft = 16;
            panel.OffsetTop = 74;
            panel.OffsetRight = 330;
            panel.OffsetBottom = 470;
            _root.AddChild(panel);

            var margin = new MarginContainer();
            foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                margin.AddThemeConstantOverride(m, 12);
            panel.AddChild(margin);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 8);
            margin.AddChild(col);

            var head = Txt(new Label(), "COLONY STATUS", "حالة المستعمرة");
            head.AddThemeFontSizeOverride("font_size", 12);
            head.Modulate = new Color(0.55f, 0.72f, 0.9f);
            col.AddChild(head);

            AddRow(col, "pwr", "PWR", "طاقة", new Color(1f, 0.78f, 0.32f), 100);
            AddRow(col, "wtr", "WTR", "ماء", new Color(0.32f, 0.62f, 1f), 100);
            AddRow(col, "o2", "O2", "أكسجين", new Color(0.35f, 0.9f, 0.95f), 100);
            AddRow(col, "co2", "CO2", "CO2", new Color(0.75f, 0.5f, 0.95f), 10000);
            AddRow(col, "tmp", "TEMP", "حرارة", new Color(1f, 0.55f, 0.3f), 40);
            AddRow(col, "food", "FOOD", "غذاء", new Color(0.45f, 0.9f, 0.45f), 100);
            AddRow(col, "dose", "DOSE", "جرعة", new Color(1f, 0.35f, 0.35f), 200);
            AddRow(col, "sig", "LINK", "رابط", new Color(0.35f, 0.9f, 0.7f), 100);

            var hint = Txt(new Label(), "G  resource math   ·   J  journal", "G  حسابات الموارد   ·   J  السجل");
            hint.AddThemeFontSizeOverride("font_size", 11);
            hint.Modulate = new Color(0.45f, 0.6f, 0.75f);
            col.AddChild(hint);
        }

        private void AddRow(VBoxContainer col, string key, string labelEn, string labelAr, Color color, double max)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            col.AddChild(row);

            var name = Txt(new Label(), labelEn, labelAr);
            name.CustomMinimumSize = new Vector2(48, 0);
            name.AddThemeFontSizeOverride("font_size", 11);
            name.Modulate = new Color(0.7f, 0.82f, 0.92f);
            row.AddChild(name);

            var bar = new ProgressBar
            {
                MinValue = 0, MaxValue = max, Value = max * 0.7,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(150, 12),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            StyleBar(bar, color);
            row.AddChild(bar);

            var val = new Label { Text = "—" };
            val.CustomMinimumSize = new Vector2(86, 0);
            val.HorizontalAlignment = HorizontalAlignment.Right;
            val.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(val);

            _bars[key] = bar;
            _vals[key] = val;
        }

        /// <summary>
        /// The shift task board: a panel to the right of the status panel, under the toast band
        /// and clear of the crosshair, in the same style as the rest of the HUD — a header
        /// naming the current shift, then the fourteen rows. The labels are built once in
        /// canonical order and only moved when the shift changes, so a per-frame refresh costs a
        /// text swap rather than a rebuild.
        /// </summary>
        private void BuildBoard()
        {
            _boardPanel = Panel(new Color(0.03f, 0.05f, 0.08f, 0.78f), new Color(0.2f, 0.35f, 0.5f, 0.45f));
            _boardPanel.AnchorTop = 0;
            _boardPanel.AnchorBottom = 0;
            _boardPanel.OffsetLeft = 346;
            _boardPanel.OffsetTop = 172;
            _boardPanel.OffsetRight = 1016;
            _boardPanel.OffsetBottom = 432;
            _root.AddChild(_boardPanel);

            var margin = new MarginContainer();
            foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                margin.AddThemeConstantOverride(m, 8);
            _boardPanel.AddChild(margin);

            _boardBox = new VBoxContainer();
            _boardBox.AddThemeConstantOverride("separation", 1);
            margin.AddChild(_boardBox);

            _boardHeader = new Label();
            _boardHeader.AddThemeFontSizeOverride("font_size", 12);
            _boardHeader.AddThemeColorOverride("font_color", RowHeadColor);
            _boardBox.AddChild(_boardHeader);

            foreach (var spec in BoardRows)
            {
                var line = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
                line.AddThemeFontSizeOverride("font_size", 11);
                line.AddThemeColorOverride("font_color", RowColor);
                _boardBox.AddChild(line);
                _boardRows.Add(line);
            }

            UpdateBoard();
        }

        private void BuildFeed()
        {
            var panel = Panel(new Color(0.03f, 0.05f, 0.08f, 0.72f), new Color(0.2f, 0.35f, 0.5f, 0.35f));
            panel.AnchorLeft = 1;
            panel.AnchorRight = 1;
            panel.OffsetLeft = -390;
            panel.OffsetRight = -16;
            panel.OffsetTop = 74;
            panel.OffsetBottom = 460;
            _root.AddChild(panel);

            var margin = new MarginContainer();
            foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                margin.AddThemeConstantOverride(m, 12);
            panel.AddChild(margin);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 6);
            margin.AddChild(col);

            _feedTitle = Txt(new Label(), "COLONY LOG", "سجل المستعمرة");
            _feedTitle.AddThemeFontSizeOverride("font_size", 12);
            _feedTitle.Modulate = new Color(0.55f, 0.72f, 0.9f);
            col.AddChild(_feedTitle);

            // Standing order card. It gives the log a purpose before the first event
            // arrives, and repeats the one rule that matters: read before you touch.
            var brief = Panel(new Color(0.05f, 0.09f, 0.12f, 0.9f), new Color(0.3f, 0.65f, 0.8f, 0.45f));
            col.AddChild(brief);
            var bm = new MarginContainer();
            foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                bm.AddThemeConstantOverride(k, 9);
            brief.AddChild(bm);
            var bc = new VBoxContainer();
            bc.AddThemeConstantOverride("separation", 3);
            bm.AddChild(bc);
            var bt = Txt(new Label(), "FIRST WATCH · SOL 1", "الوردية الأولى · اليوم ١");
            bt.AddThemeFontSizeOverride("font_size", 11);
            bt.Modulate = new Color(0.5f, 0.9f, 1f);
            bc.AddChild(bt);
            var bb = Txt(new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            },
                "Walk the colony. At every console press E and read the number before you change it. Airlock, power, water, air.",
                "امشِ في المستعمرة. عند كل وحدة اضغط E واقرأ الرقم قبل أن تغيّره. بوابة الهواء، الطاقة، الماء، الهواء.");
            bb.AddThemeFontSizeOverride("font_size", 11);
            bb.Modulate = new Color(0.7f, 0.8f, 0.9f);
            bc.AddChild(bb);

            _feed = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _feed.AddThemeConstantOverride("separation", 6);
            col.AddChild(_feed);

            var hint = Txt(new Label(), "H  controls   ·   F5 save   ·   F9 load", "H  التحكم   ·   F5 حفظ   ·   F9 تحميل");
            hint.AddThemeFontSizeOverride("font_size", 11);
            hint.Modulate = new Color(0.45f, 0.6f, 0.75f);
            col.AddChild(hint);
        }

        private void BuildPrompt()
        {
            _promptPanel = Panel(new Color(0.05f, 0.09f, 0.13f, 0.92f), new Color(0.35f, 0.8f, 0.95f, 0.8f));
            _promptPanel.AnchorLeft = 0.5f;
            _promptPanel.AnchorRight = 0.5f;
            _promptPanel.AnchorTop = 1;
            _promptPanel.AnchorBottom = 1;
            _promptPanel.OffsetLeft = -260;
            _promptPanel.OffsetRight = 260;
            _promptPanel.OffsetTop = -118;
            _promptPanel.OffsetBottom = -70;
            _promptPanel.Visible = false;
            _root.AddChild(_promptPanel);

            _prompt = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _prompt.AddThemeFontSizeOverride("font_size", 15);
            _promptPanel.AddChild(_prompt);
        }

        private void BuildSubtitle()
        {
            _subtitle = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(1, 1, 1, 0),
            };
            _subtitle.AddThemeFontSizeOverride("font_size", 17);
            _subtitle.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
            _subtitle.AddThemeConstantOverride("outline_size", 8);
            _subtitle.AnchorLeft = 0.18f;
            _subtitle.AnchorRight = 0.82f;
            _subtitle.AnchorTop = 0.78f;
            _subtitle.AnchorBottom = 0.78f;
            _subtitle.OffsetTop = 0;
            _subtitle.OffsetBottom = 90;
            _root.AddChild(_subtitle);

            _toast = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.8f, 0.95f, 1f, 0f),
            };
            _toast.AddThemeFontSizeOverride("font_size", 18);
            _toast.AnchorLeft = 0.25f;
            _toast.AnchorRight = 0.75f;
            _toast.AnchorTop = 0.14f;
            _toast.AnchorBottom = 0.14f;
            _toast.OffsetBottom = 40;
            _root.AddChild(_toast);
        }

        private void BuildOverlays()
        {
            // Journal
            _journalPanel = Panel(new Color(0.03f, 0.05f, 0.08f, 0.96f), new Color(0.35f, 0.7f, 0.9f, 0.7f));
            _journalPanel.AnchorLeft = 0.18f;
            _journalPanel.AnchorRight = 0.82f;
            _journalPanel.AnchorTop = 0.12f;
            _journalPanel.AnchorBottom = 0.88f;
            _journalPanel.Visible = false;
            _root.AddChild(_journalPanel);

            var jm = new MarginContainer();
            foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                jm.AddThemeConstantOverride(m, 18);
            _journalPanel.AddChild(jm);
            var jc = new VBoxContainer();
            jc.AddThemeConstantOverride("separation", 8);
            jm.AddChild(jc);
            _journalTitle = Txt(new Label(), "FIELD JOURNAL", "دفتر الميدان");
            _journalTitle.AddThemeFontSizeOverride("font_size", 22);
            jc.AddChild(_journalTitle);
            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            jc.AddChild(scroll);
            _journalList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _journalList.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_journalList);

            // Help
            _helpPanel = Panel(new Color(0.03f, 0.05f, 0.08f, 0.96f), new Color(0.35f, 0.7f, 0.9f, 0.7f));
            _helpPanel.AnchorLeft = 0.28f;
            _helpPanel.AnchorRight = 0.72f;
            _helpPanel.AnchorTop = 0.18f;
            _helpPanel.AnchorBottom = 0.82f;
            _helpPanel.Visible = false;
            _root.AddChild(_helpPanel);
            var hm = new MarginContainer();
            foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                hm.AddThemeConstantOverride(m, 18);
            _helpPanel.AddChild(hm);
            var hc = new VBoxContainer();
            hc.AddThemeConstantOverride("separation", 8);
            hm.AddChild(hc);
            _helpTitle = Txt(new Label(), "CONTROLS", "التحكم");
            _helpTitle.AddThemeFontSizeOverride("font_size", 22);
            hc.AddChild(_helpTitle);
            var helpBody = Txt(new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            },
                "WASD  move          Shift  run          Space  jump\n" +
                "Mouse  look          V  first / third person\n" +
                "E  use the station you are facing\n" +
                "J  journal          G  resource math          H  this panel\n" +
                "F5  save            F9  load            L  language\n" +
                "Esc  pause / release the mouse",
                "WASD  حركة          Shift  جري          Space  قفز\n" +
                "الفأرة  النظر          V  منظور أول / ثالث\n" +
                "E  استخدام المحطة المقابلة\n" +
                "J  الدفتر          G  حسابات الموارد          H  هذه اللوحة\n" +
                "F5  حفظ            F9  تحميل            L  اللغة\n" +
                "Esc  إيقاف مؤقت / تحرير الفأرة");
            helpBody.AddThemeFontSizeOverride("font_size", 15);
            hc.AddChild(helpBody);

            // Plant + habitat math
            _plantsPanel = Panel(new Color(0.03f, 0.06f, 0.06f, 0.94f), new Color(0.4f, 0.85f, 0.6f, 0.7f));
            _plantsPanel.AnchorLeft = 1;
            _plantsPanel.AnchorRight = 1;
            _plantsPanel.AnchorTop = 0.5f;
            _plantsPanel.AnchorBottom = 0.5f;
            _plantsPanel.OffsetLeft = -470;
            _plantsPanel.OffsetRight = -16;
            _plantsPanel.OffsetTop = -150;
            _plantsPanel.OffsetBottom = 170;
            _plantsPanel.Visible = false;
            _root.AddChild(_plantsPanel);
            var pm = new MarginContainer();
            foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                pm.AddThemeConstantOverride(m, 14);
            _plantsPanel.AddChild(pm);
            var pc = new VBoxContainer();
            pc.AddThemeConstantOverride("separation", 6);
            pm.AddChild(pc);
            _plantsTitle = Txt(new Label(), "RESOURCE MATH", "حسابات الموارد");
            _plantsTitle.AddThemeFontSizeOverride("font_size", 16);
            pc.AddChild(_plantsTitle);
            _plantsBox = new VBoxContainer();
            _plantsBox.AddThemeConstantOverride("separation", 4);
            pc.AddChild(_plantsBox);
        }

        /// <summary>
        /// A conversation is a short, selectable topic list, not a branching tree. The
        /// greeting plays once, then the player picks one of six lines and leaves.
        /// </summary>
        private void BuildDialogue()
        {
            _dialoguePanel = Panel(new Color(0.025f, 0.045f, 0.065f, 0.97f), new Color(0.4f, 0.8f, 0.95f, 0.8f));
            _dialoguePanel.AnchorLeft = 0.2f;
            _dialoguePanel.AnchorRight = 0.8f;
            _dialoguePanel.AnchorTop = 0.12f;
            _dialoguePanel.AnchorBottom = 0.88f;
            _dialoguePanel.Visible = false;
            _dialoguePanel.MouseFilter = Control.MouseFilterEnum.Stop;
            _root.AddChild(_dialoguePanel);

            var m = new MarginContainer();
            foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                m.AddThemeConstantOverride(k, 20);
            _dialoguePanel.AddChild(m);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 8);
            m.AddChild(col);

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 12);
            col.AddChild(header);
            var id = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            id.AddThemeConstantOverride("separation", 0);
            header.AddChild(id);

            _dialogueName = new Label();
            _dialogueName.AddThemeFontSizeOverride("font_size", 24);
            id.AddChild(_dialogueName);

            _dialogueRole = new Label { Modulate = new Color(0.55f, 0.8f, 0.95f) };
            _dialogueRole.AddThemeFontSizeOverride("font_size", 14);
            id.AddChild(_dialogueRole);

            _dialogueLeave = new Button { CustomMinimumSize = new Vector2(110, 0) };
            _dialogueLeave.Pressed += () => _main.CloseDialogue();
            header.AddChild(_dialogueLeave);

            _dialogueSkills = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.65f, 0.9f, 0.8f),
            };
            _dialogueSkills.AddThemeFontSizeOverride("font_size", 13);
            col.AddChild(_dialogueSkills);

            _dialogueResp = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.78f, 0.85f, 0.92f),
            };
            _dialogueResp.AddThemeFontSizeOverride("font_size", 13);
            col.AddChild(_dialogueResp);

            _dialogueGreeting = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.9f, 0.95f, 1f),
            };
            _dialogueGreeting.AddThemeFontSizeOverride("font_size", 15);
            col.AddChild(_dialogueGreeting);

            var rule = new HSeparator();
            rule.Modulate = new Color(0.35f, 0.7f, 0.9f, 0.4f);
            col.AddChild(rule);

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            col.AddChild(scroll);
            _dialogueTopics = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _dialogueTopics.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_dialogueTopics);

            _dialogueLine = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 64),
                VerticalAlignment = VerticalAlignment.Top,
            };
            _dialogueLine.AddThemeFontSizeOverride("font_size", 15);
            col.AddChild(_dialogueLine);
        }

        private void BuildPause()
        {
            _pauseDim = new ColorRect
            {
                Color = new Color(0f, 0f, 0f, 0.55f),
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _pauseDim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_pauseDim);

            _pausePanel = Panel(new Color(0.01f, 0.02f, 0.035f, 0.97f), new Color(0.35f, 0.75f, 0.95f, 0.65f));
            _pausePanel.AnchorLeft = 0.32f;
            _pausePanel.AnchorRight = 0.68f;
            _pausePanel.AnchorTop = 0.3f;
            _pausePanel.AnchorBottom = 0.7f;
            _pausePanel.Visible = false;
            _root.AddChild(_pausePanel);

            var m = new MarginContainer();
            foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                m.AddThemeConstantOverride(k, 24);
            _pausePanel.AddChild(m);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 12);
            m.AddChild(col);

            var title = Txt(new Label { HorizontalAlignment = HorizontalAlignment.Center }, "PAUSED", "متوقف مؤقتًا");
            title.AddThemeFontSizeOverride("font_size", 30);
            col.AddChild(title);

            var body = Txt(new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            },
                "The colony clock is stopped. Nothing is consumed while you are away.",
                "ساعة المستعمرة متوقفة. لا يُستهلك شيء أثناء غيابك.");
            body.AddThemeFontSizeOverride("font_size", 13);
            body.Modulate = new Color(0.65f, 0.78f, 0.9f);
            col.AddChild(body);

            var settingsBtn = new Button { Text = "Settings · الإعدادات", CustomMinimumSize = new Vector2(0, 40) };
            settingsBtn.Pressed += OpenSettings;
            col.AddChild(settingsBtn);

            _resume = new Button { Text = "Resume", CustomMinimumSize = new Vector2(0, 42) };
            _resume.Pressed += () => _main.ResumeGame();
            col.AddChild(_resume);

            var hint = Txt(new Label { HorizontalAlignment = HorizontalAlignment.Center }, "Esc  resume", "Esc  استئناف");
            hint.AddThemeFontSizeOverride("font_size", 12);
            hint.Modulate = new Color(0.5f, 0.65f, 0.8f);
            col.AddChild(hint);
        }

        /// <summary>The settings panel: every choice applies live and persists to
        /// user://settings.json (never the shared save). Rows are plain buttons so the panel is
        /// both keyboard- and mouse-navigable, and bilingual by construction.</summary>
        private VBoxContainer? _settingsBody;

        private void BuildSettings()
        {
            _settingsPanel = Panel(new Color(0.01f, 0.02f, 0.035f, 0.97f), new Color(0.35f, 0.75f, 0.95f, 0.65f));
            _settingsPanel.AnchorLeft = 0.22f;
            _settingsPanel.AnchorRight = 0.78f;
            _settingsPanel.AnchorTop = 0.08f;
            _settingsPanel.AnchorBottom = 0.92f;
            _settingsPanel.Visible = false;
            _root.AddChild(_settingsPanel);

            var m = new MarginContainer();
            foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                m.AddThemeConstantOverride(k, 18);
            _settingsPanel.AddChild(m);

            var scroll = new ScrollContainer { FollowFocus = true };
            m.AddChild(scroll);

            _settingsBody = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _settingsBody.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_settingsBody);
        }

        public void OpenSettings()
        {
            if (_settingsPanel == null || _settingsBody == null || _main == null) return;
            foreach (Node child in _settingsBody.GetChildren()) child.QueueFree();
            RebuildSettingsRows();
            var done = new Button { Text = "Done · تم", CustomMinimumSize = new Vector2(0, 40) };
            done.Pressed += () => _settingsPanel.Visible = false;
            _settingsBody.AddChild(done);
            _settingsPanel.Visible = true;
        }

        private void RebuildSettingsRows()
        {
            var s = _main!.LiveSettings;
            var col = _settingsBody!;
            var title = Txt(new Label { HorizontalAlignment = HorizontalAlignment.Center }, "Settings", "الإعدادات");
            title.AddThemeFontSizeOverride("font_size", 24);
            title.Modulate = new Color(0.92f, 0.96f, 1f);
            col.AddChild(title);

            CycleRow(col, "Camera · الكاميرا", new[] { "First person · منظور الخوذة", "Third person · كاميرا البدلة" },
                () => s.ThirdPerson ? 1 : 0,
                i => { s.ThirdPerson = i == 1; });
            CycleRow(col, "Language · اللغة", new[] { "English", "العربية" },
                () => s.Language == "ar" ? 1 : 0,
                i => { s.Language = i == 1 ? "ar" : "en"; });
            CycleRow(col, "Sensitivity · حساسية الماوس", new[] { "×0.5", "×1.0", "×1.5", "×2.0" },
                () => s.MouseSensitivity <= 0.75f ? 0 : s.MouseSensitivity <= 1.25f ? 1 : s.MouseSensitivity <= 1.75f ? 2 : 3,
                i => { s.MouseSensitivity = new[] { 0.5f, 1.0f, 1.5f, 2.0f }[i]; });
            CycleRow(col, "Field of view · مجال الرؤية", new[] { "62°", "70°", "78°" },
                () => s.Fov <= 66f ? 0 : s.Fov <= 74f ? 1 : 2,
                i => { s.Fov = new[] { 62f, 70f, 78f }[i]; });
            CycleRow(col, "Master volume · مستوى الصوت", new[] { "Off", "Low", "Medium", "High" },
                () => s.MasterVolume <= 0.01f ? 0 : s.MasterVolume <= 0.6f ? 1 : s.MasterVolume <= 0.9f ? 2 : 3,
                i => { s.MasterVolume = new[] { 0.0f, 0.4f, 0.8f, 1.0f }[i]; });
            CycleRow(col, "Voice · الصوت المنطوق", new[] { "Off", "On" },
                () => s.Voice ? 1 : 0,
                i => { s.Voice = i == 1; });
            CycleRow(col, "Subtitles · الترجمة", new[] { "Off", "On" },
                () => s.Subtitles ? 1 : 0,
                i => { s.Subtitles = i == 1; });
            CycleRow(col, "Quality · الجودة", new[] { "Low · منخفضة", "Medium · متوسطة", "High · عالية" },
                () => s.Quality == "low" ? 0 : s.Quality == "medium" ? 1 : 2,
                i => { s.Quality = new[] { "low", "medium", "high" }[i]; });
            CycleRow(col, "Fullscreen · ملء الشاشة", new[] { "Off", "On" },
                () => s.Fullscreen ? 1 : 0,
                i => { s.Fullscreen = i == 1; });
        }

        private void CycleRow(VBoxContainer col, string combined, string[] options,
            System.Func<int> current, System.Action<int> apply)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 16);
            var label = Txt(new Label { CustomMinimumSize = new Vector2(280, 0) }, combined, combined);
            row.AddChild(label);
            var btn = new Button { CustomMinimumSize = new Vector2(230, 34) };
            btn.Text = options[Mathf.Clamp(current(), 0, options.Length - 1)];
            row.AddChild(btn);
            btn.Pressed += () =>
            {
                int next = (current() + 1) % options.Length;
                apply(next);
                _main!.ApplySettings(_main.LiveSettings);
                btn.Text = options[next];
            };
            col.AddChild(row);
        }

        private void BuildStart()
        {
            _startOverlay = Panel(new Color(0.01f, 0.015f, 0.025f, 0.52f), new Color(0.3f, 0.6f, 0.85f, 0.0f));
            _startOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_startOverlay);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _startOverlay.AddChild(center);

            var panel = Panel(new Color(0.04f, 0.07f, 0.11f, 0.98f), new Color(0.35f, 0.75f, 0.95f, 0.65f));
            panel.CustomMinimumSize = new Vector2(560, 0);
            center.AddChild(panel);

            var m = new MarginContainer();
            foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                m.AddThemeConstantOverride(k, 28);
            panel.AddChild(m);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 12);
            m.AddChild(col);

            _startTitle = new Label { Text = "LUNA-QUEST", HorizontalAlignment = HorizontalAlignment.Center };
            _startTitle.AddThemeFontSizeOverride("font_size", 56);
            _startTitle.Modulate = new Color(0.92f, 0.96f, 1f);
            col.AddChild(_startTitle);

            _startSub = Txt(new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            },
                "The Living Moon Colony · Shackleton Rim\n30 Earth days · six crew, a commander and NEXO · every kilogram counted",
                "المستعمرة القمرية الحية · حافة شاكلتون\n30 يومًا أرضيًا · ستة أفراد وقائد ونيكسو · كل كيلوغرام محسوب");
            _startSub.AddThemeFontSizeOverride("font_size", 15);
            _startSub.Modulate = new Color(0.6f, 0.75f, 0.9f);
            col.AddChild(_startSub);

            _nameLabel = Txt(new Label(), "How should the crew address you?", "كيف يناديك الطاقم؟");
            _nameLabel.AddThemeFontSizeOverride("font_size", 14);
            col.AddChild(_nameLabel);

            _nameEdit = new LineEdit { Text = "Amara Okafor", MaxLength = 28, CustomMinimumSize = new Vector2(0, 38) };
            _nameEdit.TextSubmitted += _ => Begin();
            col.AddChild(_nameEdit);

            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 10);
            col.AddChild(row);

            var en = new Button { Text = "English", CustomMinimumSize = new Vector2(120, 36) };
            en.Pressed += () => SetLanguage(false);
            row.AddChild(en);

            var ar = new Button { Text = "العربية", CustomMinimumSize = new Vector2(120, 36) };
            ar.Pressed += () => SetLanguage(true);
            row.AddChild(ar);

            var settingsBtn = new Button { Text = "Settings · الإعدادات", CustomMinimumSize = new Vector2(240, 36) };
            settingsBtn.Pressed += OpenSettings;
            col.AddChild(settingsBtn);

            _begin = new Button { Text = "Begin the mission", CustomMinimumSize = new Vector2(240, 42) };
            _begin.Pressed += Begin;
            col.AddChild(_begin);

            if (IntroPlayer.IsAvailable())
            {
                var film = new Button { Text = L.T("Watch the intro film", "شاهد فيلم المقدمة"), CustomMinimumSize = new Vector2(240, 36) };
                film.Pressed += () => _main.PlayIntro();
                col.AddChild(film);
            }

            _startHint = Txt(new Label(), "Your name is saved in the shared save file and appears in crew lines.", "يُحفظ اسمك في ملف الحفظ المشترك ويظهر في كلام الطاقم.");
            _startHint.AddThemeFontSizeOverride("font_size", 12);
            _startHint.Modulate = new Color(0.5f, 0.62f, 0.75f);
            col.AddChild(_startHint);

            _startControls = Txt(new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            },
                "WASD move · mouse look · E interact · V camera · J journal · G resources · H help · M music",
                "WASD حركة · الفأرة نظر · E تفاعل · V كاميرا · J دفتر · G موارد · H مساعدة · M موسيقى");
            _startControls.AddThemeFontSizeOverride("font_size", 12);
            _startControls.Modulate = new Color(0.45f, 0.6f, 0.75f);
            col.AddChild(_startControls);
        }

        private void Begin()
        {
            string name = _nameEdit.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "Amara Okafor";
            _main.BeginGame(name);
            _startOverlay.Visible = false;
        }

        public void HideStart() => _startOverlay.Visible = false;

        private void SetLanguage(bool arabic)
        {
            if (L.Arabic == arabic) return;
            _main.SetLanguage(arabic);
        }

        // ---- runtime ---------------------------------------------------------

        /// <summary>The board's own surface, box, header and row labels, for the playtest.
        /// <c>BoardRowLabels[i]</c> always describes <c>BoardRows[i]</c> — reordering the shift
        /// moves nodes, never this list.</summary>
        public PanelContainer BoardPanel => _boardPanel;
        public VBoxContainer BoardBox => _boardBox;
        public Label BoardHeader => _boardHeader;
        public IReadOnlyList<Label> BoardRowLabels => _boardRows;

        public void UpdateState()
        {
            var sim = _main.Sim;
            var s = sim.State;
            _day.Text = sim.Clock.Stamp();
            _missionBar.Value = sim.Clock.MissionProgress * 100.0;
            _mission.Text = L.T($"Day {sim.Clock.Day} / 30", $"اليوم {sim.Clock.Day} / 30");

            SetBar("pwr", s.BatteryPercent * 100, $"{s.BatteryPercent * 100:0}% · {s.SolarKw:0.0} kW", s.BatteryPercent < 0.25);
            SetBar("wtr", s.WaterPercent * 100, $"{s.WaterL:0} / {s.WaterCapacityL:0} L", s.WaterPercent < 0.2);
            SetBar("o2", s.O2Percent * 100, $"{s.O2Percent * 100:0}% · {s.O2Kg:0} kg", s.O2Percent < 0.35);
            SetBar("co2", Math.Min(10000, s.Co2Ppm), $"{s.Co2Ppm:0} ppm", s.Co2Ppm > 4000);
            SetBar("tmp", s.CabinTempC, $"{s.CabinTempC:0.0} °C", s.CabinTempC < 18 || s.CabinTempC > 27);
            SetBar("food", s.FoodPercent * 100, $"{s.FoodPercent * 100:0}%", s.FoodPercent < 0.25);
            SetBar("dose", Math.Min(200, s.DoseRateMicroGyH), $"{s.DoseRateMicroGyH:0.0} µGy/h", s.SolarParticleEvent);
            SetBar("sig", s.SignalQuality * 100, $"{s.SignalQuality * 100:0}%", s.SignalQuality < 0.5);

            UpdateBoard();
            if (_plantsPanel.Visible) RefreshPlants();
        }

        private void SetBar(string key, double value, string text, bool warn)
        {
            if (!_bars.TryGetValue(key, out var bar)) return;
            bar.Value = value;
            _vals[key].Text = text;
            _vals[key].Modulate = warn ? new Color(1f, 0.42f, 0.38f) : new Color(0.82f, 0.9f, 1f);
        }

        /// <summary>
        /// Which shift the clock is in, with its bilingual header. Six to twelve is morning,
        /// twelve to eighteen is afternoon, eighteen and later is evening, everything before
        /// six is night.
        /// </summary>
        public static ShiftSpec CurrentShift(double hour)
        {
            if (hour >= 6.0 && hour < 12.0)
                return new ShiftSpec("morning", "Morning shift - power, water, air",
                    "مناوبة الصباح - الطاقة والماء والهواء");
            if (hour >= 12.0 && hour < 18.0)
                return new ShiftSpec("afternoon", "Afternoon shift - samples and crops",
                    "مناوبة الظهيرة - العينات والمحاصيل");
            if (hour >= 18.0)
                return new ShiftSpec("evening", "Evening shift - dust, comms, oxygen",
                    "مناوبة المساء - الغبار والاتصالات والأكسجين");
            return new ShiftSpec("night", "Night shift - rest and radiation watch",
                "مناوبة الليل - الراحة ومراقبة الإشعاع");
        }

        /// <summary>
        /// Paint the board: the header names the shift, the row that leads is the one for the
        /// shift the clock says it is, and a finished row gets the check and the teal colour.
        /// The labels keep their canonical identity, so only their position in the box ever
        /// changes with the shift — a per-frame refresh is a text swap.
        /// </summary>
        private void UpdateBoard()
        {
            if (_boardRows.Count == 0 || _main?.Sim == null) return;
            var sim = _main.Sim;
            var shift = CurrentShift(sim.Clock.HourOfDay);
            _boardHeader.Text = L.T(shift.En, shift.Ar);

            if (shift.Id != _boardShift)
            {
                _boardShift = shift.Id;
                // The current shift leads and everything behind it keeps its canonical order, so
                // pulling the leading rows to the front in a single pass leaves the board sorted.
                int at = 1; // index 0 is the header
                for (int i = 0; i < BoardRows.Length; i++)
                {
                    if (BoardRows[i].Shift != shift.Id) continue;
                    _boardBox.MoveChild(_boardRows[i], at);
                    at++;
                }
            }

            var rover = _main.Rover;
            int day = Mathf.Min(sim.Clock.Day, 30);
            for (int i = 0; i < BoardRows.Length; i++)
            {
                var row = BoardRows[i];
                string label = L.T(row.En, row.Ar);
                string hint = row.Hint;
                bool done;
                switch (row.Kind)
                {
                    case "journal":
                        done = sim.Learning.HasEntry(row.Id);
                        break;
                    case "rover_driven":
                        done = rover != null && rover.HasDriven;
                        break;
                    case "rover_ice":
                        done = rover != null && rover.ReachedIce;
                        break;
                    default: // mission
                        done = sim.Clock.Day > 30;
                        label = L.T($"Survive the mission — day {day} of 30",
                            $"أكمل المهمة — اليوم {day} من 30");
                        hint = $"{sim.Clock.MissionProgress * 100.0:0}%";
                        break;
                }
                string text = hint.Length > 0 ? $"{label}  ·  {hint}" : label;
                if (done) text = "✓ " + text;
                _boardRows[i].Text = text;
                _boardRows[i].AddThemeColorOverride("font_color", done ? DoneColor : RowColor);
            }
        }

        public void SetPrompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text) && _main.Started && !DialogueVisible;
            _promptPanel.Visible = show;
            if (show) _prompt.Text = L.T("E   ", "E   ") + text;
        }

        public void ShowSubtitle(string text, double seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            _subtitle.Text = text;
            _subtitle.Modulate = new Color(1, 1, 1, 1);
            _subtitleTimer = seconds;
        }

        public void ShowToast(string text, double seconds = 3)
        {
            _toast.Text = text;
            _toast.Modulate = new Color(0.8f, 0.95f, 1f, 1f);
            var tween = CreateTween();
            tween.TweenInterval(seconds);
            tween.TweenProperty(_toast, "modulate:a", 0f, 0.8);
        }

        public void PushEvent(SimEvent e)
        {
            var kindColor = e.Kind switch
            {
                SimEventKind.Critical => new Color(1f, 0.38f, 0.34f),
                SimEventKind.Warning => new Color(1f, 0.72f, 0.3f),
                SimEventKind.Mission => new Color(0.4f, 0.85f, 1f),
                SimEventKind.Discovery => new Color(0.55f, 0.95f, 0.6f),
                _ => new Color(0.62f, 0.8f, 0.95f),
            };

            var panel = Panel(new Color(0.02f, 0.035f, 0.055f, 0.9f), new Color(kindColor, 0.45f));
            _feed.AddChild(panel);
            var m = new MarginContainer();
            foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                m.AddThemeConstantOverride(k, 7);
            panel.AddChild(m);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 2);
            m.AddChild(col);

            var title = new Label
            {
                Text = L.Arabic ? e.TitleAr : e.TitleEn,
                Modulate = kindColor,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            title.AddThemeFontSizeOverride("font_size", 13);
            col.AddChild(title);

            if (e.Kind is SimEventKind.Warning or SimEventKind.Critical or SimEventKind.Mission)
            {
                var body = new Label
                {
                    Text = L.Arabic ? e.BodyAr : e.BodyEn,
                    Modulate = new Color(0.78f, 0.86f, 0.95f),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                };
                body.AddThemeFontSizeOverride("font_size", 11);
                col.AddChild(body);
            }

            _feedItems.Add((panel, 18.0));
            while (_feedItems.Count > 4)
            {
                var oldest = _feedItems[0];
                oldest.node.QueueFree();
                _feedItems.RemoveAt(0);
            }
        }

        public void ToggleJournal()
        {
            _journalPanel.Visible = !_journalPanel.Visible;
            if (_journalPanel.Visible) RefreshJournal();
        }

        public void ToggleHelp() => _helpPanel.Visible = !_helpPanel.Visible;

        public bool DialogueVisible => _dialoguePanel.Visible;

        public void OpenDialogue(NpcProfile profile, List<DialogueTopic> topics)
        {
            _dialogueNpc = profile;
            _dialogueCurrent = null;
            _dialogueList.Clear();
            _dialogueList.AddRange(topics);
            _journalPanel.Visible = false;
            _helpPanel.Visible = false;
            _plantsPanel.Visible = false;
            _promptPanel.Visible = false;
            _dialoguePanel.Visible = true;
            RefreshDialogue();
        }

        public void SetDialogueTopic(DialogueTopic topic)
        {
            _dialogueCurrent = topic;
            SetDialogueLine(L.T(topic.LineEn, topic.LineAr));
        }

        public void CloseDialogue()
        {
            _dialoguePanel.Visible = false;
            _dialogueNpc = null;
            _dialogueCurrent = null;
            _dialogueList.Clear();
            foreach (var c in _dialogueTopics.GetChildren()) c.QueueFree();
        }

        private void RefreshDialogue()
        {
            if (_dialogueNpc == null) return;
            _dialogueName.Text = L.T(_dialogueNpc.DisplayNameEn, _dialogueNpc.DisplayNameAr);
            _dialogueRole.Text = L.T(_dialogueNpc.RoleEn, _dialogueNpc.RoleAr);

            string skills = string.Join(" | ", _dialogueNpc.Skills);
            _dialogueSkills.Text = string.IsNullOrEmpty(skills) ? "" : $"{L.T("Skills", "المهارات")}: {skills}";
            _dialogueSkills.Visible = !string.IsNullOrEmpty(skills);

            string resp = L.T(_dialogueNpc.ResponsibilitiesEn, _dialogueNpc.ResponsibilitiesAr);
            _dialogueResp.Text = resp;
            _dialogueResp.Visible = !string.IsNullOrEmpty(resp);

            string greeting = L.T(_dialogueNpc.GreetingEn, _dialogueNpc.GreetingAr);
            _dialogueGreeting.Text = string.IsNullOrEmpty(greeting) ? "" : $"“{greeting}”";
            _dialogueGreeting.Visible = !string.IsNullOrEmpty(greeting);
            _dialogueLeave.Text = L.T("Leave", "إنهاء");

            foreach (var c in _dialogueTopics.GetChildren()) c.QueueFree();
            for (int i = 0; i < _dialogueList.Count; i++)
            {
                var topic = _dialogueList[i];
                var button = new Button
                {
                    Text = $"{i + 1}.  " + L.T(topic.LabelEn, topic.LabelAr),
                    Alignment = HorizontalAlignment.Left,
                    CustomMinimumSize = new Vector2(0, 34),
                };
                button.AddThemeFontSizeOverride("font_size", 15);
                button.Pressed += () => _main.AskNpcTopic(topic);
                _dialogueTopics.AddChild(button);
            }

            if (_dialogueCurrent != null)
                SetDialogueLine(L.T(_dialogueCurrent.LineEn, _dialogueCurrent.LineAr));
            else
                SetDialogueLine("");
        }

        private void SetDialogueLine(string text)
        {
            bool hint = string.IsNullOrEmpty(text);
            _dialogueLine.Text = hint
                ? L.T("Choose a topic. One short line, then back to work.",
                    "اختر موضوعًا. سطر قصير ثم نعود للعمل.")
                : text;
            _dialogueLine.Modulate = hint
                ? new Color(0.6f, 0.72f, 0.84f)
                : new Color(0.93f, 0.97f, 1f);
        }

        public void TogglePlants()
        {
            _plantsPanel.Visible = !_plantsPanel.Visible;
            if (_plantsPanel.Visible) RefreshPlants();
        }

        public void CloseOverlays()
        {
            _journalPanel.Visible = false;
            _helpPanel.Visible = false;
            _plantsPanel.Visible = false;
        }

        public bool AnyOverlayVisible => _journalPanel.Visible || _helpPanel.Visible || _plantsPanel.Visible || _pausePanel.Visible || _dialoguePanel.Visible;

        public bool PauseVisible => _pausePanel.Visible;

        public void ShowPause(bool paused)
        {
            _pausePanel.Visible = paused;
            _pauseDim.Visible = paused;
            _promptPanel.Visible = false;
            if (paused) _resume.GrabFocus();
            else _resume.ReleaseFocus();
        }

        public void SetFirstPerson(bool firstPerson)
        {
            _visor.Visible = firstPerson;
            if (firstPerson) _visor.QueueRedraw();
        }

        private void RefreshJournal()
        {
            foreach (var c in _journalList.GetChildren()) c.QueueFree();
            var entries = _main.Sim.Learning.Journal;
            if (entries.Count == 0)
            {
                _journalList.AddChild(new Label
                {
                    Text = L.T("Nothing logged yet. Do the work, then read it here.", "لا شيء مسجل بعد. اعمل أولًا ثم اقرأ هنا."),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    Modulate = new Color(0.6f, 0.72f, 0.85f),
                });
                return;
            }

            foreach (var entry in entries)
            {
                var card = Panel(new Color(0.05f, 0.08f, 0.12f, 0.95f), new Color(0.3f, 0.55f, 0.75f, 0.5f));
                _journalList.AddChild(card);
                var m = new MarginContainer();
                foreach (var k in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                    m.AddThemeConstantOverride(k, 10);
                card.AddChild(m);
                var col = new VBoxContainer();
                col.AddThemeConstantOverride("separation", 3);
                m.AddChild(col);

                var title = new Label { Text = L.Arabic ? entry.TitleAr : entry.TitleEn, Modulate = new Color(0.85f, 0.95f, 1f) };
                title.AddThemeFontSizeOverride("font_size", 15);
                col.AddChild(title);

                var body = new Label
                {
                    Text = L.Arabic ? entry.BodyAr : entry.BodyEn,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    Modulate = new Color(0.72f, 0.82f, 0.92f),
                };
                body.AddThemeFontSizeOverride("font_size", 12);
                col.AddChild(body);

                var meta = new Label
                {
                    Text = $"{(L.Arabic ? "دليل" : "evidence")} {entry.EvidenceClass} · {entry.Skill} · {entry.Source}",
                    Modulate = new Color(0.45f, 0.6f, 0.72f),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                };
                meta.AddThemeFontSizeOverride("font_size", 10);
                col.AddChild(meta);
            }
        }

        private void RefreshPlants()
        {
            foreach (var c in _plantsBox.GetChildren()) c.QueueFree();
            var s = _main.Sim.State;
            double waterDay = CropModel.PodAreaM2 * CropModel.WaterLPerM2Day;
            double co2Day = CropModel.PodAreaM2 * CropModel.Co2KgPerM2Day;
            double harvest = CropModel.HarvestKcalPerM2 * CropModel.PodAreaM2;
            double habitatKwh = (s.BaseLoadKw + s.HeaterKw + (s.O2GeneratorRunning ? s.O2GeneratorKw : 0)) * 24.0;

            AddLine(_plantsBox, L.T("PLANTS — sealed grow pod", "النباتات — وحدة نمو مغلقة"), new Color(0.55f, 0.95f, 0.65f));
            AddLine(_plantsBox, L.T($"light   {(s.GrowLightsOn ? "2.0" : "0.0")} kW from colony power (no direct Sun)", $"الضوء   {(s.GrowLightsOn ? "2.0" : "0.0")} كيلوواط من طاقة المستعمرة (بدون شمس مباشرة)"));
            AddLine(_plantsBox, L.T($"air     {co2Day:0.00} kg CO2/day from the cabin", $"الهواء   {co2Day:0.00} كغ CO2/يوم من المقصورة"));
            AddLine(_plantsBox, L.T($"water   {waterDay:0} L/day from the tank · 90% recovered by condensate", $"الماء   {waterDay:0} لتر/يوم من الخزان · استعادة 90% بالتكاثف"));
            AddLine(_plantsBox, L.T($"food    +{harvest:0} kcal per {CropModel.CycleDays:0}-day cycle", $"الغذاء   +{harvest:0} سعرة لكل دورة {CropModel.CycleDays:0} يومًا"));
            AddLine(_plantsBox, L.T($"crop    {CropModel.Stage(s)} · health {s.CropHealth * 100:0}%", $"المحصول   {CropModel.Stage(s)} · الصحة {s.CropHealth * 100:0}%"));

            _plantsBox.AddChild(new HSeparator());

            AddLine(_plantsBox, L.T("HABITAT — per Earth day", "الموئل — لكل يوم أرضي"), new Color(0.55f, 0.8f, 1f));
            AddLine(_plantsBox, L.T($"power   {habitatKwh:0} kWh · battery {s.BatteryKwh:0}/{s.BatteryCapacityKwh:0} kWh", $"الطاقة   {habitatKwh:0} كيلوواط·ساعة · البطارية {s.BatteryKwh:0}/{s.BatteryCapacityKwh:0}"));
            AddLine(_plantsBox, L.T($"water   {WaterModel.LitresPerPersonDay * s.CrewCount:0} L/day · recycled at {s.RecyclerEfficiency * 100:0}%", $"الماء   {WaterModel.LitresPerPersonDay * s.CrewCount:0} لتر/يوم · تدوير {s.RecyclerEfficiency * 100:0}%"));
            AddLine(_plantsBox, L.T($"oxygen  {AirModel.O2KgPerPersonDay * s.CrewCount:0.0} kg/day consumed · {s.O2Kg:0} kg stored", $"الأكسجين   {AirModel.O2KgPerPersonDay * s.CrewCount:0.0} كغ/يوم · المخزون {s.O2Kg:0} كغ"));
            AddLine(_plantsBox, L.T($"food    {FoodModel.KcalPerPersonDay * s.CrewCount:0} kcal/day · {FoodModel.DaysRemaining(s):0} days stored", $"الغذاء   {FoodModel.KcalPerPersonDay * s.CrewCount:0} سعرة/يوم · مخزون {FoodModel.DaysRemaining(s):0} يوم"));
        }

        private static void AddLine(VBoxContainer box, string text, Color? color = null)
        {
            var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = color ?? new Color(0.78f, 0.88f, 0.96f) };
            l.AddThemeFontSizeOverride("font_size", 12);
            box.AddChild(l);
        }

        public void RefreshLanguage()
        {
            ApplyTheme();
            foreach (var kv in _texts)
            {
                if (IsInstanceValid(kv.Key)) kv.Key.Text = L.Arabic ? kv.Value.ar : kv.Value.en;
            }
            _lang.Text = L.Arabic ? "EN" : "العربية";
            _begin.Text = L.Arabic ? "ابدأ المهمة" : "Begin the mission";
            _resume.Text = L.T("Resume", "استئناف");
            _nameEdit.Text = _main.Sim.PlayerName;
            _day.Text = _main.Sim.Clock.Stamp();
            _journalTitle.Text = L.T("FIELD JOURNAL", "دفتر الميدان");
            _helpTitle.Text = L.T("CONTROLS", "التحكم");
            _plantsTitle.Text = L.T("RESOURCE MATH", "حسابات الموارد");
            _feedTitle.Text = L.T("COLONY LOG", "سجل المستعمرة");
            foreach (var item in _feedItems) item.node.QueueFree();
            _feedItems.Clear();
            if (_journalPanel.Visible) RefreshJournal();
            if (_plantsPanel.Visible) RefreshPlants();
            if (_dialoguePanel.Visible) RefreshDialogue();
            UpdateBoard();
        }

        // ---- helpers ---------------------------------------------------------

        private Label Txt(Label label, string en, string ar)
        {
            label.Text = L.Arabic ? ar : en;
            _texts[label] = (en, ar);
            return label;
        }

        private static PanelContainer Panel(Color bg, Color border)
        {
            var p = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = border,
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2,
            };
            p.AddThemeStyleboxOverride("panel", style);
            return p;
        }

        private static void StyleBar(ProgressBar bar, Color color)
        {
            var bg = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.11f, 0.15f, 0.95f),
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            };
            var fill = new StyleBoxFlat
            {
                BgColor = color,
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            };
            bar.AddThemeStyleboxOverride("background", bg);
            bar.AddThemeStyleboxOverride("fill", fill);
        }
    }

    /// <summary>
    /// Helmet presentation for the first-person camera: a dark visor rim, a faint glass
    /// reflection and a functional crosshair that matches the interaction ray.
    /// </summary>
    public partial class VisorOverlay : Control
    {
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);
            Resized += QueueRedraw;
        }

        public override void _Draw()
        {
            Vector2 size = Size;
            if (size.X < 32 || size.Y < 32) return;

            var rim = new Color(0.01f, 0.025f, 0.04f, 0.66f);
            float band = Mathf.Max(16f, size.Y * 0.03f);
            DrawRect(new Rect2(0, 0, size.X, band), rim);
            DrawRect(new Rect2(0, size.Y - band, size.X, band), rim);
            DrawRect(new Rect2(0, 0, band, size.Y), rim);
            DrawRect(new Rect2(size.X - band, 0, band, size.Y), rim);

            var glass = new Color(0.35f, 0.7f, 1f, 0.07f);
            DrawRect(new Rect2(0, band, size.X, 2), glass);
            DrawRect(new Rect2(band, 0, 2, size.Y), glass);

            Vector2 center = new Vector2(size.X * 0.5f, size.Y * 0.5f);
            var mark = new Color(0.72f, 0.95f, 1f, 0.82f);
            DrawCircle(center, 2.0f, mark);
            DrawLine(center + new Vector2(-13, 0), center + new Vector2(-5, 0), mark, 1.5f);
            DrawLine(center + new Vector2(5, 0), center + new Vector2(13, 0), mark, 1.5f);
            DrawLine(center + new Vector2(0, -13), center + new Vector2(0, -5), mark, 1.5f);
            DrawLine(center + new Vector2(0, 5), center + new Vector2(0, 13), mark, 1.5f);
        }
    }
}
