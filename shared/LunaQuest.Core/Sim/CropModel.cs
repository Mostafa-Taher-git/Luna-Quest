using System;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Crop production. Real anchors from CSA's Interplanetary Farming and NASA Veggie/EDEN ISS:
    /// lettuce 21-28 days, soybean 45-65, tomato 50-90, carrot 70-80, chickpea 80-100.
    /// Plants here are always enclosed: no direct lunar sunlight, no unshielded radiation.
    /// Light comes from the colony's power, CO2 from the cabin, water from storage.
    /// </summary>
    public static class CropModel
    {
        public const double PodAreaM2 = 24.0;      // four 6 m² racks inside the sealed grow pod
        public const double WaterLPerM2Day = 3.0;
        public const double Co2KgPerM2Day = 0.03;
        public const double LightKwPerM2 = 0.05;
        public const double HarvestKcalPerM2 = 450.0;
        public const double CycleDays = 24.0; // lettuce reference cycle

        public static void Step(ColonyState s, Time.GameClock clock, double realSeconds)
        {
            double gameDays = realSeconds * Time.GameClock.MinutesPerSecond / Time.GameClock.MinutesPerDay;
            double dt = gameDays / CycleDays; // fraction of a crop cycle per step

            bool lightOk = s.GrowLightsOn && s.LabAndGreenhousePowered && s.BatteryKwh > 0.5;
            bool waterOk = s.WaterL > 30.0 && s.CropWaterStress < 0.5;
            bool airOk = s.Co2Ppm > 350.0;

            double growth = 0.0;
            if (lightOk && waterOk && airOk)
                growth = dt * (0.55 + 0.45 * s.CropHealth);

            s.CropBiomassKg = Math.Min(1.0, s.CropBiomassKg + growth);

            // Consume the inputs for the light-on period only. Irrigation water is 90% recovered
            // by the condensate loop, so only the net loss leaves the tank; the full figure is
            // reported to the player so the greenhouse's real demand stays visible.
            if (lightOk)
            {
                s.WaterL = Math.Max(0.0, s.WaterL - PodAreaM2 * WaterLPerM2Day * gameDays * 0.10);
                double co2Used = Math.Min(s.CO2Kg, PodAreaM2 * Co2KgPerM2Day * gameDays);
                s.CO2Kg = Math.Max(0.0, s.CO2Kg - co2Used);
                // Photosynthesis returns oxygen to the cabin at the stoichiometric ratio
                // (32 kg O2 per 44 kg CO2). It supplements life support; it cannot replace it.
                s.O2Kg = Math.Min(s.O2CapacityKg, s.O2Kg + co2Used * (32.0 / 44.0));
                s.CropLightHours += realSeconds * Time.GameClock.MinutesPerSecond / 60.0;
            }

            // Stress reduces health; recovery is slower than damage.
            if (!lightOk || !waterOk || !airOk)
            {
                s.CropHealth = Math.Max(0.0, s.CropHealth - 0.0006 * realSeconds);
                s.CropBiomassKg = Math.Max(0.0, s.CropBiomassKg - 0.00002 * realSeconds);
            }
            else
            {
                s.CropHealth = Math.Min(1.0, s.CropHealth + 0.00004 * realSeconds);
            }
        }

        public static bool ReadyToHarvest(ColonyState s) => s.CropBiomassKg >= 0.98;

        /// <summary>Harvest returns food energy and resets the pod. Returns kcal gained.</summary>
        public static double Harvest(ColonyState s)
        {
            if (!ReadyToHarvest(s)) return 0.0;
            double kcal = HarvestKcalPerM2 * PodAreaM2 * Math.Max(0.4, s.CropHealth);
            s.FoodKcal = Math.Min(s.FoodCapacityKcal, s.FoodKcal + kcal);
            s.CropBiomassKg = 0.0;
            s.CropLightHours = 0.0;
            s.CropHealth = Math.Min(1.0, s.CropHealth + 0.05);
            return kcal;
        }

        public static string Stage(ColonyState s)
        {
            if (s.CropBiomassKg < 0.15) return "seedling";
            if (s.CropBiomassKg < 0.55) return "vegetative";
            if (s.CropBiomassKg < 0.95) return "maturing";
            return "ready";
        }
    }
}
