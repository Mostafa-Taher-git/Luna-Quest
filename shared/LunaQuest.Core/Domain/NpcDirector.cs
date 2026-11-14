using System;
using System.Collections.Generic;
using LunaQuest.Core.Time;

namespace LunaQuest.Core.Domain
{
    /// <summary>
    /// Drives the six human crew members and NEXO, the colony robot, from data, not scripts. They follow schedules whether
    /// or not the player is watching, and they only know their own professional scope.
    /// </summary>
    public sealed class NpcDirector
    {
        private readonly Content.ContentDatabase _content;
        public readonly Dictionary<string, NpcState> States = new Dictionary<string, NpcState>();

        public NpcDirector(Content.ContentDatabase content)
        {
            _content = content;
            foreach (var n in content.Npcs)
            {
                States[n.Id] = new NpcState
                {
                    Id = n.Id,
                    LocationId = n.LocationId,
                    ActivityEn = "on duty",
                    ActivityAr = "في المناوبة",
                    Trust = 0.5,
                    Fatigue = 0.2,
                };
            }
        }

        public NpcState State(string id)
        {
            if (!States.TryGetValue(id, out var s))
            {
                s = new NpcState { Id = id };
                States[id] = s;
            }
            return s;
        }

        /// <summary>Re-evaluate schedules. Called on game-time changes, not every frame.</summary>
        public void Tick(double hourOfDay)
        {
            foreach (var profile in _content.Npcs)
            {
                var entry = profile.CurrentEntry(hourOfDay);
                if (entry == null) continue;
                var st = State(profile.Id);
                st.LocationId = entry.LocationId;
                st.ActivityEn = entry.ActivityEn;
                st.ActivityAr = entry.ActivityAr;

                // Fatigue is per simulated hour, not per frame. Work adds it slowly, sleep
                // removes it faster; the net over a mission day is a gentle rise that the
                // player can reverse with the rest plan.
                bool resting = entry.ActivityEn.IndexOf("rest", StringComparison.OrdinalIgnoreCase) >= 0
                            || entry.ActivityEn.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0;
                double dtHours = GameClock.MinutesPerSecond / 60.0;
                double ratePerHour = resting ? -0.035 : 0.030;
                st.Fatigue = profile.IsRobot ? 0.0 : Clamp(st.Fatigue + ratePerHour * dtHours, 0.0, 1.0);
            }
        }

        public void AddTrust(string npcId, double delta)
            => State(npcId).Trust = Clamp(State(npcId).Trust + delta, 0.0, 1.0);

        public void Remember(string npcId, string tag) => State(npcId).Remember(tag);

        /// <summary>
        /// A short, approved-context line. No open generation: the line is selected from the profile
        /// and the world state, which is what keeps child-facing dialogue safe.
        /// </summary>
        public string Line(string npcId, bool arabic, double hourOfDay, bool incidentNearby)
        {
            var p = _content.Npc(npcId);
            if (p == null) return "";
            var st = State(npcId);

            if (p.NamePending && !arabic)
                return "I am on shift. My name record is still waiting for team approval.";

            if (incidentNearby)
                return arabic
                    ? "القراءة غير طبيعية. لا تصلحها قبل أن تعرف سببها."
                    : "That reading is off. Don't fix it before you know why.";

            if (st.Fatigue > 0.7 && !p.IsRobot)
                return arabic
                    ? "أنا متعب. هل يمكننا إعادة ترتيب المناوبة؟"
                    : "I'm running tired. Can we rebalance the shift?";

            if (!arabic && !string.IsNullOrEmpty(p.GreetingEn))
                return p.GreetingEn;
            if (arabic && !string.IsNullOrEmpty(p.GreetingAr))
                return p.GreetingAr;

            return arabic ? $"{p.DisplayNameAr} — {p.RoleAr}" : $"{p.DisplayNameEn} — {p.RoleEn}";
        }

        /// <summary>Which crew member owns a system, used by incidents.</summary>
        public NpcProfile? OwnerOf(string system)
        {
            foreach (var n in _content.Npcs)
                if (n.KnownSystems.Contains(system)) return n;
            return null;
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
