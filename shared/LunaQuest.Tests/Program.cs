using System;
using System.Collections.Generic;
using System.IO;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Save;
using LunaQuest.Core.Sim;

namespace LunaQuest.Tests
{
    /// <summary>
    /// Dependency-free test harness. It exercises the same LunaQuest.Core assembly that
    /// Unity and Godot load, so a green run here means both engines get identical rules.
    /// </summary>
    internal static class Program
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> Failures = new List<string>();
        private static string _contentDir = "";

        private static int Main()
        {
            _contentDir = FindContentDirectory();
            Console.WriteLine("Luna-Quest core tests");
            Console.WriteLine($"content: {_contentDir}");
            Console.WriteLine(new string('-', 64));

            ContentTests();
            SimulationTests();
            ResourcePerSecondTests();
            ActionTests();
                        EvaTests();
IncidentTests();
            SaveTests();
            DeterminismTests();
            MissionRunTests();
            TerrainTests();

            Console.WriteLine(new string('-', 64));
            Console.WriteLine($"passed {_passed}, failed {_failed}");
            if (_failed > 0)
            {
                Console.WriteLine();
                foreach (var f in Failures) Console.WriteLine("  FAIL  " + f);
                return 1;
            }
            return 0;
        }

        // ---- tests -----------------------------------------------------------

        private static void TerrainTests()
        {
            var t = new LunaQuest.Core.Terrain.LunarTerrain(7);
            var t2 = new LunaQuest.Core.Terrain.LunarTerrain(7);
            bool same = true, flat = true, bounded = true;
            double maxH = double.MinValue, minH = double.MaxValue, maxStep = 0;
            var slopes = new List<double>();
            for (int i = -400; i <= 400; i += 7)
            for (int j = -400; j <= 400; j += 7)
            {
                double h = t.Height(i, j);
                if (h != t2.Height(i, j)) same = false;
                double d = Math.Sqrt(i * (double)i + (j - 8.0) * (j - 8.0));
                if (d < 88 && Math.Abs(h) > 1e-9) flat = false;
                if (double.IsNaN(h) || Math.Abs(h) > 80) bounded = false;
                maxH = Math.Max(maxH, h); minH = Math.Min(minH, h);
                maxStep = Math.Max(maxStep, Math.Abs(t.Height(i + 0.5, j) - h));
                if (d > 100) slopes.Add(t.SlopeDegrees(i, j));
            }
            Check("terrain is deterministic for a seed", same);
            Check("terrain is exactly flat under the colony", flat);
            Check("terrain stays finite and inside +/-80 m", bounded, $"range {minH:0.0}..{maxH:0.0}");
            Check("terrain has real relief outside the pad", maxH - minH > 10.0, $"range {maxH - minH:0.0} m");
            Check("terrain is continuous at 0.5 m", maxStep < 3.0, $"max step {maxStep:0.00} m");
            slopes.Sort();
            double p99 = slopes[(int)(slopes.Count * 0.99)];
            Check("99% of terrain is walkable (slope < 32 deg)", p99 < 32.0, $"p99 {p99:0.0} deg");
            var other = new LunaQuest.Core.Terrain.LunarTerrain(99);
            Check("different sites have different ground", Math.Abs(other.Height(300, 300) - t.Height(300, 300)) > 1e-6);
        }

