using System;
using System.Collections.Generic;
using LunaQuest.Core.Content;

namespace LunaQuest.Core.Save
{
    /// <summary>
    /// Versioned save contract shared by Unity and Godot. Only IDs and values are stored, never
    /// scene hierarchies, so the same save opens in either engine (GDD §15.5).
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentSchema = 2;

        public int SchemaVersion = CurrentSchema;
        public string SavedAtIso = DateTime.UtcNow.ToString("o");
        public string PlayerName = "Amara Okafor";
        public string Language = "en";
        public double ClockMinutes;

        // Colony metrics
        public double BatteryKwh, ArrayHealth, ArrayDust, SensorDust, AirlockDust;
        public double WaterL, FilterHealth, WaterQuality, O2Kg, CO2Kg, CabinPressureKpa, ScrubberHealth;
        public double CabinTempC, FoodKcal, CropBiomassKg, CropHealth, CropWaterStress;
        public double CumulativeDoseMsv, SignalQuality, RelayHealth, AverageFatigue, SuitEnergy;
        public int SamplesCollected;
        public bool CrewSheltered, GrowLightsOn, O2GeneratorRunning, RecyclerRunning;
        public bool SolarParticleEvent;
        public double SpeIntensity;

        public Dictionary<string, double> NpcTrust = new Dictionary<string, double>();
        public Dictionary<string, double> NpcFatigue = new Dictionary<string, double>();
        public Dictionary<string, List<string>> NpcMemory = new Dictionary<string, List<string>>();

        public List<string> JournalObjectives = new List<string>();
        public Dictionary<string, int> Skills = new Dictionary<string, int>();
        public List<string> ResolvedIncidents = new List<string>();
        public List<string> IgnoredIncidents = new List<string>();

        /// <summary>Engine/position extras: player position, interior state, build layout.</summary>
        public Dictionary<string, object?> Extras = new Dictionary<string, object?>();

        public static SaveData Capture(Sim.ColonySimulation sim)
        {
            var s = sim.State;
            var d = new SaveData
            {
                PlayerName = sim.PlayerName,
                Language = sim.Language,
                ClockMinutes = sim.Clock.TotalMinutes,
                BatteryKwh = s.BatteryKwh,
                ArrayHealth = s.ArrayHealth,
                ArrayDust = s.ArrayDust,
                SensorDust = s.SensorDust,
                AirlockDust = s.AirlockDust,
                WaterL = s.WaterL,
                FilterHealth = s.FilterHealth,
                WaterQuality = s.WaterQuality,
                O2Kg = s.O2Kg,
                CO2Kg = s.CO2Kg,
                CabinPressureKpa = s.CabinPressureKpa,
                ScrubberHealth = s.ScrubberHealth,
                CabinTempC = s.CabinTempC,
                FoodKcal = s.FoodKcal,
                CropBiomassKg = s.CropBiomassKg,
                CropHealth = s.CropHealth,
                CropWaterStress = s.CropWaterStress,
                CumulativeDoseMsv = s.CumulativeDoseMsv,
                SignalQuality = s.SignalQuality,
                RelayHealth = s.RelayHealth,
                AverageFatigue = s.AverageFatigue,
                SuitEnergy = s.SuitEnergy,
                SamplesCollected = s.SamplesCollected,
                CrewSheltered = s.CrewSheltered,
                GrowLightsOn = s.GrowLightsOn,
                O2GeneratorRunning = s.O2GeneratorRunning,
                RecyclerRunning = s.RecyclerRunning,
                SolarParticleEvent = s.SolarParticleEvent,
                SpeIntensity = s.SpeIntensity,
            };

            foreach (var kv in sim.Npcs.States)
            {
                d.NpcTrust[kv.Key] = kv.Value.Trust;
                d.NpcFatigue[kv.Key] = kv.Value.Fatigue;
                d.NpcMemory[kv.Key] = new List<string>(kv.Value.MemoryTags);
            }
            foreach (var e in sim.Learning.Journal) d.JournalObjectives.Add(e.ObjectiveId);
            foreach (var kv in sim.Learning.Skills) d.Skills[kv.Key] = kv.Value;
            foreach (var a in sim.Incidents.Active)
            {
                if (a.Resolved) d.ResolvedIncidents.Add(a.DefinitionId);
                if (a.Ignored) d.IgnoredIncidents.Add(a.DefinitionId);
            }
            return d;
        }

