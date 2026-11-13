using System;
using LunaQuest.Core.Time;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Solar array, battery and load priority. Real anchor: a ~100 m² array at 22% efficiency
    /// yields ~30 kW at full solar constant (1361 W/m²). Polar grazing light means full power
    /// is rare; the battery carries the long shadow.
    /// </summary>
    public static class PowerModel
    {
        public static void Step(ColonyState s, GameClock clock, double realSeconds)
        {
            double dtHours = realSeconds * GameClock.MinutesPerSecond / 60.0;

            // Grazing polar sunlight, dust and array health.
            double irradiance = clock.SolarIrradianceFraction;
            double dustFactor = 1.0 - Math.Min(0.85, s.ArrayDust * 0.9);
            s.SolarKw = s.ArrayPeakKw * irradiance * s.ArrayHealth * dustFactor;

            double load = s.BaseLoadKw;
            if (s.O2GeneratorRunning) load += s.O2GeneratorKw;
            if (s.GrowLightsOn) load += 2.0;
            load += s.HeaterKw; // thermostat holds the cabin; the heater is a real load

            // Priority shed: keep life support, drop the lab and greenhouse first.
            double net = s.SolarKw - load;
            s.LabAndGreenhousePowered = true;
            if (s.BatteryKwh + net * dtHours < s.BatteryCapacityKwh * 0.20 && s.GrowLightsOn)
            {
                // Trial: shed and recompute.
                double shedNet = s.SolarKw - (load - 2.0);
                if (shedNet > net) { net = shedNet; load -= 2.0; s.LabAndGreenhousePowered = false; }
            }

            s.BatteryKwh = Clamp(s.BatteryKwh + net * dtHours, 0.0, s.BatteryCapacityKwh);

            // Never a hard failure: at empty battery the colony runs on emergency reserves
            // with reduced service, and the UI explains it (GDD: no permanent loss state).
            if (s.BatteryKwh <= 0.01)
            {
                s.O2GeneratorRunning = false;
                s.GrowLightsOn = false;
                s.LabAndGreenhousePowered = false;
                if (s.GrowLightsResumeAtMinute < 0)
                    s.GrowLightsResumeAtMinute = clock.TotalMinutes + 360.0;
            }

            // A shed is temporary. When the pack has recovered, the grow lights return on their
            // own so a single bad afternoon cannot silently kill the crop.
            if (!s.GrowLightsOn && s.GrowLightsResumeAtMinute >= 0 &&
                clock.TotalMinutes >= s.GrowLightsResumeAtMinute && s.BatteryPercent > 0.35)
            {
                s.GrowLightsOn = true;
                s.GrowLightsResumeAtMinute = -1;
            }
        }

        /// <summary>Realistic array output for the current sun and dust, for the UI.</summary>
        public static double PredictedKw(ColonyState s, GameClock clock)
            => s.ArrayPeakKw * clock.SolarIrradianceFraction * s.ArrayHealth * (1.0 - Math.Min(0.85, s.ArrayDust * 0.9));

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