        private static void ContentTests()
        {
            var db = ContentDatabase.LoadFromDirectory(_contentDir);

            Check("16 buildings load", db.Buildings.Count == 16, $"got {db.Buildings.Count}");
            Check("6 crew + NEXO load", db.Npcs.Count == 7, $"got {db.Npcs.Count}");
            Check("exactly one robot", db.Npcs.FindAll(n => n.IsRobot).Count == 1);
            foreach (var want in new[] { "Dr. Safaa", "Noor", "Dr. Alaa", "Abdulaziz", "Ahmed", "Mostafa", "NEXO" })
                Check("crew member " + want, db.Npcs.Exists(n => n.NameEn == want));
            Check("no unnamed crew", !db.Npcs.Exists(n => n.NamePending));
            Check("15 incidents load", db.Incidents.Count == 15, $"got {db.Incidents.Count}");
            Check("9 crops load", db.Crops.Count == 9, $"got {db.Crops.Count}");
            Check("11 learning objectives load", db.Learning.Count == 11, $"got {db.Learning.Count}");
            Check("17 research records load", db.Research.Count == 17, $"got {db.Research.Count}");

            var buildingIds = new HashSet<string>();
            foreach (var b in db.Buildings) buildingIds.Add(b.Id);

            foreach (var b in db.Buildings)
            {
                Check($"building {b.Id} has an id and name", !string.IsNullOrEmpty(b.Id) && !string.IsNullOrEmpty(b.NameEn));
                if (b.Category == BuildingCategory.Functional)
                {
                    Check($"functional building {b.Id} has an interior", b.Rooms.Count > 0, "no rooms");
                    Check($"functional building {b.Id} has interactions", b.Interactions.Count > 0, "no interactions");
                    Check($"functional building {b.Id} is valid", b.IsValid, "failed IsValid");
                }
                foreach (var i in b.Interactions)
                {
                    Check($"interaction {b.Id}/{i.Id} has a room", b.Rooms.Exists(r => r.Id == i.RoomId), i.RoomId);
                    if (!string.IsNullOrEmpty(i.LearningObjectiveId))
                        Check($"interaction {b.Id}/{i.Id} learning id exists", db.Objective(i.LearningObjectiveId) != null, i.LearningObjectiveId);
                }
            }

            var npcIds = new HashSet<string>();
            foreach (var n in db.Npcs) npcIds.Add(n.Id);
            foreach (var n in db.Npcs)
            {
                Check($"npc {n.Id} location exists", buildingIds.Contains(n.LocationId), n.LocationId);
                Check($"npc {n.Id} has a schedule", n.Schedule.Count > 0);
                Check($"npc {n.Id} has two relationships", n.Relationships.Count >= 2, $"{n.Relationships.Count}");
                foreach (var e in n.Schedule)
                    Check($"npc {n.Id} schedule location exists", buildingIds.Contains(e.LocationId), e.LocationId);
                if (n.NamePending)
                    Check($"pending npc {n.Id} has no invented English name", string.IsNullOrEmpty(n.NameEn), n.NameEn);
            }

            foreach (var d in db.Incidents)
            {
                Check($"incident {d.Id} has an owner", string.IsNullOrEmpty(d.OwnerNpcId) || npcIds.Contains(d.OwnerNpcId), d.OwnerNpcId);
                Check($"incident {d.Id} location exists", string.IsNullOrEmpty(d.LocationId) || buildingIds.Contains(d.LocationId), d.LocationId);
                Check($"incident {d.Id} learning id exists", db.Objective(d.LearningObjectiveId) != null, d.LearningObjectiveId);
                Check($"incident {d.Id} has a resolution action", !string.IsNullOrEmpty(d.RequiredAction));
            }

            foreach (var l in db.Learning)
                Check($"learning {l.Id} states its evidence class", !string.IsNullOrEmpty(l.EvidenceClass));

            foreach (var r in db.Research)
            {
                Check($"research {r.Id} has a URL", r.Url.StartsWith("http", StringComparison.Ordinal), r.Url);
                Check($"research {r.Id} states a boundary", !string.IsNullOrEmpty(r.BoundaryEn) || !string.IsNullOrEmpty(r.UseEn));
            }
        }

        private static void SimulationTests()
        {
            var sim = NewSim();
            Check("mission starts at day 1", sim.Clock.Day == 1, $"{sim.Clock.Day}");
            Check("seven people breathe", sim.State.CrewCount == 7, $"{sim.State.CrewCount}");
            Check("starting battery inside bounds", InRange(sim.State.BatteryKwh, 0, sim.State.BatteryCapacityKwh));

            double water0 = sim.State.WaterL;
            double food0 = sim.State.FoodKcal;
            double o2 = sim.State.O2Kg;

            for (int i = 0; i < 120; i++) sim.Step(1.0); // one game day

            Check("one day advances the clock by 1440 sim minutes",
                Math.Abs(sim.Clock.TotalMinutes - 1440.0) < 1e-6, $"{sim.Clock.TotalMinutes}");
            Check("water is consumed in a day", sim.State.WaterL < water0, $"{water0} -> {sim.State.WaterL}");
            Check("food is consumed in a day", sim.State.FoodKcal < food0, $"{food0} -> {sim.State.FoodKcal}");
            Check("oxygen is consumed in a day", sim.State.O2Kg < o2, $"{o2} -> {sim.State.O2Kg}");
            Check("food is not negative", sim.State.FoodKcal >= 0);
            Check("water stays inside its tank", InRange(sim.State.WaterL, 0, sim.State.WaterCapacityL));
            Check("CO2 is a sane concentration", sim.State.Co2Ppm > 300 && sim.State.Co2Ppm < 20000, $"{sim.State.Co2Ppm:0}");
            Check("cabin temperature is stable", sim.State.CabinTempC > 10 && sim.State.CabinTempC < 30, $"{sim.State.CabinTempC:0.0}");
            Check("battery is not negative", sim.State.BatteryKwh >= 0);
            Check("signal in range", InRange(sim.State.SignalQuality, 0, 1));
            Check("crop biomass in range", InRange(sim.State.CropBiomassKg, 0, 1));
            Check("no NaN in power", !double.IsNaN(sim.State.SolarKw));
        }

