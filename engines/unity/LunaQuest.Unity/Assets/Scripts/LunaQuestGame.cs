using System;
using System.Collections.Generic;
using System.IO;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Save;
using LunaQuest.Core.Sim;
using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// Unity entry point and glue. It owns the simulation, the procedural colony, the
    /// player, the HUD and the shared save contract. Every rule that matters lives in
    /// LunaQuest.Core, the same assembly the Godot project and the tests use.
    /// </summary>
    public sealed class LunaQuestGame : MonoBehaviour
    {
        public static LunaQuestGame Instance { get; private set; }

        public ColonySimulation Sim;
        public ColonyWorld World;
        public ColonyPlayer Player;
        public CrewRover Rover;
        public ColonyHud Hud;
        public VoicePlayer Voice;

        public bool Started { get; private set; }
        public string ContentError = "";

        private double _saveTimer;
        private double _traverseWarnAt;
        // F6: hold-E physical jobs (durations in real seconds; 0 = instant station).
        private static float HoldDurationFor(string id) => id switch
        {
            "clean_arrays" => 2.5f,
            "dust_removal" => 2.5f,
            "eva_prep" => 2.5f,
            "clean_filter" => 3.0f,
            "replace_scrubber" => 3.0f,
            "collect_sample" => 3.5f,
            "cache_log" => 1.5f,
            "cargo_crate" => 1.0f,
            _ => 0f,
        };
        private Station _holdStation;
        private float _holdTime;
        private float _holdNeed = 1f;

        private bool InsideBuilding(Vector3 pos)
        {
            foreach (var b in Sim.Content.Buildings)
            {
                if (Mathf.Abs(pos.x - b.PositionX) < b.FootprintW * 0.5f + 0.5f
                    && Mathf.Abs(pos.z - b.PositionZ) < b.FootprintD * 0.5f + 0.5f)
                    return true;
            }
            return false;
        }
        private int _lastSavedDay = 1;
        private readonly HashSet<string> _announced = new HashSet<string>();

        public bool InputActive =>
            Started && Cursor.lockState == CursorLockMode.Locked &&
            Hud != null && !Hud.OverlayOpen;

        private string SavePath => Path.Combine(Application.persistentDataPath, "lunaquest-save.json");

        private void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 130f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            L.Init();

            ContentDatabase content;
            try
            {
                content = ContentDatabase.LoadFromDirectory(Path.Combine(Application.streamingAssetsPath, "content"));
                if (content.Buildings.Count == 0)
                    throw new InvalidOperationException("content/buildings.json produced no buildings");
            }
            catch (Exception ex)
            {
                ContentError = $"Content could not be loaded: {ex.Message}";
                Debug.LogError(ContentError);
                content = new ContentDatabase();
            }

            Sim = new ColonySimulation(content);
            Sim.Clock.LoadFrom(6.0 * 60.0);
            _lastSavedDay = Sim.Clock.Day;
        }

        private void Start()
        {
            var worldGo = new GameObject("Colony");
            World = worldGo.AddComponent<ColonyWorld>();
            World.Init(this);
            World.Build();

            Player = ColonyPlayer.Create(this);
            Player.Teleport(new Vector3(0f, 0.15f, 23f));

            Rover = CrewRover.Create(this);
            Rover.transform.position = new Vector3(-11f, 0.05f, -28f);
            Rover.transform.rotation = Quaternion.Euler(0f, 0.4f * Mathf.Rad2Deg, 0f);

            Hud = new ColonyHud(this);
            Hud.Init();

            var voiceGo = new GameObject("Voice");
            Voice = voiceGo.AddComponent<VoicePlayer>();
            Voice.Bind(this);

            LiveSettings = SettingsStore.Load();
            ApplySettings(LiveSettings);

            Debug.Log($"Luna-Quest ready: {Sim.Content.Buildings.Count} buildings, " +
                      $"{Sim.Content.Npcs.Count} crew, {Sim.Content.Incidents.Count} incident rules.");
        }

        private void Update()
        {
            if (Sim == null) return;

            if (Started && !Paused)
            {
                var events = Sim.Step(Time.deltaTime);
                foreach (var e in events) Deliver(e);

                if (Sim.Clock.Day != _lastSavedDay)
                {
                    _lastSavedDay = Sim.Clock.Day;
                    SaveGame(false);
                }
                // F6: the EVA reserve drains outside and recharges indoors.
                int eva = Sim.TickEvaEnergy(InsideBuilding(Player.transform.position), Time.deltaTime);
                if (eva == 1)
                    Hud.ShowSubtitle(L.T("EVA reserve below 25%. Plan the route home.",
                        "احتياط EVA دون 25%. خطط للمسار."), 6f);
                else if (eva == 2)
                    Hud.ShowSubtitle(L.T("Below 10% — turn back now.", "دون 10% — عد الآن."), 6f);
                else if (eva == 3)
                    Hud.ShowSubtitle(L.T("EVA reserve empty. Charge the suit at the airlock.",
                        "نفد احتياط EVA. اشحن البدلة عند الأيرلوك."), 6f);
                Sim.TickCargo(Time.deltaTime);
                // F8: traverse warning at 90 m from centre; the rim holds at 118 m.
                if (Player != null)
                {
                    float rim = new Vector2(Player.transform.position.x, Player.transform.position.z).magnitude;
                    if (rim > 90f && Time.unscaledTimeAsDouble - _traverseWarnAt > 9.0)
                    {
                        _traverseWarnAt = Time.unscaledTimeAsDouble;
                        Hud.ShowSubtitle(L.T("Survey rim approaching. Beyond 110 m is unsurveyed.",
                            "خط الحدود يقترب. خارج 110 متر غير نشط."), 8f);
                    }
                    if (rim > 118f)
                    {
                        Vector2 v = new Vector2(Player.transform.position.x, Player.transform.position.z).normalized * 118f;
                        Player.Teleport(new Vector3(v.x, Player.transform.position.y, v.y));
                    }
                }

                _saveTimer += Time.deltaTime;
                if (_saveTimer > 30.0) _saveTimer = 0.0;
            }

            HandleKeys();

            if (Hud != null)
                Hud.Tick(Time.deltaTime);
        }

        private void OnGUI()
        {
            if (Hud != null)
                Hud.Draw();
        }

        /// <summary>The live settings view the game reads from (loaded at boot, saved on change).</summary>
        public SettingsStore.SettingsData LiveSettings = SettingsStore.Defaults();

        /// <summary>Save the settings panel's choices and push them into the engine right now.</summary>
        public void ApplySettings(SettingsStore.SettingsData data)
        {
            LiveSettings = data;
            SettingsStore.Save(data);
            SettingsStore.ApplyTo(data, this);
            bool wantArabic = data.Language == "ar";
            if (L.Arabic != wantArabic) SetLanguage(wantArabic);
        }

        public bool Paused { get; private set; }

        public void PauseGame()
        {
            if (!Started || Paused) return;
            Paused = true;
            Time.timeScale = 0f;
            UnlockCursor();
        }

        public void ResumeGame()
        {
            if (!Started) return;
            Paused = false;
            Time.timeScale = 1f;
            LockCursor();
        }

        private void HandleKeys()
        {
            if (Hud == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Paused) { ResumeGame(); return; }
                if (Hud.OverlayOpen)
                {
                    Hud.CloseOverlays();
                    UnlockCursor();
                }
                else if (Started)
                {
                    if (Cursor.lockState == CursorLockMode.Locked) PauseGame();
                    else LockCursor();
                }
                return;
            }

            if (!Started || Paused) return;

            if (Input.GetKeyDown(KeyCode.J)) { Hud.ToggleJournal(); UnlockCursor(); }
            if (Input.GetKeyDown(KeyCode.H)) { Hud.ToggleHelp(); UnlockCursor(); }
            if (Input.GetKeyDown(KeyCode.G)) { Hud.ToggleMath(); UnlockCursor(); }
            if (Input.GetKeyDown(KeyCode.L)) ToggleLanguage();
            if (Input.GetKeyDown(KeyCode.F5)) SaveGame(true);
            if (Input.GetKeyDown(KeyCode.F9)) LoadGame();

            if (_holdStation != null)
            {
                // A held job: complete at 100%, otherwise cancel on release.
                if (!InputActive)
                {
                    _holdStation = null;
                    if (Player != null) Player.HoldPrompt = "";
                }
                else if (Input.GetKey(KeyCode.E))
                {
                    _holdTime += Time.deltaTime;
                    float pct = Mathf.Clamp01(_holdTime / _holdNeed);
                    if (Player != null) Player.HoldPrompt = $"Hold E… {Mathf.RoundToInt(pct * 100f)}%";
                    if (_holdTime >= _holdNeed)
                    {
                        var station = _holdStation;
                        _holdStation = null;
                        if (Player != null) Player.HoldPrompt = "";
                        ExecuteInteract(station);
                    }
                }
                else
                {
                    _holdStation = null;
                    if (Player != null) Player.HoldPrompt = "";
                    Hud.ShowToast(L.T("Hold released. The job waits.",
                        "تركت العمل. ينتظرك عند المحطة."), 3f);
                }
            }
            else if (Input.GetKeyDown(KeyCode.E) && InputActive)
                Interact();

            if (Input.GetMouseButtonDown(0) && !Hud.OverlayOpen &&
                Cursor.lockState != CursorLockMode.Locked && Started)
                LockCursor();
        }

        public void BeginGame(string playerName)
        {
            if (Started) return;
            Sim.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Amara Okafor" : playerName.Trim();
            Started = true;
            Paused = false;
            Time.timeScale = 1f;
            LockCursor();
            Hud.ShowToast(L.T("Suit link nominal. The colony is yours.",
                "رابط البدلة سليم. المستعمرة مسؤوليتك."), 5f);
            Hud.ShowSubtitle(L.T($"{Sim.PlayerName}, you are on console. Walk the colony and read the numbers.",
                $"{Sim.PlayerName}، أنت على الكونسول. تجوّل في المستعمرة واقرأ الأرقام."), 8f);
        }

        /// <summary>
        /// Start the mission over with a fresh simulation and a rover back on its apron — the
        /// equivalent of the Godot editions' "restart mission" (used by the playtest, and by the
        /// end screen). Keeps the same colony and the same commander.
        /// </summary>
        public void RestartMission()
        {
            string commander = Sim.PlayerName;
            Sim = new ColonySimulation(Sim.Content);
            Sim.PlayerName = string.IsNullOrWhiteSpace(commander) ? "Amara Okafor" : commander;
            Started = true;
            Paused = false;
            Time.timeScale = 1f;
            _lastSavedDay = Sim.Clock.Day;
            if (Rover != null)
            {
                if (Rover.Occupied && Player != null) Rover.Exit(Player);
                Rover.transform.position = new Vector3(-11f, 0.05f, -28f);
                Rover.transform.rotation = Quaternion.Euler(0f, 0.4f * Mathf.Rad2Deg, 0f);
                Rover.BatteryKwh = CrewRover.BatteryKwhMax;
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
                Player.Teleport(new Vector3(0f, 0.4f, 23f));
                Player.SetDriving(false);
            }
            Hud?.ShowSubtitle(L.T("New mission. The colony is reset.", "مهمة جديدة. أعيد ضبط المستعمرة."), 5f);
        }

        private void Interact()
        {
            if (Player == null) return;

            if (Rover != null)
            {
                if (Rover.Occupied)
                {
                    Rover.Exit(Player);
                    Hud.ShowSubtitle(L.T("Out of the rover. It recharges on the garage apron.",
                        "خرجت من المركبة. هي تُشحن على أرضية المرآب."), 4f);
                    return;
                }
                if ((Player.transform.position - Rover.transform.position).sqrMagnitude < 4.2f * 4.2f)
                {
                    Rover.Enter(Player);
                    Hud.ShowSubtitle(L.T("At the wheel. W drive, A/D steer, Space brake, E to step out.",
                        "أنت على المقعد. W للسائق، A/D للتوجيه، Space للفرملة، E للخروج."), 6f);
                    return;
                }
            }

            if (Player.FocusCrew != null)
            {
                var crew = Player.FocusCrew;
                var profile = Sim.Content.Npc(crew.NpcId);
                if (profile != null) OpenDialogue(profile);
                return;
            }

            var station = Player.FocusStation;
            if (station == null) return;

            float need = HoldDurationFor(station.InteractionId);
            if (need > 0f)
            {
                _holdStation = station;
                _holdNeed = need;
                _holdTime = 0f;
                if (Player != null) Player.HoldPrompt = "Hold E… 0%";
                return;
            }

            ExecuteInteract(station);
        }

        private void ExecuteInteract(Station station)
        {
            bool ok = Sim.Interact(station.BuildingId, station.InteractionId,
                out string en, out string ar);
            string message = L.T(en, ar);
            Hud.ShowToast(message, ok ? 4f : 3f);
            Hud.ShowSubtitle(message, 4.5f);
            if (ok) Voice.PlayChirp();

            var owner = FindOwnerAt(station.BuildingId);
            if (ok && owner != null)
            {
                Sim.Npcs.AddTrust(owner.Id, 0.02);
                Sim.Npcs.Remember(owner.Id, "worked_with_player");
            }
        }

        private NpcProfile FindOwnerAt(string buildingId)
        {
            foreach (var p in Sim.Content.Npcs)
                if (p.LocationId == buildingId) return p;
            return null;
        }

        public void OpenDialogue(NpcProfile profile)
        {
            if (!Started || profile == null) return;
            Sim.Npcs.AddTrust(profile.Id, 0.01);
            Hud.OpenDialogue(profile, Sim.Content.TopicsFor(profile.Id));
            UnlockCursor();
            Voice.Speak(profile, true);

            string line = Sim.Arabic ? profile.GreetingAr : profile.GreetingEn;
            if (string.IsNullOrEmpty(line)) line = Sim.Arabic ? profile.StorySeedAr : profile.StorySeedEn;
            if (!string.IsNullOrEmpty(line))
            {
                string speaker = L.T(profile.DisplayNameEn, profile.DisplayNameAr);
                Hud.ShowSubtitle($"{speaker}: {line}", 7f);
            }
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
            if (Started && !Paused) LockCursor();
        }

        public void ToggleLanguage() => SetLanguage(!L.Arabic);

        public void SetLanguage(bool arabic)
        {
            if (L.Arabic == arabic) return;
            L.Arabic = arabic;
            Sim.Language = arabic ? "ar" : "en";
            World?.RefreshLanguage();
            Hud?.OnLanguageChanged();
            Hud?.ShowToast(L.T("Language: English", "اللغة: العربية"), 2.5f);
        }

        public void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void UnlockCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // ---- events ----------------------------------------------------------

        private void Deliver(SimEvent e)
        {
            Hud.PushEvent(e);

            string key = $"{e.Id}:{Math.Floor(Sim.Clock.TotalMinutes / 30.0)}";
            if (!_announced.Add(key)) return;

            Voice.SpeakEvent(e, L.Arabic);

            if (e.Kind == SimEventKind.Critical && LiveSettings.Subtitles)
                Hud.ShowSubtitle(L.T(e.BodyEn, e.BodyAr), 6f);
            else if (e.Kind == SimEventKind.Mission)
                Hud.ShowToast(L.T(e.TitleEn, e.TitleAr), 4f);
        }

        // ---- save / load -----------------------------------------------------

        public void SaveGame(bool announce)
        {
            if (!Started) return;
            try
            {
                var data = SaveData.Capture(Sim);
                data.Extras["engine"] = "unity";
                data.Extras["started"] = Started;
                data.Extras["playerX"] = Player.transform.position.x;
                data.Extras["playerY"] = Player.transform.position.y;
                data.Extras["playerZ"] = Player.transform.position.z;
                data.Extras["thirdPerson"] = Player.ThirdPerson;
                data.Extras["yaw"] = Player.Yaw;
                data.Extras["pitch"] = Player.Pitch;
            if (Rover != null)
            {
                data.Extras["roverX"] = Rover.transform.position.x;
                data.Extras["roverY"] = Rover.transform.position.y;
                data.Extras["roverZ"] = Rover.transform.position.z;
                data.Extras["roverYaw"] = Rover.transform.eulerAngles.y * Mathf.Deg2Rad;
                data.Extras["roverBattery"] = Rover.BatteryKwh;
                data.Extras["roverOdometer"] = Rover.Odometer;
                data.Extras["roverDriven"] = Rover.HasDriven;
                data.Extras["roverIce"] = Rover.ReachedIce;
                data.Extras["roverOccupied"] = Rover.Occupied;
            }
                File.WriteAllText(SavePath, data.ToJson());
                if (announce)
                    Hud.ShowToast(L.T("Colony saved.", "حُفظت المستعمرة."), 2.5f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed: {ex}");
                if (announce)
                    Hud.ShowToast(L.T("Save failed.", "فشل الحفظ."), 4f);
            }
        }

        public void LoadGame()
        {
            if (Hud == null) return;
            if (!File.Exists(SavePath))
            {
                Hud.ShowToast(L.T("No save file yet. The colony starts when you do.",
                    "لا يوجد ملف حفظ بعد. تبدأ المستعمرة عندما تبدأ أنت."), 4f);
                return;
            }

            try
            {
                var data = SaveData.FromJson(File.ReadAllText(SavePath));
                data.Apply(Sim);

                float x = 0f, y = 0.3f, z = 23f;
                if (data.Extras.TryGetValue("playerX", out object px))
                    x = (float)LunaQuest.Core.Content.Json.Num(px);
                if (data.Extras.TryGetValue("playerY", out object py))
                    y = (float)LunaQuest.Core.Content.Json.Num(py, 0.3);
                if (data.Extras.TryGetValue("playerZ", out object pz))
                    z = (float)LunaQuest.Core.Content.Json.Num(pz);
                if (y < -1f) y = 0.4f;
                Player.Teleport(new Vector3(x, y, z));

                bool thirdPerson = true;
                if (data.Extras.TryGetValue("thirdPerson", out object tp))
                    thirdPerson = LunaQuest.Core.Content.Json.Bool(tp, true);
                Player.SetThirdPerson(thirdPerson, false);

                float yaw = 180f, pitch = -8f;
                if (data.Extras.TryGetValue("yaw", out object pyaw))
                    yaw = (float)LunaQuest.Core.Content.Json.Num(pyaw, 180.0);
                if (data.Extras.TryGetValue("pitch", out object ppitch))
                    pitch = (float)LunaQuest.Core.Content.Json.Num(ppitch, -8.0);
                Player.SetLook(yaw, pitch);
                if (Rover != null) Rover.Restore(data.Extras, Player);

                L.Arabic = Sim.Language == "ar";
                World.RefreshLanguage();
                Hud.OnLanguageChanged();
                Started = true;
                Paused = false;
                Time.timeScale = 1f;
                _lastSavedDay = Sim.Clock.Day;
                LockCursor();
                Hud.ShowToast(L.T("Colony restored.", "استُعيدت المستعمرة."), 3f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Load failed: {ex}");
                Hud.ShowToast(L.T("Save file is not readable.", "ملف الحفظ غير قابل للقراءة."), 4f);
            }
        }

        private void OnApplicationQuit()
        {
            if (Started) SaveGame(false);
        }
    }
}
