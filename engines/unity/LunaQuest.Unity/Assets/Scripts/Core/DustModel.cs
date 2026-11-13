using System;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Lunar dust. Regolith grains are sharp, cling to suits and seals, and degrade optical and
    /// thermal surfaces. Dust on panels is the single most visible maintenance loop in the game.
    /// </summary>
    public static class DustModel
    {
        public static void Step(ColonyState s, Time.GameClock clock, double realSeconds)
        {
            // Slow ambient accumulation. At this rate a clean array needs attention every few
            // mission days, which is the maintenance rhythm the GDD asks for.
            s.ArrayDust = Clamp(s.ArrayDust + 0.0003 * realSeconds, 0.0, 1.0);
            s.SensorDust = Clamp(s.SensorDust + 0.00017 * realSeconds, 0.0, 1.0);
            s.AirlockDust = Clamp(s.AirlockDust + 0.00025 * realSeconds, 0.0, 1.0);

            // Dust on arrays slowly pits the cover glass; heavy sensor dust corrupts readings
            // used by incidents. Most health loss comes from events, not from time alone.
            s.ArrayHealth = Clamp(s.ArrayHealth - s.ArrayDust * 0.0004 * realSeconds, 0.2, 1.0);
        }

        public static void CleanArrays(ColonyState s, double amount = 0.6)
            => s.ArrayDust = Clamp(s.ArrayDust - amount, 0.0, 1.0);

        public static void CleanSensors(ColonyState s, double amount = 0.8)
            => s.SensorDust = Clamp(s.SensorDust - amount, 0.0, 1.0);

        public static void CleanAirlock(ColonyState s, double amount = 0.9)
            => s.AirlockDust = Clamp(s.AirlockDust - amount, 0.0, 1.0);

        public static void OnAirlockUse(ColonyState s)
            => s.AirlockDust = Clamp(s.AirlockDust + 0.03, 0.0, 1.0);

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
