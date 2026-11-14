using System.Collections.Generic;

namespace LunaQuest.Core.Domain
{
    public sealed class ScheduleEntry
    {
        public double StartHour;
        public double EndHour;
        public string LocationId = "";
        public string ActivityEn = "";
        public string ActivityAr = "";
    }

    /// <summary>
    /// A human crew member. Names marked pending are never invented (GDD/roster rule):
    /// they stay as an explicit "awaiting approval" label.
    /// </summary>
    public sealed class NpcProfile
    {
        public string Id = "";
        public string NameEn = "";
        public string NameAr = "";
        public string RoleEn = "";
        public string RoleAr = "";
        public string ResponsibilitiesEn = "";
        public string ResponsibilitiesAr = "";
        public string LocationId = "";
        public string NameStatus = "approved";
        /// <summary>NEXO, the colony robot: no air, food, water or fatigue.</summary>
        public bool IsRobot;
        public List<string> Skills = new List<string>();
        public List<string> Traits = new List<string>();
        public List<ScheduleEntry> Schedule = new List<ScheduleEntry>();
        public List<string> KnownSystems = new List<string>();
        public List<string> Relationships = new List<string>();
        public string StorySeedEn = "";
        public string StorySeedAr = "";
        public string BoundaryEn = "";
        public string GreetingEn = "";
        public string GreetingAr = "";

        public bool NamePending => NameStatus != "approved";
        public string DisplayNameEn => NamePending
            ? (RoleEn.Contains("—") ? RoleEn.Split('—')[0].Trim() : (RoleEn.Contains("·") ? RoleEn.Split('·')[0].Trim() : "Name pending approval"))
            : NameEn;
        public string DisplayNameAr => NamePending
            ? (RoleAr.Contains("—") ? RoleAr.Split('—')[0].Trim() : (RoleAr.Contains("·") ? RoleAr.Split('·')[0].Trim() : "الاسم قيد الاعتماد"))
            : NameAr;

        public ScheduleEntry? CurrentEntry(double hourOfDay)
        {
            foreach (var e in Schedule)
            {
                bool wraps = e.StartHour > e.EndHour;
                if (wraps ? (hourOfDay >= e.StartHour || hourOfDay < e.EndHour)
                          : (hourOfDay >= e.StartHour && hourOfDay < e.EndHour))
                    return e;
            }
            return Schedule.Count > 0 ? Schedule[0] : null;
        }
    }

    public sealed class NpcState
    {
        public string Id = "";
        public string LocationId = "";
        public string ActivityEn = "";
        public string ActivityAr = "";
        public double Trust = 0.5;
        public double Fatigue;
        public List<string> MemoryTags = new List<string>();

        public bool HasTag(string tag) => MemoryTags.Contains(tag);
        public void Remember(string tag) { if (!MemoryTags.Contains(tag)) MemoryTags.Add(tag); }
    }
}