        private static void ResourcePerSecondTests()
        {
            var sim = NewSim();
            double water = sim.State.WaterL;
            double food = sim.State.FoodKcal;
            double o2 = sim.State.O2Kg;
            sim.Step(1.0);
            Check("one real second changes water", sim.State.WaterL != water, $"{water} -> {sim.State.WaterL}");
            Check("one real second changes food", sim.State.FoodKcal != food, $"{food} -> {sim.State.FoodKcal}");
            Check("one real second changes oxygen", sim.State.O2Kg != o2, $"{o2} -> {sim.State.O2Kg}");
            Check("one real second advances 12 sim minutes", Math.Abs(sim.Clock.TotalMinutes - 12.0) < 1e-9, $"{sim.Clock.TotalMinutes}");
        }

        private static void EvaTests()
        {
            var db = ContentDatabase.LoadFromDirectory(_contentDir);
            var sim = new ColonySimulation(db);
            Check("suit starts full", sim.State.SuitEnergy == 1.0);

            int warning = -1;
            for (int i = 0; i < 400 && warning < 3; i++)
                warning = sim.TickEvaEnergy(false, 1.0);
            Check("emptying suit raises the empty warning", warning == 3);
            Check("emptying suit drains to zero", sim.State.SuitEnergy == 0.0, $"{sim.State.SuitEnergy}");
            Check("empty suit refuses the drill", !sim.DrillSample(out _, out _));
            Check("dry-suit journal entry written", sim.Learning.Journal.Exists(e => e.ObjectiveId == "eva_drain"));

            for (int i = 0; i < 80; i++) sim.TickEvaEnergy(true, 1.0);
            Check("indoors recharges the suit", sim.State.SuitEnergy > 0.0, $"{sim.State.SuitEnergy}");

            Check("charged suit drills", sim.DrillSample(out _, out _));
            Check("drill refuses a second sample", !sim.DrillSample(out _, out _));
            Check("logging custody seals the chain", sim.LogSampleAtCache(out _, out _));
            Check("samples counted", sim.State.SamplesCollected == 1);
            Check("custody pays data", sim.State.DataPoints >= 10.0, $"{sim.State.DataPoints}");
            Check("custody unlocks the sample objective", sim.Learning.HasEntry("obj_sample"));

            var cargo = new ColonySimulation(db);
            Check("the pad crate can be carried", cargo.StartCargoRun(0.0, out _, out _));
            Check("already carrying refuses a second grab", !cargo.StartCargoRun(0.0, out _, out _));
            Check("delivery succeeds", cargo.CompleteCargoRun(out _, out _));
            Check("on-time delivery pays 15", cargo.State.DataPoints >= 15.0, $"{cargo.State.DataPoints}");
            Check("on-time delivery writes its journal entry",
                cargo.Learning.Journal.Exists(e => e.ObjectiveId == "cargo_run"));

            var late = new ColonySimulation(db);
            late.StartCargoRun(0.0, out _, out _);
            late.TickCargo(200.0); // window is 150 s of board time
            Check("late delivery still completes", late.CompleteCargoRun(out _, out _));
            Check("late delivery pays 8 and journals honestly",
                late.State.DataPoints >= 8.0 && late.Learning.Journal.Exists(e => e.ObjectiveId == "cargo_run_late"));
        }

