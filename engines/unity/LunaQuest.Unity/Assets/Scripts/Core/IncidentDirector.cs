using System.Collections.Generic;

namespace LunaQuest.Core.Domain
{
    public sealed class IncidentDefinition
    {
        public string Id = "";
        /// <summary>incident | maintenance | discovery | project | social | exploration</summary>
        public string Kind = "incident";
        /// <summary>critical | important | routine | discovery</summary>
        public string Priority = "routine";
        public string Metric = "";
        public string Comparison = "above";
        public double Threshold;
        public double CooldownMinutes = 720;
        public string OwnerNpcId = "";
        public string LocationId = "";
        public string TitleEn = "";
        public string TitleAr = "";
        public string BodyEn = "";
        public string BodyAr = "";
        public string LearningObjectiveId = "";
        /// <summary>Action id the player must perform to resolve it, e.g. "clean_arrays".</summary>
        public string RequiredAction = "";
    }

    public sealed class ActiveIncident
    {
        public string DefinitionId = "";
        public double RaisedAtMinute;
        public bool Resolved;
        public bool Ignored;
        public int HintLevel;
    }

    /// <summary>
    /// Watches world metrics and raises a bounded number of meaningful events. Rules are content,
    /// never random text (GDD §9.3). Ignoring an event records it; it never becomes a permanent fail.
    /// </summary>
    public sealed class IncidentDirector
    {
        public const int MaxActive = 3;

        private readonly List<IncidentDefinition> _definitions;
        private readonly Dictionary<string, double> _lastRaised = new Dictionary<string, double>();
        public readonly List<ActiveIncident> Active = new List<ActiveIncident>();

        public IncidentDirector(List<IncidentDefinition> definitions)
        {
            _definitions = definitions;
        }

        public IEnumerable<IncidentDefinition> Definitions => _definitions;

        public IncidentDefinition? Definition(string id) => _definitions.Find(d => d.Id == id);

        public bool TryRaise(string id, double minute, out IncidentDefinition? def)
        {
            def = Definition(id);
            if (def == null) return false;
            foreach (var a in Active) if (a.DefinitionId == id && !a.Resolved && !a.Ignored) return false;
            if (Active.Count >= MaxActive) return false;
            if (_lastRaised.TryGetValue(id, out double last) && minute - last < def.CooldownMinutes) return false;

            Active.Add(new ActiveIncident { DefinitionId = id, RaisedAtMinute = minute });
            _lastRaised[id] = minute;
            return true;
        }

        /// <summary>Evaluates every rule against live state and returns newly raised definitions.</summary>
        public List<IncidentDefinition> Evaluate(Sim.ColonyState state, double minute)
        {
            var raised = new List<IncidentDefinition>();
            foreach (var d in _definitions)
            {
                double value = ReadMetric(state, d.Metric, minute);
                bool hit = d.Comparison == "below" ? value < d.Threshold : value > d.Threshold;
                if (!hit) continue;
                if (TryRaise(d.Id, minute, out var def) && def != null) raised.Add(def);
            }
            return raised;
        }

        public bool Resolve(string id, string action)
        {
            var def = Definition(id);
            var active = Active.Find(a => a.DefinitionId == id && !a.Resolved);
            if (def == null || active == null) return false;
            if (!string.IsNullOrEmpty(def.RequiredAction) && def.RequiredAction != action) return false;
            active.Resolved = true;
            return true;
        }

        public void Ignore(string id)
        {
            var a = Active.Find(x => x.DefinitionId == id && !x.Resolved);
            if (a != null) a.Ignored = true;
        }

        public void DismissResolved()
            => Active.RemoveAll(a => a.Resolved || a.Ignored);

        /// <summary>Restores a resolved incident from a save without re-firing it immediately.</summary>
        public void MarkResolved(string id, double minute)
        {
            _lastRaised[id] = minute;
            Active.RemoveAll(a => a.DefinitionId == id);
        }

        public void MarkIgnored(string id, double minute)
        {
            _lastRaised[id] = minute;
            Active.RemoveAll(a => a.DefinitionId == id);
        }

        public static double ReadMetric(Sim.ColonyState s, string metric, double minute = 0.0)
        {
            switch (metric)
            {
                case "arrayDust": return s.ArrayDust;
                case "sensorDust": return s.SensorDust;
                case "airlockDust": return s.AirlockDust;
                case "batteryPercent": return s.BatteryPercent;
                case "waterPercent": return s.WaterPercent;
                case "o2Percent": return s.O2Percent;
                case "co2Ppm": return s.Co2Ppm;
                case "filterHealth": return s.FilterHealth;
                case "scrubberHealth": return s.ScrubberHealth;
                case "cropHealth": return s.CropHealth;
                case "signalQuality": return s.SignalQuality;
                case "doseRate": return s.DoseRateMicroGyH;
                case "cabinTemp": return s.CabinTempC;
                case "foodPercent": return s.FoodPercent;
                case "relayHealth": return s.RelayHealth;
                case "crewFatigue": return s.AverageFatigue;
                case "pressure": return s.CabinPressureKpa;
                case "samples": return s.SamplesCollected;
                case "suitEnergy": return s.SuitEnergy;
                case "foodKcal": return s.FoodKcal;
                case "cropBiomass": return s.CropBiomassKg;
                case "day": return minute / 1440.0 + 1.0;
                default: return 0.0;
            }
        }
    }
}
