using System;
using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Save;
using LunaQuest.Core.Sim;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Entry point and glue. It owns the simulation, the world, the player, the HUD and the
    /// voice pack, and it is the only place that knows about save files and input actions.
    /// Everything that matters (resources, incidents, learning) lives in the shared core.
    /// </summary>
    public partial class Main : Node3D
    {
        public static Main? Instance { get; private set; }

        public ColonySimulation Sim = null!;
        public WorldBuilder World = null!;
        public PlayerController Player = null!;
        public Hud Hud = null!;
        public VoicePlayer Voice = null!;
        public bool Started { get; private set; }

        private const string SavePath = "user://lunaquest-save.json";
        private double _saveTimer;
        private int _lastSavedDay = 1;
        private double _screenshotTimer = -1;
        private string _screenshotPath = "";

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            DisplayServer.WindowSetTitle("Luna-Quest — The Living Moon Colony");

            L.Arabic = false;
            L.ArabicFont = SafeLoadFont("res://assets/noto-sans-arabic.woff");
            L.LatinFont = SafeLoadFont("res://assets/noto-sans-latin.woff");

            SetupInput();

            ContentDatabase content = LoadContent();
            Sim = new ColonySimulation(content);
            // A new mission starts at 06:00 so the first shift, the first meal and the first
            // maintenance round are all ahead of the player rather than behind them.
            Sim.Clock.LoadFrom(6.0 * 60.0);
            _lastSavedDay = Sim.Clock.Day;

            World = new WorldBuilder { Name = "Colony" };
            World.ProcessMode = ProcessModeEnum.Pausable;
            AddChild(World);
            World.Build(this, Sim);

            Voice = new VoicePlayer { Name = "Voice", ProcessMode = ProcessModeEnum.Pausable };
            AddChild(Voice);

            Player = new PlayerController { Name = "Commander", ProcessMode = ProcessModeEnum.Pausable };
            AddChild(Player);
            Player.GlobalPosition = new Vector3(0, 0.1f, 23);
            Player.Visual.AddChild(World.CreateAstronaut(1.78f));
            Player.InputLocked = true;

            Hud = new Hud { Name = "Hud", ProcessMode = ProcessModeEnum.Always };
            AddChild(Hud);
            Hud.Bind(this);
            Voice.Bind(Hud);

            Hud.UpdateState();
            GD.Print($"Luna-Quest ready: {content.Buildings.Count} buildings, {content.Npcs.Count} crew, {content.Incidents.Count} incident rules.");

            HandleCommandLine();
        }

        /// <summary>
        /// Developer helpers: `--autostart` skips the name overlay, `--screenshot=&lt;path&gt;`
        /// renders one frame after a short settle time and quits. They are inert in normal play.
        /// </summary>
        private void HandleCommandLine()
        {
            bool autoStart = false;
            bool portrait = false;
            bool firstPerson = false;
            bool voiceCheck = false;
            bool voiceDemo = false;
            string? testDialogueNpc = null;
            Vector3? spawn = null;
            float? lookYaw = null, lookPitch = null;
            var args = new List<string>(OS.GetCmdlineArgs());
            args.AddRange(OS.GetCmdlineUserArgs());
            foreach (var arg in args)
            {
                if (arg == "--autostart") autoStart = true;
                else if (arg == "--portrait") portrait = true;
                else if (arg == "--first-person") firstPerson = true;
                else if (arg == "--voice-check") voiceCheck = true;
                else if (arg == "--voice-demo") voiceDemo = true;
                else if (arg.StartsWith("--spawn=", StringComparison.Ordinal))
                {
                    var parts = arg.Substring("--spawn=".Length).Split(',');
                    if (parts.Length >= 2 &&
                        float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                        spawn = new Vector3(x, 0.4f, z);
                }
                else if (arg.StartsWith("--look=", StringComparison.Ordinal))
                {
                    var parts = arg.Substring("--look=".Length).Split(',');
                    if (parts.Length >= 1 &&
                        float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float yaw))
                        lookYaw = yaw;
                    if (parts.Length >= 2 &&
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pitch))
                        lookPitch = pitch;
                }
                else if (arg.StartsWith("--screenshot=", StringComparison.Ordinal))
                {
                    _screenshotPath = arg.Substring("--screenshot=".Length);
                    _screenshotTimer = 4.0;
                }
                else if (arg.StartsWith("--test-dialogue=", StringComparison.Ordinal))
                    testDialogueNpc = arg.Substring("--test-dialogue=".Length);
                else if (arg.StartsWith("--shot-delay=", StringComparison.Ordinal))
                {
                    if (double.TryParse(arg.Substring("--shot-delay=".Length), out double d))
                        _screenshotTimer = d;
                }
            }
            if (spawn.HasValue) Player.GlobalPosition = spawn.Value;
            if (lookYaw.HasValue || lookPitch.HasValue)
                Player.ResetLook(lookYaw ?? 0f, lookPitch ?? -0.12f);
            if (autoStart) CallDeferred(nameof(AutoStart));
            if (firstPerson) CallDeferred(nameof(FirstPersonView));
            if (portrait) CallDeferred(nameof(Portrait));
            if (voiceCheck) CallDeferred(nameof(VoicePackCheck));
            if (voiceDemo) CallDeferred(nameof(VoiceDemo));
            if (!string.IsNullOrEmpty(testDialogueNpc)) CallDeferred(nameof(TestDialogue), testDialogueNpc);
        }

        private void TestDialogue(string npcId)
        {
            AutoStart();
            var npc = Sim.Content.Npc(npcId) ?? Sim.Content.Npcs[0];
            OpenDialogue(npc);
        }

        private void VoiceDemo()
        {
            var profile = Sim.Content.Npcs.Find(n => n.Id == "npc_safaa") ?? Sim.Content.Npcs[0];
            Voice.Speak(profile, true);
            GD.Print($"Voice demo: {profile.NameEn} greeting, playing={Voice.IsPlaying}.");
        }

        private void VoicePackCheck()
        {
            var (total, problems) = Voice.VerifyPack(Sim.Content.Dialogue);
            GD.Print($"Voice pack: {total} lines, {problems.Count} problem(s).");
            foreach (string problem in problems) GD.Print($"  {problem}");
            GetTree().Quit(problems.Count == 0 ? 0 : 1);
        }

        private void AutoStart() => BeginGame("Amara Okafor");

        private void FirstPersonView() => Player.SetThirdPerson(false, false);

        private void Portrait() => Player.PortraitCamera();

        public override void _Process(double delta)
        {
            if (Sim == null) return;

            if (Started && !GetTree().Paused)
            {
                var events = Sim.Step(delta);
                foreach (var e in events) Deliver(e);

                // Autosave once per mission day, so a long session never loses a day of work.
                if (Sim.Clock.Day != _lastSavedDay)
                {
                    _lastSavedDay = Sim.Clock.Day;
                    SaveGame(false);
                }
                _saveTimer += delta;
                if (_saveTimer > 30.0) { _saveTimer = 0; }
            }

            Hud.UpdateState();

            if (_screenshotTimer > 0)
            {
                _screenshotTimer -= delta;
                if (_screenshotTimer <= 0) CaptureAndQuit();
            }
        }

        private void CaptureAndQuit()
        {
            try
            {
                var image = GetViewport().GetTexture().GetImage();
                string path = _screenshotPath;
                if (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal))
                    path = ProjectSettings.GlobalizePath(path);
                var error = image.SavePng(path);
                GD.Print(error == Error.Ok ? $"Screenshot saved: {path}" : $"Screenshot failed: {error}");
            }
            catch (Exception ex)
            {
                GD.PushError($"Screenshot failed: {ex.Message}");
            }
            GetTree().Quit();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Started)
            {
                if (@event.IsActionPressed("load_game")) LoadGame();
                return;
            }

            if (@event.IsActionPressed("ui_cancel"))
            {
                HandleCancel();
                return;
            }

            if (GetTree().Paused) return;

            if (@event.IsActionPressed("journal")) Hud.ToggleJournal();
            if (@event.IsActionPressed("resources")) Hud.TogglePlants();
            if (@event.IsActionPressed("help")) Hud.ToggleHelp();
            if (@event.IsActionPressed("language")) ToggleLanguage();
            if (@event.IsActionPressed("save_game")) SaveGame(true);
            if (@event.IsActionPressed("load_game")) LoadGame();

            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed &&
                Input.MouseMode == Input.MouseModeEnum.Visible && !Hud.AnyOverlayVisible)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                Player.InputLocked = false;
            }
        }

        /// <summary>
        /// Esc is a single, predictable control: close a panel, recapture a released mouse, pause,
        /// or resume. It never leaves the player unlocked without telling them.
        /// </summary>
        private void HandleCancel()
        {
            if (Hud.PauseVisible) { ResumeGame(); return; }
            if (Hud.DialogueVisible) { CloseDialogue(); return; }
            if (Hud.AnyOverlayVisible)
            {
                Hud.CloseOverlays();
                Input.MouseMode = Input.MouseModeEnum.Captured;
                return;
            }
            if (Input.MouseMode != Input.MouseModeEnum.Captured) ResumeGame();
            else PauseGame();
        }

        public void PauseGame()
        {
            if (!Started || GetTree().Paused) return;
            GetTree().Paused = true;
            Player.InputLocked = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            Hud.ShowPause(true);
            Hud.SetPrompt("");
        }

        public void ResumeGame()
        {
            if (!Started) return;
            GetTree().Paused = false;
            Hud.ShowPause(false);
            Player.InputLocked = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        // ---- game flow -------------------------------------------------------

        public void BeginGame(string playerName)
        {
            if (Started) return;
            Sim.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Amara Okafor" : playerName.Trim();
            Started = true;
            GetTree().Paused = false;
            Hud.HideStart();
            Hud.ShowPause(false);
            Player.InputLocked = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Hud.ShowToast(L.T("Suit link nominal. The colony is yours.",
                "رابط البدلة سليم. المستعمرة مسؤوليتك."), 5.0);
            Hud.ShowSubtitle(L.T($"{Sim.PlayerName}, you are on console. Walk the colony and read the numbers.",
                $"{Sim.PlayerName}، أنت على الكونسول. تجوّل في المستعمرة واقرأ الأرقام."), 7.0);
        }

        public void DoInteract(Interactable it)
        {
            if (!Started || it == null) return;

            if (!string.IsNullOrEmpty(it.NpcId))
            {
                var profile = Sim.Content.Npc(it.NpcId);
                if (profile != null) OpenDialogue(profile);
                return;
            }

            bool ok = Sim.Interact(it.BuildingId, it.InteractionId, out string en, out string ar);
            string message = L.Arabic ? ar : en;
            Hud.ShowToast(message, ok ? 4.0 : 3.0);
            Hud.ShowSubtitle(message, 4.5);

            // The crew member who owns this building reacts to work done in it.
            var owner = FindOwnerAt(it.BuildingId);
            if (ok && owner != null)
            {
                Sim.Npcs.AddTrust(owner.Id, 0.02);
                Sim.Npcs.Remember(owner.Id, "worked_with_player");
            }
            else if (!ok && owner != null)
            {
                // Observation stations are still useful. The core has already logged the
                // reading in the journal; the owner acknowledges it.
                Voice.Speak(owner, false);
            }
        }

        /// <summary>
        /// A conversation locks the suit, releases the mouse and opens the topic panel. The
        /// colony keeps running: talking costs time, just like everything else.
        /// </summary>
        public void OpenDialogue(NpcProfile profile)
        {
            if (!Started || profile == null) return;
            Sim.Npcs.AddTrust(profile.Id, 0.01);
            Hud.OpenDialogue(profile, Sim.Content.TopicsFor(profile.Id));
            Player.InputLocked = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            Voice.Speak(profile, true);

            string line = L.Arabic ? profile.GreetingAr : profile.GreetingEn;
            if (string.IsNullOrEmpty(line))
                line = L.Arabic ? profile.StorySeedAr : profile.StorySeedEn;
            if (!string.IsNullOrEmpty(line))
                Hud.ShowSubtitle($"{L.T(profile.DisplayNameEn, profile.DisplayNameAr)}: {line}", 7.0);
        }

        public void AskNpcTopic(DialogueTopic topic)
        {
            if (!Started || topic == null) return;
            Voice.SpeakTopic(topic);
            Hud.SetDialogueTopic(topic);
            Sim.Npcs.AddTrust(topic.NpcId, 0.005);
            Sim.Npcs.Remember(topic.NpcId, "talked_" + topic.Id);
        }

        public void CloseDialogue()
        {
            Hud.CloseDialogue();
            if (!Started) return;
            Player.InputLocked = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        private NpcProfile? FindOwnerAt(string buildingId)
        {
            foreach (var p in Sim.Content.Npcs)
                if (p.LocationId == buildingId) return p;
            return null;
        }

        public void ToggleLanguage() => SetLanguage(!L.Arabic);

        public void SetLanguage(bool arabic)
        {
            if (L.Arabic == arabic) return;
            L.Arabic = arabic;
            Sim.Language = arabic ? "ar" : "en";
            Hud.RefreshLanguage();
            World.RefreshLanguage();
            Hud.ShowToast(L.T("Language: English", "اللغة: العربية"), 2.5);
        }

        // ---- save / load -----------------------------------------------------

        public void SaveGame(bool announce)
        {
            try
            {
                var data = SaveData.Capture(Sim);
                data.Extras["engine"] = "godot";
                data.Extras["started"] = Started;
                data.Extras["playerX"] = Player.GlobalPosition.X;
                data.Extras["playerY"] = Player.GlobalPosition.Y;
                data.Extras["playerZ"] = Player.GlobalPosition.Z;
                data.Extras["thirdPerson"] = Player.ThirdPerson;
                data.Extras["yaw"] = Player.Yaw;
                data.Extras["pitch"] = Player.Pitch;

                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    if (announce) Hud.ShowToast(L.T("Save failed: cannot open the save file.",
                        "فشل الحفظ: تعذر فتح ملف الحفظ."), 4);
                    return;
                }
                file.StoreString(data.ToJson());
                if (announce) Hud.ShowToast(L.T("Colony saved.", "حُفظت المستعمرة."), 2.5);
            }
            catch (Exception ex)
            {
                GD.PushError($"Save failed: {ex.Message}");
                if (announce) Hud.ShowToast(L.T("Save failed.", "فشل الحفظ."), 4);
            }
        }

        public void LoadGame()
        {
            if (!FileAccess.FileExists(SavePath))
            {
                Hud.ShowToast(L.T("No save file yet. The colony starts when you do.",
                    "لا يوجد ملف حفظ بعد. تبدأ المستعمرة عندما تبدأ أنت."), 4);
                return;
            }
            try
            {
                string text = FileAccess.GetFileAsString(SavePath);
                var data = SaveData.FromJson(text);
                data.Apply(Sim);

                float x = 0f, y = 0.2f, z = 23f;
                if (data.Extras.TryGetValue("playerX", out var px))
                    x = (float)LunaQuest.Core.Content.Json.Num(px);
                if (data.Extras.TryGetValue("playerY", out var py))
                    y = (float)LunaQuest.Core.Content.Json.Num(py, 0.2);
                if (data.Extras.TryGetValue("playerZ", out var pz))
                    z = (float)LunaQuest.Core.Content.Json.Num(pz);
                Player.GlobalPosition = new Vector3(x, y, z);

                bool thirdPerson = true;
                if (data.Extras.TryGetValue("thirdPerson", out var tp))
                    thirdPerson = LunaQuest.Core.Content.Json.Bool(tp, true);
                Player.SetThirdPerson(thirdPerson, false);

                float yaw = 0f, pitch = -0.12f;
                if (data.Extras.TryGetValue("yaw", out var pyaw))
                    yaw = (float)LunaQuest.Core.Content.Json.Num(pyaw);
                if (data.Extras.TryGetValue("pitch", out var ppitch))
                    pitch = (float)LunaQuest.Core.Content.Json.Num(ppitch, -0.12);
                Player.ResetLook(yaw, pitch);

                // Guard against save files written under an older, taller layout: an obviously
                // invalid height is clamped rather than dropping the player out of the world.
                if (y < -1f) Player.GlobalPosition = new Vector3(x, 0.4f, z);

                L.Arabic = Sim.Language == "ar";
                Hud.RefreshLanguage();
                World.RefreshLanguage();
                Started = true;
                GetTree().Paused = false;
                Hud.HideStart();
                Hud.ShowPause(false);
                Player.InputLocked = false;
                Input.MouseMode = Input.MouseModeEnum.Captured;
                _lastSavedDay = Sim.Clock.Day;
                Hud.ShowToast(L.T("Colony restored.", "استُعيدت المستعمرة."), 3);
            }
            catch (Exception ex)
            {
                GD.PushError($"Load failed: {ex.Message}");
                Hud.ShowToast(L.T("Save file is not readable.", "ملف الحفظ غير قابل للقراءة."), 4);
            }
        }

        // ---- events ----------------------------------------------------------

        private readonly HashSet<string> _announced = new HashSet<string>();

        private void Deliver(SimEvent e)
        {
            Hud.PushEvent(e);

            // One spoken line per event family per 30 game minutes. The subtitle carries the
            // text when the voice pack has no matching bark.
            if (e.Kind is SimEventKind.Critical or SimEventKind.Warning or SimEventKind.Mission)
            {
                string key = $"{e.Id}:{Math.Floor(Sim.Clock.TotalMinutes / 30)}";
                if (_announced.Add(key))
                {
                    Voice.SpeakEvent(e, L.Arabic);
                    if (e.Kind == SimEventKind.Critical)
                        Hud.ShowSubtitle(L.Arabic ? e.BodyAr : e.BodyEn, 6.0);
                }
            }
        }

        // ---- input -----------------------------------------------------------

        private static void SetupInput()
        {
            AddKey("move_forward", Key.W);
            AddKey("move_back", Key.S);
            AddKey("move_left", Key.A);
            AddKey("move_right", Key.D);
            AddKey("sprint", Key.Shift);
            AddKey("jump", Key.Space);
            AddKey("interact", Key.E);
            AddKey("toggle_view", Key.V);
            AddKey("journal", Key.J);
            AddKey("resources", Key.G);
            AddKey("help", Key.H);
            AddKey("language", Key.L);
            AddKey("save_game", Key.F5);
            AddKey("load_game", Key.F9);
        }

        private static void AddKey(string action, Key key)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            var ev = new InputEventKey { PhysicalKeycode = key };
            InputMap.ActionAddEvent(action, ev);
        }

        // ---- loading ---------------------------------------------------------

        private static Font SafeLoadFont(string path)
        {
            if (ResourceLoader.Exists(path))
            {
                var font = GD.Load<Font>(path);
                if (font != null) return font;
            }
            return ThemeDB.FallbackFont;
        }

        private static ContentDatabase LoadContent()
        {
            string Load(string name)
            {
                string path = $"res://data/{name}";
                if (!FileAccess.FileExists(path))
                    throw new InvalidOperationException($"Missing content file {path}");
                return FileAccess.GetFileAsString(path);
            }

            return ContentDatabase.LoadFromTexts(
                Load("buildings.json"),
                Load("npcs.json"),
                Load("incidents.json"),
                Load("crops.json"),
                Load("learning.json"),
                Load("research.json"),
                Load("dialogue.json"));
        }
    }
}
