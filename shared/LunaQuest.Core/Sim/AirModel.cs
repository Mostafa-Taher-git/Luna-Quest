using System;
using LunaQuest.Core.Time;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Cabin atmosphere. Real anchors: ~0.84 kg O2 per person per day consumed and ~1.0 kg CO2
    /// produced. A 300 m³ cabin at 101.3 kPa holds ~84 kg O2 at 21%.
    /// </summary>
    public static class AirModel
    {
        public const double O2KgPerPersonDay = 0.84;
        public const double CO2KgPerPersonDay = 1.00;
        public const double CabinVolumeM3 = 300.0;

        public static void Step(ColonyState s, GameClock clock, double realSeconds)
        {
            double gameDays = realSeconds * GameClock.MinutesPerSecond / GameClock.MinutesPerDay;
            double dtHours = realSeconds * GameClock.MinutesPerSecond / 60.0;

            double o2Use = O2KgPerPersonDay * s.CrewCount * gameDays;
            double co2Made = CO2KgPerPersonDay * s.CrewCount * gameDays;

            s.O2Kg = Math.Max(0.0, s.O2Kg - o2Use);
            s.CO2Kg += co2Made;

            // Electrolysis: 1 kg of water yields ~0.889 kg O2. The stack runs at 90% of the
            // crew's demand, so the stored reserve declines slowly and the greenhouse's
            // photosynthetic oxygen is a real (if small) contribution. Life support keeps
            // priority; PowerModel already shuts the stack down when the battery is empty.
            if (s.O2GeneratorRunning && s.WaterL > 20.0)
            {
                double o2Made = o2Use * 0.90;
                double waterUsed = o2Made / 0.889;
                s.WaterL = Math.Max(0.0, s.WaterL - waterUsed);
                s.O2Kg = Math.Min(s.O2CapacityKg, s.O2Kg + o2Made);
            }
            else if (!s.O2GeneratorRunning)
            {
                s.O2Kg = Math.Max(0.0, s.O2Kg - o2Use * 0.05); // suit reserves / slow bleed
            }

            // CO2 scrubber: a healthy unit holds the cabin near the 400 ppm baseline instead of
            // removing a fixed percentage. A worn unit lets the excess grow, which is what the
            // player sees on the gauge. Rate is per hour of cabin volume turnover.
            double baselineKg = 0.24; // ~400 ppm at 300 m³
            double excess = Math.Max(0.0, s.CO2Kg - baselineKg);
            double scrubRatePerHour = 0.55 * Math.Max(0.0, s.ScrubberHealth);
            double removed = excess * (1.0 - Math.Exp(-scrubRatePerHour * dtHours));
            s.CO2Kg = Math.Max(0.0, s.CO2Kg - removed);
            s.CO2Kg = Math.Max(0.0, s.CO2Kg - 0.0002 * realSeconds); // slow cabin leakage

            // Pressure tracks total gas; small leak, compensated by the O2 generator.
            s.CabinPressureKpa = Clamp(
                s.CabinPressureKpa - 0.00002 * realSeconds,
                80.0,
                101.3);
            if (s.O2GeneratorRunning && s.LabAndGreenhousePowered)
                s.CabinPressureKpa = Math.Min(101.3, s.CabinPressureKpa + 0.00003 * realSeconds);

            // Scrubber wear: about 3% of capacity per mission day, so a 30-day mission
            // forces at least one replacement without failing on its own.
            s.ScrubberHealth = Clamp(s.ScrubberHealth - 0.00025 * realSeconds, 0.0, 1.0);
        }

        public static bool Co2Warning(ColonyState s) => s.Co2Ppm > 5000.0;
        public static bool Co2Danger(ColonyState s) => s.Co2Ppm > 10000.0;
        public static bool O2Low(ColonyState s) => s.O2Percent < 0.30;

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
