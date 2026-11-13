using System;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Cabin thermal balance. Polar surface swings from about -180 °C in permanent shadow to
    /// roughly -50 °C in grazing sunlight. The habitat must hold ~21 °C.
    /// </summary>
    public static class ThermalModel
    {
        public const double TargetC = 21.0;

        public static void Step(ColonyState s, Time.GameClock clock, double realSeconds)
        {
            double illum = clock.SolarIrradianceFraction;
            s.ExternalTempC = -180.0 + 130.0 * illum;

            // Heat loss scales with the inside/outside difference and insulation quality.
            double deltaT = TargetC - s.ExternalTempC;
            double lossKw = 0.03 * deltaT * (1.0 - s.Insulation * 0.75);

            double heatInKw = 0.0;
            if (s.BatteryKwh > 1.0) heatInKw = Math.Min(s.HeaterKw, lossKw);
            // Safe haven: residual thermal mass and a small emergency heater hold the cabin
            // above freezing with no main power. Service is reduced; the crew is not killed.
            else heatInKw = lossKw * 0.92;

            double netKw = heatInKw - lossKw;
            double dtHours = realSeconds * Time.GameClock.MinutesPerSecond / 60.0;

            // Crude lumped capacitance: the habitat shell + air responds slowly.
            double thermalMassKwhPerK = 6.0;
            s.CabinTempC += (netKw * dtHours) / thermalMassKwhPerK;
            s.CabinTempC = Clamp(s.CabinTempC, 3.0, 35.0);

            if (s.CabinTempC < 16.0) s.AverageFatigue = Math.Min(1.0, s.AverageFatigue + 0.0015);
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