        private static void ActionTests()
        {
            var db = ContentDatabase.LoadFromDirectory(_contentDir);
            var sim = new ColonySimulation(db);

            sim.State.ArrayDust = 0.8;
            Check("clean arrays succeeds", sim.TryAction(PlayerAction.CleanArrays, out _, out _));
            Check("cleaning arrays removes dust", sim.State.ArrayDust < 0.3, $"{sim.State.ArrayDust}");

            sim.State.FilterHealth = 0.4;
            sim.TryAction(PlayerAction.CleanFilter, out _, out _);
            Check("filter service raises health", sim.State.FilterHealth > 0.7, $"{sim.State.FilterHealth}");

            sim.State.ScrubberHealth = 0.3;
            sim.TryAction(PlayerAction.ReplaceScrubber, out _, out _);
            Check("scrubber replacement restores health", sim.State.ScrubberHealth > 0.99);

            sim.State.CropBiomassKg = 1.0;
            double before = sim.State.FoodKcal;
            Check("harvest succeeds when ready", sim.TryAction(PlayerAction.HarvestCrops, out _, out _));
            Check("harvest adds food", sim.State.FoodKcal > before, $"{before} -> {sim.State.FoodKcal}");
            Check("harvest resets the pod", sim.State.CropBiomassKg == 0);

            sim.State.SolarParticleEvent = true;
            sim.State.SpeIntensity = 0.8;
            sim.State.CrewSheltered = false;
            sim.Step(1.0);
            double unsheltered = sim.State.DoseRateMicroGyH;
            sim.TryAction(PlayerAction.ShelterCrew, out _, out _);
            sim.Step(1.0);
            Check("shelter cuts the dose rate", sim.State.DoseRateMicroGyH < unsheltered * 0.5,
                $"{unsheltered} -> {sim.State.DoseRateMicroGyH}");

            int samples = sim.State.SamplesCollected;
            sim.TryAction(PlayerAction.CollectSample, out _, out _);
            Check("sample collection increments the log", sim.State.SamplesCollected == samples + 1);
            Check("sample unlocks its journal entry", sim.Learning.HasEntry("obj_sample"));

            sim.Learning.Unlock("obj_solar_dust", 10);
            Check("objective unlocks once", sim.Learning.Unlock("obj_solar_dust", 20) == null);
            Check("skill evidence recorded", sim.Learning.EvidenceFor("cleaning") > 0);

            Check("interaction clean_arrays maps to action", sim.Interact("power_farm", "clean_arrays", out _, out _));
            Check("unknown interaction is refused", !sim.Interact("habitat", "dance", out _, out _));

            // Observation stations must teach something, otherwise half the consoles in the
            // colony are furniture. Reading the ice probe logs the provenance objective.
            var reader = new ColonySimulation(db);
            Check("read station succeeds", reader.Interact("ice_site", "ice_probe", out string readEn, out _),
                readEn);
            Check("read station unlocks its journal entry", reader.Learning.HasEntry("obj_sample"));
            Check("read station counts as evidence", reader.State.DataPoints > 0);

            // Every console in buildings.json must map to real behaviour. Harvest is skipped
            // because a fresh colony has no ripe biomass yet; it is covered just above.
            int unmapped = 0;
            string unmappedIds = "";
            foreach (var building in db.Buildings)
            {
                foreach (var point in building.Interactions)
                {
                    if (point.Id == "harvest") continue;
                    var probe = new ColonySimulation(db);
                    if (!probe.Interact(building.Id, point.Id, out _, out _))
                    {
                        unmapped++;
                        unmappedIds += $"{building.Id}/{point.Id} ";
                    }
                }
            }
            Check("every console maps to an action", unmapped == 0, unmappedIds.Trim());

            var mapped = new ColonySimulation(db);
            mapped.State.RelayHealth = 0.4;
            mapped.Interact("mission_control", "comms_desk", out _, out _);
            Check("comms desk repairs the relay", mapped.State.RelayHealth > 0.7, $"{mapped.State.RelayHealth}");

            mapped.State.SensorDust = 0.9;
            mapped.Interact("comms_tower", "antenna", out _, out _);
            Check("antenna cleaning removes sensor dust", mapped.State.SensorDust < 0.3, $"{mapped.State.SensorDust}");

            mapped.State.AirlockDust = 0.8;
            mapped.Interact("airlock", "dust_removal", out _, out _);
            Check("dust station cleans the airlock", mapped.State.AirlockDust < 0.3, $"{mapped.State.AirlockDust}");

            bool lights = mapped.State.GrowLightsOn;
            mapped.Interact("greenhouse", "grow_lights", out _, out _);
            Check("grow lights toggle", mapped.State.GrowLightsOn != lights);

            mapped.State.AverageFatigue = 0.8;
            mapped.Interact("medical", "checkup", out _, out _);
            Check("check-up lowers crew fatigue", mapped.State.AverageFatigue < 0.75, $"{mapped.State.AverageFatigue}");

            double oxygen = mapped.State.O2Kg;
            mapped.Interact("regolith_plant", "intake", out _, out _);
            Check("regolith intake produces oxygen", mapped.State.O2Kg > oxygen, $"{oxygen} -> {mapped.State.O2Kg}");
        }