        public void Apply(Sim.ColonySimulation sim)
        {
            var s = sim.State;
            sim.PlayerName = PlayerName;
            sim.Language = Language;
            sim.Clock.LoadFrom(ClockMinutes);

            s.BatteryKwh = BatteryKwh;
            s.ArrayHealth = ArrayHealth;
            s.ArrayDust = ArrayDust;
            s.SensorDust = SensorDust;
            s.AirlockDust = AirlockDust;
            s.WaterL = WaterL;
            s.FilterHealth = FilterHealth;
            s.WaterQuality = WaterQuality;
            s.O2Kg = O2Kg;
            s.CO2Kg = CO2Kg;
            s.CabinPressureKpa = CabinPressureKpa;
            s.ScrubberHealth = ScrubberHealth;
            s.CabinTempC = CabinTempC;
            s.FoodKcal = FoodKcal;
            s.CropBiomassKg = CropBiomassKg;
            s.CropHealth = CropHealth;
            s.CropWaterStress = CropWaterStress;
            s.CumulativeDoseMsv = CumulativeDoseMsv;
            s.SignalQuality = SignalQuality;
            s.RelayHealth = RelayHealth;
            s.AverageFatigue = AverageFatigue;
            s.SuitEnergy = SuitEnergy;
            s.SamplesCollected = SamplesCollected;
            s.CrewSheltered = CrewSheltered;
            s.GrowLightsOn = GrowLightsOn;
            s.O2GeneratorRunning = O2GeneratorRunning;
            s.RecyclerRunning = RecyclerRunning;
            s.SolarParticleEvent = SolarParticleEvent;
            s.SpeIntensity = SpeIntensity;

            foreach (var kv in NpcTrust) sim.Npcs.State(kv.Key).Trust = kv.Value;
            foreach (var kv in NpcFatigue) sim.Npcs.State(kv.Key).Fatigue = kv.Value;
            foreach (var kv in NpcMemory)
            {
                var st = sim.Npcs.State(kv.Key);
                st.MemoryTags.Clear();
                st.MemoryTags.AddRange(kv.Value);
            }
            foreach (var id in JournalObjectives) sim.Learning.Unlock(id, sim.Clock.TotalMinutes);
            foreach (var kv in Skills) sim.Learning.LoadSkill(kv.Key, kv.Value);

            // Restore incidents as already-resolved so they do not re-fire immediately.
            foreach (var id in ResolvedIncidents) sim.Incidents.MarkResolved(id, sim.Clock.TotalMinutes);
            foreach (var id in IgnoredIncidents) sim.Incidents.MarkIgnored(id, sim.Clock.TotalMinutes);
        }

        public string ToJson() => Json.Serialize(ToDict(), true);

        public Dictionary<string, object?> ToDict()
        {
            var d = new Dictionary<string, object?>
            {
                ["schemaVersion"] = SchemaVersion,
                ["savedAt"] = SavedAtIso,
                ["playerName"] = PlayerName,
                ["language"] = Language,
                ["clockMinutes"] = ClockMinutes,
                ["colony"] = new Dictionary<string, object?>
                {
                    ["batteryKwh"] = BatteryKwh, ["arrayHealth"] = ArrayHealth, ["arrayDust"] = ArrayDust,
                    ["sensorDust"] = SensorDust, ["airlockDust"] = AirlockDust, ["waterL"] = WaterL,
                    ["filterHealth"] = FilterHealth, ["waterQuality"] = WaterQuality, ["o2Kg"] = O2Kg,
                    ["co2Kg"] = CO2Kg, ["pressureKpa"] = CabinPressureKpa, ["scrubberHealth"] = ScrubberHealth,
                    ["cabinTempC"] = CabinTempC, ["foodKcal"] = FoodKcal, ["cropBiomass"] = CropBiomassKg,
                    ["cropHealth"] = CropHealth, ["cropWaterStress"] = CropWaterStress,
                    ["doseMsv"] = CumulativeDoseMsv, ["signal"] = SignalQuality, ["relayHealth"] = RelayHealth,
                    ["fatigue"] = AverageFatigue, ["suitEnergy"] = SuitEnergy,
                    ["samples"] = SamplesCollected, ["sheltered"] = CrewSheltered,
                    ["growLights"] = GrowLightsOn, ["o2Generator"] = O2GeneratorRunning,
                    ["recycler"] = RecyclerRunning, ["spe"] = SolarParticleEvent, ["speIntensity"] = SpeIntensity
                },
                ["npcs"] = NpcDict(),
                ["journal"] = new List<object?>(JournalObjectives),
                ["skills"] = SkillDict(),
                ["resolved"] = new List<object?>(ResolvedIncidents),
                ["ignored"] = new List<object?>(IgnoredIncidents),
                ["extras"] = Extras,
            };
            return d;
        }

        private Dictionary<string, object?> NpcDict()
        {
            var o = new Dictionary<string, object?>();
            foreach (var kv in NpcTrust)
            {
                o[kv.Key] = new Dictionary<string, object?>
                {
                    ["trust"] = kv.Value,
                    ["fatigue"] = NpcFatigue.TryGetValue(kv.Key, out double f) ? f : 0.0,
                    ["memory"] = NpcMemory.TryGetValue(kv.Key, out var m) ? new List<object?>(m) : new List<object?>(),
                };
            }
            return o;
        }

