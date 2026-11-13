using System;
using LunaQuest.Core.Time;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Water storage, crew use and closed-loop recycling. Real anchor: ISS-class water
    /// recovery is about 93%. Nominal crew use is ~11 L per person per day including hygiene.
    /// </summary>
    public static class WaterModel
    {
        public const double LitresPerPersonDay = 11.0;

        public static void Step(ColonyState s, GameClock clock, double realSeconds)
        {
            double gameDays = realSeconds * GameClock.MinutesPerSecond / GameClock.MinutesPerDay;

            double demand = LitresPerPersonDay * s.CrewCount * gameDays;
            demand = Math.Min(demand, s.WaterL);

            double recovered = 0.0;
            if (s.RecyclerRunning && s.FilterHealth > 0.05)
            {
                recovered = demand * s.RecyclerEfficiency * s.FilterHealth * s.WaterQuality;
            }

            s.WaterL = Clamp(s.WaterL - demand + recovered, 0.0, s.WaterCapacityL);

            // Filter clogs with throughput: roughly 4% of capacity per mission day at full crew,
            // so the player has to back-flush it about every two weeks.
            s.FilterHealth = Clamp(s.FilterHealth - demand * 0.0004, 0.0, 1.0);

            // Water quality drifts down when the filter is neglected and recovers with good filters.
            double qualityDelta = (s.FilterHealth - 0.5) * 0.00002;
            s.WaterQuality = Clamp(s.WaterQuality + qualityDelta, 0.4, 1.0);

            if (s.WaterL < 40.0) s.CropWaterStress = Math.Min(1.0, s.CropWaterStress + 0.01);
            else s.CropWaterStress = Math.Max(0.0, s.CropWaterStress - 0.004);
        }

        /// <summary>Water produced/consumed by plant transpiration is closed-loop; this is the net loss.</summary>
        public static double NetLossLitresPerDay(ColonyState s)
            => LitresPerPersonDay * s.CrewCount * (1.0 - s.RecyclerEfficiency * s.FilterHealth * s.WaterQuality);

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