        private static void IncidentTests()
        {
            var db = ContentDatabase.LoadFromDirectory(_contentDir);
            var sim = new ColonySimulation(db);
            sim.Clock.LoadFrom(3 * 1440 + 600);

            sim.State.ArrayDust = 0.9;
            var raised = sim.Incidents.Evaluate(sim.State, sim.Clock.TotalMinutes);
            Check("high dust raises the dust incident", raised.Exists(r => r.Id == "inc_array_dust"),
                string.Join(",", raised.ConvertAll(r => r.Id)));

            Check("incident resolution requires the right action",
                !sim.Incidents.Resolve("inc_array_dust", "rest"));
            Check("incident resolves with its action",
                sim.Incidents.Resolve("inc_array_dust", "clean_arrays"));

            sim.Incidents.DismissResolved();
            Check("resolved incidents leave the board", sim.Incidents.Active.Count == 0);

            // A raised incident must not fire again inside its cooldown.
            sim.State.ArrayDust = 0.9;
            var again = sim.Incidents.Evaluate(sim.State, sim.Clock.TotalMinutes + 10);
            Check("cooldown suppresses duplicate incident", !again.Exists(r => r.Id == "inc_array_dust"));

            sim.State.BatteryKwh = sim.State.BatteryCapacityKwh * 0.1;
            sim.Clock.LoadFrom(20 * 1440);
            var power = sim.Incidents.Evaluate(sim.State, sim.Clock.TotalMinutes);
            Check("low battery raises the power incident", power.Exists(r => r.Id == "inc_power_low"),
                string.Join(",", power.ConvertAll(r => r.Id)));
        }

        private static void SaveTests()
        {
            var db = ContentDatabase.LoadFromDirectory(_contentDir);
            var sim = new ColonySimulation(db);
            sim.PlayerName = "Test Commander";
            sim.Language = "ar";
            sim.Clock.LoadFrom(5 * 1440 + 300);
            sim.State.ArrayDust = 0.42;
            sim.State.WaterL = 512.5;
            sim.State.FoodKcal = 400000;
            sim.State.SamplesCollected = 3;
            sim.State.CrewSheltered = true;
            sim.TryAction(PlayerAction.CollectSample, out _, out _);
            sim.Npcs.AddTrust("npc_nour", 0.2);
            sim.Npcs.Remember("npc_nour", "filter_fixed");

            var saved = SaveData.Capture(sim);
            string json = saved.ToJson();
            var loaded = SaveData.FromJson(json);
            var restored = new ColonySimulation(db);
            loaded.Apply(restored);

            Check("save keeps player name", restored.PlayerName == "Test Commander");
            Check("save keeps language", restored.Language == "ar");
            Check("save keeps clock", Math.Abs(restored.Clock.TotalMinutes - sim.Clock.TotalMinutes) < 1e-6);
            Check("save keeps water", Math.Abs(restored.State.WaterL - sim.State.WaterL) < 1e-6);
            Check("save keeps food", Math.Abs(restored.State.FoodKcal - sim.State.FoodKcal) < 1e-6);
            Check("save keeps dust", Math.Abs(restored.State.ArrayDust - sim.State.ArrayDust) < 1e-6);
            Check("save keeps samples", restored.State.SamplesCollected == sim.State.SamplesCollected);
            Check("save keeps shelter flag", restored.State.CrewSheltered);
            Check("save keeps NPC trust", Math.Abs(restored.Npcs.State("npc_nour").Trust - 0.7) < 1e-6,
                $"{restored.Npcs.State("npc_nour").Trust}");
            Check("save keeps NPC memory", restored.Npcs.State("npc_nour").HasTag("filter_fixed"));
            Check("save keeps journal", restored.Learning.HasEntry("obj_sample"));
            Check("save rejects a future schema", Throws(() => SaveData.FromJson("{\"schemaVersion\":99}")));
            Check("save rejects corrupt JSON", Throws(() => SaveData.FromJson("{")));
        }