        private Dictionary<string, object?> SkillDict()
        {
            var o = new Dictionary<string, object?>();
            foreach (var kv in Skills) o[kv.Key] = kv.Value;
            return o;
        }

        public static SaveData FromJson(string json)
        {
            var root = Json.Obj(Json.Parse(json));
            int schema = Json.Int(root["schemaVersion"], 1);
            if (schema < 1 || schema > CurrentSchema)
                throw new InvalidOperationException($"Unsupported save schema {schema}; expected <= {CurrentSchema}.");

            var d = new SaveData
            {
                SchemaVersion = schema,
                SavedAtIso = Json.Str(root["savedAt"]),
                PlayerName = Json.Str(root["playerName"], "Amara Okafor"),
                Language = Json.Str(root["language"], "en"),
                ClockMinutes = Json.Num(root["clockMinutes"]),
            };

            var c = Json.Obj(root["colony"]);
            d.BatteryKwh = Json.Num(c["batteryKwh"], 96);
            d.ArrayHealth = Json.Num(c["arrayHealth"], 1);
            d.ArrayDust = Json.Num(c["arrayDust"]);
            d.SensorDust = Json.Num(c["sensorDust"]);
            d.AirlockDust = Json.Num(c["airlockDust"]);
            d.WaterL = Json.Num(c["waterL"], 900);
            d.FilterHealth = Json.Num(c["filterHealth"], 1);
            d.WaterQuality = Json.Num(c["waterQuality"], 0.98);
            d.O2Kg = Json.Num(c["o2Kg"], 83.7);
            d.CO2Kg = Json.Num(c["co2Kg"], 0.35);
            d.CabinPressureKpa = Json.Num(c["pressureKpa"], 101.3);
            d.ScrubberHealth = Json.Num(c["scrubberHealth"], 1);
            d.CabinTempC = Json.Num(c["cabinTempC"], 21);
            d.FoodKcal = Json.Num(c["foodKcal"], 36000);
            d.CropBiomassKg = Json.Num(c["cropBiomass"]);
            d.CropHealth = Json.Num(c["cropHealth"], 1);
            d.CropWaterStress = Json.Num(c["cropWaterStress"]);
            d.CumulativeDoseMsv = Json.Num(c["doseMsv"]);
            d.SignalQuality = Json.Num(c["signal"], 1);
            d.RelayHealth = Json.Num(c["relayHealth"], 1);
            d.AverageFatigue = Json.Num(c["fatigue"]);
            d.SuitEnergy = Json.Num(c["suitEnergy"], 1);
            d.SamplesCollected = Json.Int(c["samples"]);
            d.CrewSheltered = Json.Bool(c["sheltered"]);
            d.GrowLightsOn = Json.Bool(c["growLights"], true);
            d.O2GeneratorRunning = Json.Bool(c["o2Generator"], true);
            d.RecyclerRunning = Json.Bool(c["recycler"], true);
            d.SolarParticleEvent = Json.Bool(c["spe"]);
            d.SpeIntensity = Json.Num(c["speIntensity"]);

            foreach (var kv in Json.Obj(root["npcs"]))
            {
                var n = Json.Obj(kv.Value);
                d.NpcTrust[kv.Key] = Json.Num(n["trust"], 0.5);
                d.NpcFatigue[kv.Key] = Json.Num(n["fatigue"]);
                var mem = new List<string>();
                foreach (var m in Json.Arr(n["memory"])) mem.Add(Json.Str(m));
                d.NpcMemory[kv.Key] = mem;
            }
            foreach (var j in Json.Arr(root["journal"])) d.JournalObjectives.Add(Json.Str(j));
            foreach (var kv in Json.Obj(root["skills"])) d.Skills[kv.Key] = Json.Int(kv.Value);
            foreach (var j in Json.Arr(root["resolved"])) d.ResolvedIncidents.Add(Json.Str(j));
            foreach (var j in Json.Arr(root["ignored"])) d.IgnoredIncidents.Add(Json.Str(j));
            d.Extras = Json.Obj(root["extras"]);
            return d;
        }
    }

    /// <summary>
    /// Schema migration policy: old saves are upgraded field-by-field, never rejected.
    /// v1 -> v2 added crop stress, suit energy and extras.
    /// </summary>
    public static class SaveMigrator
    {
        public static SaveData Migrate(string json)
        {
            var data = SaveData.FromJson(json);
            if (data.SchemaVersion < 2)
            {
                data.SchemaVersion = 2;
            }
            return data;
        }
    }
}
