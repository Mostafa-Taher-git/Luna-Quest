using System;
using System.IO;
using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// Persisted player settings — engine-local, written to  @Application.persistentDataPath/settings.json@,
    /// never the shared save file (schema 2 stays schema 2). Loaded at boot, applied live from the
    /// settings panel, round-trips exactly.
    /// </summary>
    public static class SettingsStore
    {
        [Serializable]
        public sealed class SettingsData
        {
            public bool ThirdPerson = true;   // this edition ships in third person
            public string Language = "en";
            public float MouseSensitivity = 1.0f;
            public float Fov = 70f;
            public float MasterVolume = 0.8f;
            public bool Voice = true;
            public bool Subtitles = true;
            public string Quality = "medium";
            public bool Fullscreen = false;
        }

        private static string StorePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public static SettingsData Defaults() => new SettingsData();

        public static SettingsData Load()
        {
            var data = Defaults();
            try
            {
                if (!File.Exists(StorePath)) return data;
                var parsed = JsonUtility.FromJson<SettingsData>(File.ReadAllText(StorePath));
                return parsed ?? data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"settings.json could not be read ({e.Message}); using defaults.");
                return data;
            }
        }

        public static bool Save(SettingsData data)
        {
            try
            {
                string tmp = StorePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(StorePath))
                    File.Copy(StorePath, StorePath + ".bak", true);
                File.Move(tmp, StorePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"settings.json could not be written ({e.Message}).");
                return false;
            }
        }

        /// <summary>Push the settings into the engine and the player. Called at boot and on every
        /// change from the settings panel.</summary>
        public static void ApplyTo(SettingsData data, LunaQuestGame game)
        {
            if (game == null) return;
            var player = game.Player;
            if (player != null)
            {
                player.SetThirdPerson(data.ThirdPerson, false);
                player.MouseSensitivity = Mathf.Clamp(data.MouseSensitivity, 0.1f, 4.0f);
                player.BaseFov = Mathf.Clamp(data.Fov, 30f, 110f);
            }
            AudioListener.volume = Mathf.Clamp01(data.MasterVolume);
            Screen.fullScreen = data.Fullscreen;
            QualitySettings.SetQualityLevel(data.Quality == "low" ? 0 : data.Quality == "high" ? 2 : 1, true);
            if (game.Voice != null) game.Voice.Enabled = data.Voice;
        }
    }
}
