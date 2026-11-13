using System;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Radiation exposure. Real anchor: Chang'e-4 LND measured 13.2 ± 1 µGy/h absorbed dose in
    /// silicon on the lunar surface. That is a silicon dose, not a human dose equivalent; the game
    /// labels it as a simplified model. Solar particle events are rare, serious and shelterable.
    /// </summary>
    public static class RadiationModel
    {
        public const double SurfaceMicroGyH = 13.2;

        public static void Step(ColonyState s, Time.GameClock clock, double realSeconds)
        {
            double hours = realSeconds * Time.GameClock.MinutesPerSecond / 60.0;

            double rate = SurfaceMicroGyH;
            if (s.SolarParticleEvent)
                rate *= 1.0 + 120.0 * s.SpeIntensity;

            // Shelter and regolith berms cut the dose substantially.
            if (s.CrewSheltered) rate *= 0.25;

            s.DoseRateMicroGyH = rate;
            s.CumulativeDoseMsv += rate * hours / 1000.0;
        }

        /// <summary>Dose a crew member would receive in one game day at the current rate (mSv).</summary>
        public static double DailyDoseMsv(ColonyState s)
            => s.DoseRateMicroGyH * 24.0 / 1000.0;
    }
}
