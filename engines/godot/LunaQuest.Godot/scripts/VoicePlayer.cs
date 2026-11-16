using System;
using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Voice and ambience. Every crew member has a recorded offline voice identity and a
    /// one-line subtitle; there are no dialogue trees and no walls of text. The pack is
    /// generated at development time, so the game never calls a cloud service.
    /// </summary>
    public partial class VoicePlayer : Node
    {
        private AudioStreamPlayer _voice = null!;
        private AudioStreamPlayer _ambient = null!;
        private readonly Dictionary<string, string> _subtitle = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _subtitleAr = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _file = new Dictionary<string, string>();
        private Hud? _hud;
        private double _barkCooldown;

        private static readonly Dictionary<string, string> PrefixOf = new Dictionary<string, string>
        {
            ["npc_safaa"] = "safaa",
            ["npc_nour"] = "noor",
            ["npc_alaa"] = "alaa",
            ["npc_abdelaziz"] = "aziz",
            ["npc_ahmed"] = "ahmed",
            ["npc_nexo"] = "nexo",
            ["npc_mostafa"] = "mostafa",
        };

        /// <summary>Spoken lines can be silenced from the settings panel; subtitles still run.</summary>
        public bool Enabled = true;

        public void Bind(Hud hud) => _hud = hud;

        public bool IsPlaying => _voice != null && _voice.Playing;

        public void SetAmbientPaused(bool paused)
        {
            if (_ambient != null) _ambient.StreamPaused = paused;
        }

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
            if (!Enabled) return;
            // A greeting is a direct answer to the player and always plays. Ambient barks
            // keep their cooldown so the colony does not chatter over itself.
            if (_barkCooldown > 0 && !greeting) return;
            string prefix = PrefixOf.TryGetValue(profile.Id, out var p) ? p : "nexo";
            int idx = (int)(GD.Randi() % 4u);
            string key = greeting ? $"crew.{prefix}.greet" : $"bark.{prefix}.{idx}";
            string fallback = FallbackLine(profile);
            string text = L.Arabic
                ? (_subtitleAr.TryGetValue(key, out var ar) ? ar : fallback)
                : (_subtitle.TryGetValue(key, out var en) ? en : fallback);
            string file = _file.TryGetValue(key, out var f)
                ? f
                : (greeting ? $"crew-{prefix}-greet.mp3" : $"bark-{prefix}-{idx}.mp3");
            SpeakFile(file, text, greeting ? 7.0 : 5.0);
            _barkCooldown = greeting ? 6.0 : 9.0;
        }

        public void SpeakEvent(SimEvent e, bool arabic)
        {
            if (!Enabled) return;
            string key = $"inc.{e.Id}";
            if (!_file.TryGetValue(key, out string? file)) return;
            string text = arabic
                ? (_subtitleAr.TryGetValue(key, out var ar) ? ar : e.BodyAr)
                : (_subtitle.TryGetValue(key, out var en) ? en : e.BodyEn);
            SpeakFile(file, text, 6.0);
        }

        /// <summary>
        /// Plays one selectable crew topic. The authored voice key and the subtitle come
        /// from the same dialogue entry, so they cannot drift apart.
        /// </summary>
        public void SpeakTopic(DialogueTopic topic)
        {
            if (!Enabled) return;
            string text = L.Arabic ? topic.LineAr : topic.LineEn;
            if (_file.TryGetValue(topic.VoiceKey, out string? file))
                SpeakFile(file, text, 7.0);
            else
            {
                GD.PushWarning($"Voice key missing from pack: {topic.VoiceKey}");
                SpeakFile($"{topic.VoiceKey}.mp3", text, 7.0);
            }
            _barkCooldown = 6.0;
        }

        /// <summary>
        /// Development check: every crew voice must have its greeting and four rotating
        /// barks, every authored dialogue topic must have a key, and every key in the pack
        /// must point at a real imported file.
        /// </summary>
        public (int Total, List<string> Problems) VerifyPack(List<DialogueTopic>? topics = null)
        {
            var problems = new List<string>();
            foreach (var kv in PrefixOf)
            {
                string greet = $"crew.{kv.Value}.greet";
                if (!_file.ContainsKey(greet)) problems.Add($"missing key {greet}");
                for (int i = 0; i < 4; i++)
                {
                    string bark = $"bark.{kv.Value}.{i}";
                    if (!_file.ContainsKey(bark)) problems.Add($"missing key {bark}");
                }
            }

            if (topics != null)
                foreach (var topic in topics)
                    if (!_file.ContainsKey(topic.VoiceKey))
                        problems.Add($"missing topic key {topic.VoiceKey}");

            foreach (var kv in _file)
            {
                string path = $"res://assets/voice/{kv.Value}";
                if (!ResourceLoader.Exists(path)) problems.Add($"missing file {path}");
            }
            return (_file.Count, problems);
        }

        public void SpeakFile(string file, string subtitle, double seconds = 5.0)
        {
            if (!Enabled) return;
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
            else GD.PushWarning($"Voice file missing: {path}");
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
                _subtitleAr[kv.Key] = LunaQuest.Core.Content.Json.Str(entry["textAr"], _subtitle[kv.Key]);
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
