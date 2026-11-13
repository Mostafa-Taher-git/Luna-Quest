using System;
using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Voice and ambience. The crew speak in short recorded barks with a one-line subtitle;
    /// there are no dialogue trees and no walls of text. Incident barks are chosen from the
    /// same fixed pack, so nothing is generated at runtime.
    /// </summary>
    public partial class VoicePlayer : Node
    {
        private AudioStreamPlayer _voice = null!;
        private AudioStreamPlayer _ambient = null!;
        private readonly Dictionary<string, string> _subtitle = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _file = new Dictionary<string, string>();
        private Hud? _hud;
        private double _barkCooldown;

        private static readonly Dictionary<string, string> VoiceOf = new Dictionary<string, string>
        {
            ["npc_safaa"] = "sofia",
            ["npc_nour"] = "omar",
            ["npc_alaa"] = "amina",
            ["npc_agriculture_engineer"] = "maya",
            ["npc_abdelaziz"] = "leo",
            ["npc_ahmed"] = "leo",
            ["npc_comms_software_engineer"] = "leo",
            ["npc_mostafa"] = "omar",
        };

        private static readonly Dictionary<string, string> IncidentVoice = new Dictionary<string, string>
        {
            ["inc_array_dust"] = "dust",
            ["inc_sensor_dust"] = "dust",
            ["inc_airlock_dust"] = "dust",
            ["inc_water_filter"] = "water",
            ["inc_water_low"] = "water",
            ["inc_scrubber"] = "water",
            ["inc_co2_high"] = "water",
            ["inc_o2_low"] = "water",
            ["inc_crop_stress"] = "green",
            ["inc_food_low"] = "green",
            ["inc_radiation"] = "storm",
            ["inc_comms"] = "comms",
            ["inc_fatigue"] = "fatigue",
            ["inc_power_low"] = "budget",
            ["inc_ice_discovery"] = "sample",
        };

        public void Bind(Hud hud) => _hud = hud;

        public override void _Ready()
        {
            _voice = new AudioStreamPlayer { Bus = "Master", VolumeDb = -2 };
            AddChild(_voice);
            _ambient = new AudioStreamPlayer { Bus = "Master", VolumeDb = -20 };
            AddChild(_ambient);
            LoadPack();
            _ambient.Stream = BuildAmbient();
            _ambient.Play();
        }

        public override void _Process(double delta)
        {
            _barkCooldown = Math.Max(0, _barkCooldown - delta);
        }

        public void Speak(NpcProfile profile, bool greeting)
        {
            if (_barkCooldown > 0) return;
            string voice = VoiceOf.TryGetValue(profile.Id, out var v) ? v : "maya";
            int idx = (int)(GD.Randi() % 3u);
            string key = greeting ? $"crew.{voice}.greet" : $"bark.{voice}.{idx}";
            string file = greeting ? $"crew-{voice}-greet.mp3" : $"bark-{voice}-{idx}.mp3";
            SpeakFile(file, _subtitle.TryGetValue(key, out var text) ? text : FallbackLine(profile));
            _barkCooldown = 9.0;
        }

        public void SpeakEvent(SimEvent e, bool arabic)
        {
            if (!IncidentVoice.TryGetValue(e.Id, out string? kind)) return;
            string key = $"inc.{kind}.npc";
            string file = $"inc-{kind}-npc.mp3";
            string text = arabic ? e.BodyAr : (_subtitle.TryGetValue(key, out var t) ? t : e.BodyEn);
            SpeakFile(file, text, 6.0);
        }

        public void SpeakFile(string file, string subtitle, double seconds = 5.0)
        {
            var path = $"res://assets/voice/{file}";
            if (ResourceLoader.Exists(path))
            {
                var stream = GD.Load<AudioStream>(path);
                if (stream != null)
                {
                    _voice.Stream = stream;
                    _voice.Play();
                }
            }
            _hud?.ShowSubtitle(subtitle, seconds);
        }

        private string FallbackLine(NpcProfile p)
            => L.Arabic ? $"{p.DisplayNameAr} — {p.RoleAr}" : $"{p.DisplayNameEn} — {p.RoleEn}";

        private void LoadPack()
        {
            const string path = "res://assets/voice/pack.json";
            if (!FileAccess.FileExists(path)) return;
            string text = FileAccess.GetFileAsString(path);
            var root = LunaQuest.Core.Content.Json.Obj(LunaQuest.Core.Content.Json.Parse(text));
            foreach (var kv in root)
            {
                var entry = LunaQuest.Core.Content.Json.Obj(kv.Value);
                _subtitle[kv.Key] = LunaQuest.Core.Content.Json.Str(entry["text"]);
                _file[kv.Key] = LunaQuest.Core.Content.Json.Str(entry["file"]);
            }
        }

        /// <summary>
        /// A quiet habitat rumble: filtered noise plus a 55 Hz hum. Generated rather than
        /// shipped as another asset, and looped seamlessly over five seconds.
        /// </summary>
        private static AudioStreamWav BuildAmbient()
        {
            const int rate = 22050;
            const double seconds = 5.0;
            int count = (int)(rate * seconds);
            var data = new byte[count * 2];
            var rng = new Random(7);
            double low = 0;
            for (int i = 0; i < count; i++)
            {
                double n = rng.NextDouble() * 2.0 - 1.0;
                low = low * 0.995 + n * 0.005;
                double env = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / count); // seamless loop window
                double hum = Math.Sin(2.0 * Math.PI * 55.0 * i / rate) * 0.18;
                double fan = low * 3.0;
                short s = (short)(Math.Clamp((hum + fan) * env * 12000.0, -32768, 32767));
                data[i * 2] = (byte)(s & 0xff);
                data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = rate,
                Stereo = false,
                Data = data,
                LoopMode = AudioStreamWav.LoopModeEnum.Forward,
                LoopEnd = count,
            };
            return wav;
        }
    }
}
