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
        public RoverController Rover = null!;
        public Hud Hud = null!;
        public VoicePlayer Voice = null!;
        /// <summary>The live settings view the game reads from (loaded at boot, saved on change).</summary>
        public GameSettings.SettingsData LiveSettings = GameSettings.Defaults();
        public bool Started { get; private set; }

        private const string SavePath = "user://lunaquest-save.json";
        private double _saveTimer;
        private int _lastSavedDay = 1;
        private double _screenshotTimer = -1;
        private IntroPlayer? _intro;
        private Camera3D? _menuCam;
        private double _menuT;
        private AudioStreamPlayer? _music;
        private bool _musicOn = true;
        private const string IntroSeenPath = "user://intro_seen";
        private const string MusicPath = "res://assets/audio/music/lunaquest-score.mp3";
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
            Player.Visual.AddChild(World.CreateAstronaut(1.75f));
            Player.InputLocked = true;

            Rover = new RoverController { Name = "CrewRover", ProcessMode = ProcessModeEnum.Pausable };
            AddChild(Rover);
            Rover.GlobalPosition = new Vector3(-11f, 0.05f, -28f);
            Rover.Rotation = new Vector3(0, 0.4f, 0);
            Rover.Settle();

            Hud = new Hud { Name = "Hud", ProcessMode = ProcessModeEnum.Always };
            AddChild(Hud);
            Hud.Bind(this);
            Voice.Bind(Hud);

            LiveSettings = GameSettings.Load();
            ApplySettings(LiveSettings);

            Hud.UpdateState();
            GD.Print($"Luna-Quest ready: {content.Buildings.Count} buildings, {content.Npcs.Count} crew, {content.Incidents.Count} incident rules.");

            CreateMenuCamera();
            CreateMusic();
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
            bool cinematic = false;
            bool noIntro = false;
            bool roverTest = false;
            bool drive = false;
            bool noBoard = false;
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
                else if (arg == "--cinematic") cinematic = true;
                else if (arg == "--no-intro") noIntro = true;
                else if (arg == "--rover-test") roverTest = true;
                else if (arg == "--drive") drive = true;
                else if (arg == "--no-board") noBoard = true;
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
            // `--look` normally applies at once, but `--portrait` sets the portrait camera up
            // later (deferred), and its own rotation would swallow the requested direction.
            // With both flags the look is therefore applied after the portrait — that
            // combination is how tools/capture-screenshots.sh gets the close back view it
            // crops for astronaut-detail.png.
            bool lookAfterPortrait = portrait && (lookYaw.HasValue || lookPitch.HasValue);
            if (!lookAfterPortrait && (lookYaw.HasValue || lookPitch.HasValue))
                Player.ResetLook(lookYaw ?? 0f, lookPitch ?? -0.12f);
            if (autoStart) CallDeferred(nameof(AutoStart));
            if (drive) CallDeferred(nameof(DriveForCapture));
            if (firstPerson) CallDeferred(nameof(FirstPersonView));
            if (portrait) CallDeferred(nameof(Portrait));
            if (lookAfterPortrait)
                CallDeferred(nameof(ApplyLookDeferred), lookYaw ?? 0f, lookPitch ?? -0.12f);
            if (noBoard) CallDeferred(nameof(HideBoardForCapture));
            if (voiceCheck) CallDeferred(nameof(VoicePackCheck));
            if (voiceDemo) CallDeferred(nameof(VoiceDemo));
            if (cinematic) CallDeferred(nameof(StartCinematic));
            bool scripted = autoStart || cinematic || voiceCheck || voiceDemo || portrait || firstPerson ||
                            roverTest || drive || noBoard ||
                            _screenshotTimer > 0 || !string.IsNullOrEmpty(testDialogueNpc);
            if (cinematic) { _menuCam?.QueueFree(); _menuCam = null; _music?.Stop(); }
            // The film plays on the first launch; afterwards the menu offers it on demand.
            if (!scripted && !noIntro && !FileAccess.FileExists(IntroSeenPath)) CallDeferred(nameof(PlayIntro));
            if (!string.IsNullOrEmpty(testDialogueNpc)) CallDeferred(nameof(TestDialogue), testDialogueNpc);
            if (roverTest) CallDeferred(nameof(RunRoverTest));
        }

        // ---- intro film, menu backdrop and music ------------------------------

        /// <summary>Plays the opening film. Skippable; remembered so it is not forced twice.</summary>
        public void PlayIntro()
        {
            if (_intro != null || Started) return;
            if (!IntroPlayer.IsAvailable()) return;
            _intro = new IntroPlayer();
            AddChild(_intro);
            if (_music != null) _music.StreamPaused = true;
            Voice?.SetAmbientPaused(true);
            _intro.Finished += OnIntroFinished;
        }

        private void OnIntroFinished()
        {
            using (var f = FileAccess.Open(IntroSeenPath, FileAccess.ModeFlags.Write))
                f?.StoreString("1");
            _intro?.QueueFree();
            _intro = null;
            if (_music != null) _music.StreamPaused = false;
            Voice?.SetAmbientPaused(false);
        }

        /// <summary>
        /// Behind the main menu the colony is shown from a slow orbit instead of a dark void, so
        /// the first screen already shows the actual game.
        /// </summary>
        private void CreateMenuCamera()
        {
            _menuCam = new Camera3D { Name = "MenuCamera", Fov = 52f, Near = 0.2f, Far = 4000f, Current = true };
            AddChild(_menuCam);
            UpdateMenuCamera(0);
        }

        private void UpdateMenuCamera(double delta)
        {
            if (_menuCam == null || !IsInstanceValid(_menuCam)) return;
            _menuT += delta;
            double a = 2.35 + _menuT * 0.030;
            var pos = new Vector3((float)(Math.Sin(a) * 118.0), 13f + (float)(Math.Sin(_menuT * 0.11) * 3.0), 8f + (float)(Math.Cos(a) * 118.0));
            _menuCam.GlobalPosition = pos;
            _menuCam.LookAt(new Vector3(0, 4.5f, 8f), Vector3.Up);
        }

        private void CreateMusic()
        {
            if (!ResourceLoader.Exists(MusicPath)) return;
            var stream = GD.Load<AudioStream>(MusicPath);
            if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
            _music = new AudioStreamPlayer { Name = "Music", Stream = stream, Bus = "Master", VolumeDb = -15f, ProcessMode = ProcessModeEnum.Always };
            AddChild(_music);
            _music.Play();
        }

        public void ToggleMusic()
        {
            if (_music == null) return;
            _musicOn = !_musicOn;
            _music.VolumeDb = _musicOn ? -15f : -80f;
            Hud?.ShowToast(_musicOn ? L.T("Music on", "الموسيقى تعمل") : L.T("Music off", "الموسيقى متوقفة"), 1.6);
        }

        private void StartCinematic()
        {
            var cine = new Cinematic { Name = "Cinematic" };
            AddChild(cine);
            cine.Begin(this);
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

        /// <summary>
        /// `--rover-test`: drive the T5 rover checklist headlessly and exit non-zero if anything
        /// fails. CI runs this on every push (see .github/workflows/godot-dotnet.yml).
        /// </summary>
        private void RunRoverTest()
        {
            bool passed = new RoverAudit().Run(this);
            GetTree().Quit(passed ? 0 : 1);
        }

        /// <summary>
        /// Start the mission over: a fresh simulation, the commander back at the spawn and the
        /// rover back on its apron with full power and no milestones.
        /// </summary>
        public void RestartMission()
        {
            string commander = Sim.PlayerName;
            Sim = new LunaQuest.Core.Sim.ColonySimulation(Sim.Content);
            Sim.PlayerName = commander;
            Started = true;
            _lastSavedDay = Sim.Clock.Day;
            if (Rover != null)
            {
                if (Rover.Occupied && Player != null) Rover.Exit(Player);
                Rover.GlobalPosition = new Vector3(-11f, 0.05f, -28f);
                Rover.Rotation = new Vector3(0, 0.4f, 0);
                Rover.BatteryKwh = RoverController.BatteryKwhMax;
                Rover.Speed = 0f;
                Rover.Steer = 0f;
                Rover.Spin = 0f;
                Rover.Odometer = 0f;
                Rover.HasDriven = false;
                Rover.ReachedIce = false;
                Rover.Settle();
            }
            if (Player != null)
            {
                Player.GlobalPosition = new Vector3(0, 0.1f, 23);
                Player.InputLocked = false;
            }
            Hud?.ShowSubtitle(L.T("New mission. The colony is reset.", "مهمة جديدة. أعيد ضبط المستعمرة."), 5.0);
        }

        private void FirstPersonView() => Player.SetThirdPerson(false, false);

        private void Portrait() => Player.PortraitCamera();

        /// <summary>
        /// `--drive`: take the crew rover's wheel for a screenshot run (tools/capture-screenshots.sh).
        /// A capture has no hands on the keyboard, so once the seat is taken the flag supplies
        /// the pedal through <see cref="RoverController.CaptureThrottle"/> until the frame is
        /// saved. Outside a capture the throttle stays zero and the keys win as before.
        /// </summary>
        private void DriveForCapture()
        {
            TryRoverInteraction();
            RoverController.CaptureThrottle = Rover != null && Rover.Occupied ? 1f : 0f;
        }

        /// <summary>
        /// The look half of a `--portrait --look=...` capture (see HandleCommandLine): applied
        /// after the portrait camera so the arm orbits to the requested direction instead of
        /// the portrait's own rotation winning. Inert without both flags.
        /// </summary>
        private void ApplyLookDeferred(float yaw, float pitch) => Player.ResetLook(yaw, pitch);

        /// <summary>
        /// `--no-board`: keep the shift task board off the screen for one capture. The board
        /// sits in the centre of the frame — exactly where a portrait's visor lands — so
        /// tools/capture-screenshots.sh takes it out of the way for the portrait and detail
        /// shots. Every other HUD element stays; nothing changes in normal play, where the
        /// flag is never passed.
        /// </summary>
        private void HideBoardForCapture()
        {
            // BoardPanel is the styled container around BoardBox — hiding the panel takes the
            // background and the rows with it.
            if (Hud?.BoardPanel != null) Hud.BoardPanel.Visible = false;
        }

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
                TickHold(delta);
                TickEva(delta);

                // F8: traverse warning at 90 m from centre; the rim holds at 118 m.

                float rim = new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z).Length();

                if (rim > 90f && Time.GetTicksMsec() - _traverseWarnMsec > 9000)

                {

                    _traverseWarnMsec = Time.GetTicksMsec();

                    Hud.ShowSubtitle(L.T("Survey rim approaching. Beyond 110 m is unsurveyed.",

                        "خط الحدود يقترب. خارج 110 متر غير نشط."), 8.0);

                }

                if (rim > 118f)

                {

                    Vector2 v = new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z).Normalized() * 118f;

                    Player.GlobalPosition = new Vector3(v.X, Player.GlobalPosition.Y, v.Y);

                }
            }

            Hud.UpdateState();
            UpdateMenuCamera(delta);

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
            if (@event is InputEventKey mk && mk.Pressed && !mk.Echo && mk.Keycode == Key.M && _intro == null)
            {
                ToggleMusic();
                return;
            }
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
            _menuCam?.QueueFree();
            _menuCam = null;
            Player.Camera.Current = true;
            Hud.HideStart();
            Hud.ShowPause(false);
            Player.InputLocked = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Hud.ShowToast(L.T("Suit link nominal. The colony is yours.",
                "رابط البدلة سليم. المستعمرة مسؤوليتك."), 5.0);
            Hud.ShowSubtitle(L.T($"{Sim.PlayerName}, you are on console. Walk the colony and read the numbers.",
                $"{Sim.PlayerName}، أنت على الكونسول. تجوّل في المستعمرة واقرأ الأرقام."), 7.0);
        }

        // F6: hold-E physical jobs. Durations in real seconds; an entry of zero means the
        // console is instant (reading, relays, dialogue).
        private readonly Dictionary<string, float> _holdTimes = new()
        {
            ["clean_arrays"] = 2.5f,
            ["dust_removal"] = 2.5f,
            ["eva_prep"] = 2.5f,
            ["clean_filter"] = 3.0f,
            ["replace_scrubber"] = 3.0f,
            ["collect_sample"] = 3.5f,
            ["cache_log"] = 1.5f,
            ["cargo_crate"] = 1.0f,
        };
        private Interactable? _holdStation;
        private float _holdTime;
        private float _holdNeed = 1f;
        private ulong _traverseWarnMsec;

        public void DoInteract(Interactable it)
        {
            if (!Started || it == null) return;

            if (!string.IsNullOrEmpty(it.NpcId))
            {
                var profile = Sim.Content.Npc(it.NpcId);
                if (profile != null) OpenDialogue(profile);
                return;
            }

            if (_holdTimes.TryGetValue(it.InteractionId, out float need))
            {
                _holdStation = it;
                _holdTime = 0f;
                _holdNeed = need;
                Hud.SetPrompt("Hold E… 0%");
                return;
            }

            ExecuteInteract(it);
        }

        /// <summary>The console actually runs here: hold complete or instant station.</summary>
        public void ExecuteInteract(Interactable it)
        {
            bool ok = Sim.Interact(it.BuildingId, it.InteractionId, out string en, out string ar);
            string message = L.Arabic ? ar : en;
            Hud.ShowToast(message, ok ? 4.0 : 3.0);
            Hud.ShowSubtitle(message, 4.5);
            Hud.SetPrompt("");
            if (ok) Voice.PlayChirp();

            // The crew member who owns this building reacts to work done in it.
            var owner = FindOwnerAt(it.BuildingId);
            if (ok && owner != null)
            {
                Sim.Npcs.AddTrust(owner.Id, 0.02);
                Sim.Npcs.Remember(owner.Id, "worked_with_player");
            }
            else if (!ok && owner != null && it.InteractionId != "cargo_crate")
            {
                // Observation stations are still useful. The core has already logged the
                // reading in the journal; the owner acknowledges it.
                Voice.Speak(owner, false);
            }
        }

        /// <summary>Tick the hold-E in progress; cancels on release, completes at 100%.</summary>
        private void TickHold(double delta)
        {
            if (_holdStation == null) return;
            if (Input.IsActionPressed("interact"))
            {
                _holdTime += (float)delta;
                int pct = Mathf.Clamp((int)(100f * _holdTime / _holdNeed), 0, 100);
                Hud.SetPrompt($"Hold E… {pct}%");
                if (_holdTime >= _holdNeed)
                {
                    var it = _holdStation;
                    _holdStation = null;
                    ExecuteInteract(it);
                }
                return;
            }
            _holdStation = null;
            Hud.SetPrompt("");
            Hud.ShowSubtitle(L.T("Hold released. The job waits.",
                "تركت العمل. ينتظرك عند المحطة."), 3.0);
        }

        private bool InsideBuilding(Vector3 pos)
        {
            foreach (var b in Sim.Content.Buildings)
            {
                if (Mathf.Abs(pos.X - b.PositionX) < b.FootprintW * 0.5f + 0.5f
                    && Mathf.Abs(pos.Z - b.PositionZ) < b.FootprintD * 0.5f + 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>F6 per frame: EVA reserve drains outside, recharges indoors; the cargo clock runs down.</summary>
        private void TickEva(double delta)
        {
            if (!Started || GetTree().Paused || Player == null) return;
            int code = Sim.TickEvaEnergy(InsideBuilding(Player.GlobalPosition), delta);
            if (code != 0)
            {
                Hud.ShowSubtitle(code switch
                {
                    1 => L.T("EVA reserve below 25%. Plan the route home.", "احتياط EVA دون 25%. خطط للمسار."),
                    2 => L.T("Below 10% — turn back now.", "دون 10% — عد الآن."),
                    _ => L.T("EVA reserve empty. Charge the suit at the airlock.",
                        "نفد احتياط EVA. اشحن البدلة عند الأيرلوك."),
                }, 6.0);
            }
            Sim.TickCargo(delta);
        }

        /// <summary>
        /// A conversation locks the suit, releases the mouse and opens the topic panel. The
        /// colony keeps running: talking costs time, just like everything else.
        /// </summary>
        public void TryRoverInteraction()
        {
            if (!Started || Rover == null || Player == null) return;
            if (Rover.Occupied)
            {
                Rover.Exit(Player);
                Hud.ShowSubtitle(L.T("Out of the rover. It recharges on the garage apron.",
                    "خرجت من المركبة. هي تُشحن على أرضية المرآب."), 4.0);
                return;
            }
            if (Player.GlobalPosition.DistanceTo(Rover.GlobalPosition) < 4.2f)
            {
                Rover.Enter(Player);
                Hud.ShowSubtitle(L.T("At the wheel. W drive, A/D steer, Space brake, E to step out.",
                    "أنت على المقعد. W للسائق، A/D للتوجيه، Space للفرملة، E للخروج."), 6.0);
            }
        }

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

        /// <summary>Apply the settings panel's choices right now: save them and push them into
        /// the engine (camera, audio, window, voice) and the HUD (language, subtitles).</summary>
        public void ApplySettings(GameSettings.SettingsData data)
        {
            LiveSettings = data;
            GameSettings.Save(data);
            GameSettings.ApplyTo(data, Player, Voice);
            bool wantArabic = data.Language == "ar";
            if (L.Arabic != wantArabic) SetLanguage(wantArabic);
        }

        public void SetLanguage(bool arabic)
        {
            if (L.Arabic == arabic) return;
            L.Arabic = arabic;
            Sim.Language = arabic ? "ar" : "en";
            Hud.RefreshLanguage();
            World.RefreshLanguage();
            LiveSettings.Language = arabic ? "ar" : "en";
            GameSettings.Save(LiveSettings);
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
                if (Rover != null)
                {
                    data.Extras["roverX"] = Rover.GlobalPosition.X;
                    data.Extras["roverY"] = Rover.GlobalPosition.Y;
                    data.Extras["roverZ"] = Rover.GlobalPosition.Z;
                    data.Extras["roverYaw"] = Rover.Rotation.Y;
                    data.Extras["roverBattery"] = Rover.BatteryKwh;
                    data.Extras["roverOdometer"] = Rover.Odometer;
                    data.Extras["roverDriven"] = Rover.HasDriven;
                    data.Extras["roverIce"] = Rover.ReachedIce;
                    data.Extras["roverOccupied"] = Rover.Occupied;
                }

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
                if (Rover != null) Rover.Restore(data.Extras, Player);

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
                    if (e.Kind == SimEventKind.Critical && LiveSettings.Subtitles)
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
