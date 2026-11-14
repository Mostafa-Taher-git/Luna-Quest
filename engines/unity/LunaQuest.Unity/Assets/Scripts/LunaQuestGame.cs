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
        public ColonyHud Hud;
        public VoicePlayer Voice;

        public bool Started { get; private set; }
        public string ContentError = "";

        private double _saveTimer;
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

            Hud = new ColonyHud(this);
            Hud.Init();

            var voiceGo = new GameObject("Voice");
            Voice = voiceGo.AddComponent<VoicePlayer>();
            Voice.Bind(this);

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

            if (Input.GetKeyDown(KeyCode.E) && InputActive)
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

        private void Interact()
        {
            if (Player == null) return;

            if (Player.FocusCrew != null)
            {
                var crew = Player.FocusCrew;
                var profile = Sim.Content.Npc(crew.NpcId);
                if (profile != null)
                {
                    Sim.Npcs.AddTrust(profile.Id, 0.01);
                    Voice.Speak(profile, true);
                    string line = Sim.Arabic ? profile.GreetingAr : profile.GreetingEn;
                    if (string.IsNullOrEmpty(line)) line = Sim.Arabic ? profile.StorySeedAr : profile.StorySeedEn;
                    string speaker = L.T(profile.DisplayNameEn, profile.DisplayNameAr);
                    Hud.ShowSubtitle($"{speaker}: {line}", 7f);
                    Hud.ShowToast(L.T("Crew log: a short, in-scope line. No generated advice.",
                        "سجل الطاقم: سطر قصير ضمن الاختصاص. لا نصائح مولدة."), 3f);
                }
                return;
            }

            var station = Player.FocusStation;
            if (station == null) return;

            bool ok = Sim.Interact(station.BuildingId, station.InteractionId,
                out string en, out string ar);
            string message = L.T(en, ar);
            Hud.ShowToast(message, ok ? 4f : 3f);
            Hud.ShowSubtitle(message, 4.5f);

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

            if (e.Kind == SimEventKind.Critical)
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
