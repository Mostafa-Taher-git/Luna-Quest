using System.Collections.Generic;
using Godot;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Persisted player settings, engine-local by design: this is @user://settings.json@ — never
    /// the shared save file (schema 2 stays schema 2). Loads on launch, applies live from the
    /// settings panel, and round-trips through JSON exactly.
    /// </summary>
    public static class GameSettings
    {
        private const string SettingsPath = "user://settings.json";

        public sealed class SettingsData
        {
            public bool ThirdPerson { get; set; } = true;
            public string Language { get; set; } = "en";
            public float MouseSensitivity { get; set; } = 1.0f;
            public float Fov { get; set; } = 70f;
            public float MasterVolume { get; set; } = 0.8f;
            public bool Voice { get; set; } = true;
            public bool Subtitles { get; set; } = true;
            public string Quality { get; set; } = "medium";
            public bool Fullscreen { get; set; } = false;
        }

        public static SettingsData Defaults() => new();

        public static SettingsData Load()
        {
            var data = Defaults();
            if (!FileAccess.FileExists(SettingsPath)) return data;
            using var f = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
            if (f == null) return data;
            var parsed = Json.ParseString(f.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary) return data;
            var d = parsed.AsGodotDictionary();
            if (d.ContainsKey("third_person")) data.ThirdPerson = d["third_person"].AsBool();
            if (d.ContainsKey("language")) data.Language = d["language"].AsString();
            if (d.ContainsKey("mouse_sensitivity")) data.MouseSensitivity = (float)d["mouse_sensitivity"].AsDouble();
            if (d.ContainsKey("fov")) data.Fov = (float)d["fov"].AsDouble();
            if (d.ContainsKey("master_volume")) data.MasterVolume = (float)d["master_volume"].AsDouble();
            if (d.ContainsKey("voice")) data.Voice = d["voice"].AsBool();
            if (d.ContainsKey("subtitles")) data.Subtitles = d["subtitles"].AsBool();
            if (d.ContainsKey("quality")) data.Quality = d["quality"].AsString();
            if (d.ContainsKey("fullscreen")) data.Fullscreen = d["fullscreen"].AsBool();
            return data;
        }

        public static Error Save(SettingsData data)
        {
            var d = new global::Godot.Collections.Dictionary
            {
                ["third_person"] = data.ThirdPerson,
                ["language"] = data.Language,
                ["mouse_sensitivity"] = data.MouseSensitivity,
                ["fov"] = data.Fov,
                ["master_volume"] = data.MasterVolume,
                ["voice"] = data.Voice,
                ["subtitles"] = data.Subtitles,
                ["quality"] = data.Quality,
                ["fullscreen"] = data.Fullscreen,
            };
            string tmp = SettingsPath + ".tmp";
            using (var f = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
            {
                if (f == null) return FileAccess.GetOpenError();
                f.StoreString(Json.Stringify(d, "\t"));
            }
            if (FileAccess.FileExists(SettingsPath))
                DirAccess.RenameAbsolute(SettingsPath, SettingsPath + ".bak");
            return DirAccess.RenameAbsolute(tmp, SettingsPath);
        }

        /// <summary>Apply the persisted/live settings to the engine and the player.</summary>
        public static void ApplyTo(SettingsData data, PlayerController player, VoicePlayer voice)
        {
            player.SetThirdPerson(data.ThirdPerson, false);
            player.BaseFov = data.Fov;
            player.MouseSensitivity = data.MouseSensitivity;
            int bus = AudioServer.GetBusIndex("Master");
            if (bus != -1)
            {
                AudioServer.SetBusVolumeDb(bus, LinearToDb(data.MasterVolume));
                AudioServer.SetBusMute(bus, data.MasterVolume <= 0.001f);
            }
            var size = data.Quality switch
            {
                "low" => new Vector2I(960, 540),
                "high" => new Vector2I(1600, 900),
                _ => new Vector2I(1280, 720),
            };
            DisplayServer.WindowSetSize(size);
            DisplayServer.WindowSetMode(data.Fullscreen
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
            voice.Enabled = data.Voice;
        }

        public static float LinearToDb(float linear)
            => Mathf.Clamp(20f * Mathf.Log(Mathf.Max(linear, 1e-6f)) / Mathf.Log(10f), -60f, 0f);
    }
}