        private static void DeterminismTests()
        {
            var a = NewSim();
            var b = NewSim();
            for (int i = 0; i < 600; i++) { a.Step(1.0); b.Step(1.0); }
            Check("same input gives same battery", a.State.BatteryKwh == b.State.BatteryKwh,
                $"{a.State.BatteryKwh} vs {b.State.BatteryKwh}");
            Check("same input gives same water", a.State.WaterL == b.State.WaterL);
            Check("same input gives same CO2", a.State.CO2Kg == b.State.CO2Kg);
            Check("same input gives same clock", a.Clock.TotalMinutes == b.Clock.TotalMinutes);

            var saveA = SaveData.Capture(a).ToJson();
            var saveB = SaveData.Capture(b).ToJson();
            Check("deterministic saves match", StripTimestamp(saveA) == StripTimestamp(saveB));
        }

        private static void MissionRunTests()
        {
            var sim = NewSim();
            // 30 Earth days at 12 sim-minutes per real second = 60 real minutes.
            for (int i = 0; i < 3600; i++) sim.Step(1.0);

            Check("30-day mission completes", sim.Clock.IsMissionComplete, $"day {sim.Clock.Day}");
            Check("mission progress reaches 1", Math.Abs(sim.Clock.MissionProgress - 1.0) < 0.001,
                $"{sim.Clock.MissionProgress}");
            Check("no NaN after a full mission", !double.IsNaN(sim.State.BatteryKwh) && !double.IsNaN(sim.State.WaterL));
            Check("no permanent death flag", !sim.State.GameOver);
            Check("resources stay inside physical limits",
                InRange(sim.State.WaterL, 0, sim.State.WaterCapacityL) &&
                InRange(sim.State.O2Kg, 0, sim.State.O2CapacityKg) &&
                InRange(sim.State.CropBiomassKg, 0, 1) &&
                InRange(sim.State.CropHealth, 0, 1) &&
                InRange(sim.State.SignalQuality, 0, 1));
            Check("cabin temperature stays survivable", sim.State.CabinTempC > 0 && sim.State.CabinTempC < 40,
                $"{sim.State.CabinTempC:0.0}");
            Check("authored mission beats fired", sim.SeenEventIds.Contains("welcome")
                && sim.SeenEventIds.Contains("spe_onset") && sim.SeenEventIds.Contains("mission_end"),
                string.Join(",", sim.SeenEventIds));
            Check("events were produced", TotalEvents(sim) > 0);
        }

        // ---- helpers ---------------------------------------------------------

        private static ColonySimulation NewSim()
            => new ColonySimulation(ContentDatabase.LoadFromDirectory(_contentDir));

        private static int TotalEvents(ColonySimulation sim)
        {
            // Stepping again produces the notices that prove the event channel is alive.
            return sim.Step(1.0).Count;
        }

        private static bool InRange(double v, double lo, double hi) => v >= lo && v <= hi;

        private static bool Throws(Action a)
        {
            try { a(); return false; } catch { return true; }
        }

        private static string StripTimestamp(string json)
        {
            var lines = json.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains("\"savedAt\"")) lines[i] = "  \"savedAt\": \"<ts>\",";
            return string.Join("\n", lines);
        }

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                _passed++;
                Console.WriteLine("  ok    " + name);
            }
            else
            {
                _failed++;
                string msg = string.IsNullOrEmpty(detail) ? name : $"{name}  ({detail})";
                Failures.Add(msg);
                Console.WriteLine("  FAIL  " + msg);
            }
        }

        private static string FindContentDirectory()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "content"),
                Path.Combine(Directory.GetCurrentDirectory(), "content"),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "content")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "content")),
            };
            foreach (var c in candidates)
                if (File.Exists(Path.Combine(c, "buildings.json"))) return c;
            throw new DirectoryNotFoundException("content/ not found. Run from the repo root or pass the repo as the working directory.");
        }
    }
}
