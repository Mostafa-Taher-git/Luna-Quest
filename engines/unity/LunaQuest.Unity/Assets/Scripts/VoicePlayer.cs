using System;
using System.Collections.Generic;
using System.IO;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;
using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// Short recorded crew barks and incident lines — no dialogue trees, no generated
    /// speech. The pack is the same one the Godot adapter plays, and every line has a
    /// matching subtitle so the game stays readable with the sound off.
    /// </summary>
    public sealed class VoicePlayer : MonoBehaviour
    {
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

        private readonly Dictionary<string, string> _subtitle = new Dictionary<string, string>();
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private LunaQuestGame _game;
        private AudioSource _source;
        private double _nextBark;

        public void Bind(LunaQuestGame game)
        {
            _game = game;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.82f;
            LoadPack();
        }

        private void Update()
        {
            if (_nextBark > 0.0)
                _nextBark = Math.Max(0.0, _nextBark - Time.unscaledDeltaTime);
        }

        public void Speak(NpcProfile profile, bool greeting)
        {
            if (_game == null || profile == null || Time.unscaledTimeAsDouble < _nextBark) return;
            string voice = VoiceOf.TryGetValue(profile.Id, out string v) ? v : "maya";
            int index = UnityEngine.Random.Range(0, 3);
            string key = greeting ? $"crew.{voice}.greet" : $"bark.{voice}.{index}";
            string file = greeting ? $"crew-{voice}-greet" : $"bark-{voice}-{index}";
            string fallback = _game.Sim.Arabic
                ? $"{profile.DisplayNameAr} — {profile.RoleAr}"
                : $"{profile.DisplayNameEn} — {profile.RoleEn}";
            SpeakFile(file, _subtitle.TryGetValue(key, out string text) ? text : fallback);
            _nextBark = Time.unscaledTimeAsDouble + 9.0;
        }

        public void SpeakEvent(SimEvent e, bool arabic)
        {
            if (e == null || !IncidentVoice.TryGetValue(e.Id, out string kind)) return;
            string file = $"inc-{kind}-npc";
            string key = $"inc.{kind}.npc";
            string text = arabic ? e.BodyAr : (_subtitle.TryGetValue(key, out string t) ? t : e.BodyEn);
            SpeakFile(file, text, 6f);
        }

        public void SpeakFile(string file, string subtitle, float seconds = 5f)
        {
            var clip = Clip(file);
            if (clip != null && _source != null)
            {
                _source.Stop();
                _source.clip = clip;
                _source.Play();
            }
            if (_game != null && _game.Hud != null)
                _game.Hud.ShowSubtitle(subtitle, seconds);
        }

        private AudioClip Clip(string file)
        {
            if (_clips.TryGetValue(file, out AudioClip clip)) return clip;
            clip = Resources.Load<AudioClip>("Voice/" + Path.GetFileNameWithoutExtension(file));
            _clips[file] = clip;
            return clip;
        }

        private void LoadPack()
        {
            var asset = Resources.Load<TextAsset>("Voice/pack");
            if (asset == null) return;
            try
            {
                var root = Json.Obj(Json.Parse(asset.text));
                foreach (var pair in root)
                {
                    var entry = Json.Obj(pair.Value);
                    if (entry == null || !entry.TryGetValue("text", out object text)) continue;
                    _subtitle[pair.Key] = Json.Str(text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Voice pack could not be read: {ex.Message}");
            }
        }
    }
}
