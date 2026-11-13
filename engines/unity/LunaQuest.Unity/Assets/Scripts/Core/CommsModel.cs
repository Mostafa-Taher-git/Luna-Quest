using System;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Earth link and local network. One-way light time is ~1.3 s; the umbilical is not the
    /// bottleneck, the relay and the antenna are. Loss of signal is a planning problem, never
    /// a permanent failure.
    /// </summary>
    public static class CommsModel
    {
        public static void Step(ColonyState s, Time.GameClock clock, double realSeconds)
        {
            // Antenna dust and relay wear degrade the link; solar noise adds a small penalty.
            double dustPenalty = 1.0 - Math.Min(0.45, s.SensorDust * 0.5);
            double sunNoise = 1.0 - 0.06 * Math.Max(0.0, Math.Sin(clock.LunarPhase * 2.0 * Math.PI));
            s.SignalQuality = Clamp(s.RelayHealth * dustPenalty * sunNoise, 0.0, 1.0);
            s.EarthLinkUp = s.SignalQuality > 0.25;
        }

        public static string Band(ColonyState s)
            => s.SignalQuality > 0.75 ? "nominal" : s.SignalQuality > 0.45 ? "degraded" : s.EarthLinkUp ? "weak" : "lost";

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
