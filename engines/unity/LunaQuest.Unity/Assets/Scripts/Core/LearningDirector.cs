using System.Collections.Generic;

namespace LunaQuest.Core.Domain
{
    public sealed class JournalEntry
    {
        public string ObjectiveId = "";
        public string TitleEn = "";
        public string TitleAr = "";
        public string BodyEn = "";
        public string BodyAr = "";
        public string Skill = "";
        public string EvidenceClass = "";
        public string Source = "";
        public double UnlockedAtMinute;
    }

    /// <summary>
    /// Learning is evidence of practice, not a quiz. Each completed activity records one concept
    /// and one skill, and hints deepen only when the player asks (GDD §10).
    /// </summary>
    public sealed class LearningDirector
    {
        private readonly Content.ContentDatabase _content;
        private readonly Dictionary<string, int> _skillEvidence = new Dictionary<string, int>();
        public readonly List<JournalEntry> Journal = new List<JournalEntry>();

        public LearningDirector(Content.ContentDatabase content)
        {
            _content = content;
        }

        public IReadOnlyDictionary<string, int> Skills => _skillEvidence;

        public bool HasEntry(string objectiveId) => Journal.Exists(j => j.ObjectiveId == objectiveId);

        public JournalEntry? Unlock(string objectiveId, double minute)
        {
            if (string.IsNullOrEmpty(objectiveId) || HasEntry(objectiveId)) return null;
            var obj = _content.Objective(objectiveId);
            if (obj == null) return null;

            var entry = new JournalEntry
            {
                ObjectiveId = obj.Id, TitleEn = obj.TitleEn, TitleAr = obj.TitleAr,
                BodyEn = obj.BodyEn, BodyAr = obj.BodyAr, Skill = obj.Skill,
                EvidenceClass = obj.EvidenceClass, Source = obj.Source, UnlockedAtMinute = minute
            };
            Journal.Add(entry);

            if (!string.IsNullOrEmpty(obj.Skill))
                _skillEvidence[obj.Skill] = (_skillEvidence.TryGetValue(obj.Skill, out int n) ? n : 0) + 1;

            return entry;
        }

        public int EvidenceFor(string skill)
            => _skillEvidence.TryGetValue(skill, out int n) ? n : 0;

        /// <summary>0 = nudge, 1 = point at the right station, 2 = explain the variable, 3 = do it with you.</summary>
        public string Hint(string objectiveId, int level, bool arabic)
        {
            var obj = _content.Objective(objectiveId);
            if (obj == null) return arabic ? "اقترب من المحطة واقرأ اللوحة." : "Walk up to the station and read its panel.";
            switch (level)
            {
                case 0: return arabic ? "انظر إلى الأرقام قبل أن تلمس أي شيء." : "Look at the numbers before you touch anything.";
                case 1: return arabic ? obj.TitleAr : obj.TitleEn;
                case 2: return arabic ? obj.BodyAr : obj.BodyEn;
                default: return arabic ? "افعلها خطوة واحدة الآن: " + obj.BodyAr : "Do one step now: " + obj.BodyEn;
            }
        }

        public void LoadEntry(JournalEntry e) { if (!HasEntry(e.ObjectiveId)) Journal.Add(e); }
        public void LoadSkill(string skill, int count) => _skillEvidence[skill] = count;
    }
}
