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
    /// Short recorded crew barks and incident lines — no dialogue trees, no runtime cloud
    /// speech. The pack is generated offline with Piper and shared byte-for-byte with the
    /// Godot adapter; every line carries English and Arabic subtitles.
    /// </summary>
    public sealed class VoicePlayer : MonoBehaviour
    {
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

        private readonly Dictionary<string, string> _subtitle = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _subtitleAr = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _file = new Dictionary<string, string>();
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
            string prefix = PrefixOf.TryGetValue(profile.Id, out string p) ? p : "nexo";
            int index = UnityEngine.Random.Range(0, 4);
            string key = greeting ? $"crew.{prefix}.greet" : $"bark.{prefix}.{index}";
            string fallback = _game.Sim.Arabic
                ? $"{profile.DisplayNameAr} — {profile.RoleAr}"
                : $"{profile.DisplayNameEn} — {profile.RoleEn}";
            string file = _file.TryGetValue(key, out string f)
                ? f
                : (greeting ? $"crew-{prefix}-greet" : $"bark-{prefix}-{index}");
            string text;
            if (_game.Sim.Arabic)
                text = _subtitleAr.TryGetValue(key, out string ar) ? ar : fallback;
            else
                text = _subtitle.TryGetValue(key, out string en) ? en : fallback;
            SpeakFile(file, text);
            _nextBark = Time.unscaledTimeAsDouble + (greeting ? 6.0 : 9.0);
        }

        public void SpeakTopic(DialogueTopic topic)
        {
            if (_game == null || topic == null) return;
            string text = _game.Sim.Arabic ? topic.LineAr : topic.LineEn;
            string file = _file.TryGetValue(topic.VoiceKey, out string f) ? f : $"{topic.VoiceKey}.mp3";
            SpeakFile(file, text, 7f);
            _nextBark = Time.unscaledTimeAsDouble + 6.0;
        }

        public void SpeakEvent(SimEvent e, bool arabic)
        {
            if (e == null) return;
            string key = $"inc.{e.Id}";
            if (!_file.TryGetValue(key, out string file)) return;
            string text = arabic
                ? (_subtitleAr.TryGetValue(key, out string ar) ? ar : e.BodyAr)
                : (_subtitle.TryGetValue(key, out string en) ? en : e.BodyEn);
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
                    _subtitleAr[pair.Key] = entry.TryGetValue("textAr", out object textAr)
                        ? Json.Str(textAr)
                        : Json.Str(text);
                    _file[pair.Key] = entry.TryGetValue("file", out object file) ? Json.Str(file) : "";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Voice pack could not be read: {ex.Message}");
            }
        }
    }
}
