using System;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// All mutable colony state. Pure data: no engine types, no scene references.
    /// Units are declared next to every field so content and UI never guess.
    /// </summary>
    public sealed class ColonyState
    {
        // ---- Power (kW, kWh) -------------------------------------------------
        public double BatteryKwh = 96.0;          // 80% of a 120 kWh pack
        public double BatteryCapacityKwh = 120.0;
        public double ArrayPeakKw = 30.0;         // ~100 m² at 22% efficiency
        public double ArrayHealth = 1.0;          // 0..1, dust + damage
        public double BaseLoadKw = 12.4;          // habitat, lights, pumps, comms
        public double ShedLoadKw = 4.2;           // lab + greenhouse, shed first
        public bool LabAndGreenhousePowered = true;
        public double SolarKw;

        // ---- Water (litres) --------------------------------------------------
        public double WaterL = 900.0;
        public double WaterCapacityL = 1200.0;
        public double RecyclerEfficiency = 0.93;  // ISS-class recovery
        public double FilterHealth = 1.0;         // 0..1
        public double WaterQuality = 0.98;        // 0..1
        public bool RecyclerRunning = true;

        // ---- Air (kg, kPa, ppm) ---------------------------------------------
        public double O2Kg = 83.7;                // 300 m³ at 21% O2
        public double O2CapacityKg = 110.0;
        public double CO2Kg = 0.35;               // ~400 ppm baseline
        public double CabinPressureKpa = 101.3;
        public double ScrubberHealth = 1.0;       // 0..1
        public double O2GeneratorKw = 2.4;        // electrolysis load when running
        public bool O2GeneratorRunning = true;

        // ---- Thermal (°C) ----------------------------------------------------
        public double CabinTempC = 21.0;
        public double ExternalTempC = -55.0;
        public double HeaterKw = 1.8;
        public double Insulation = 0.86;          // 0..1 quality

        // ---- Dust (0..1 coverage) -------------------------------------------
        public double ArrayDust = 0.04;
        public double SensorDust = 0.03;
        public double AirlockDust = 0.05;

        // ---- Food (kcal, kg) -------------------------------------------------
        public double FoodKcal = 620000.0;        // ~30 crew-days of stored food (9 crew)
        public double FoodCapacityKcal = 780000.0;
        public double FoodShortageDays;
        public bool FoodShortage;
        public double CropWaterStress;
        public double CropLightHours;
        public double CropBiomassKg;
        public double CropHealth = 1.0;
        public bool GrowLightsOn = true;
        /// <summary>When the grow lights may auto-resume after a load shed; -1 means no timer.</summary>
        public double GrowLightsResumeAtMinute = -1;

        // ---- Radiation (µGy/h, mSv cumulative) -------------------------------
        public double DoseRateMicroGyH = 13.2;    // Chang'e-4 LND surface median
        public double CumulativeDoseMsv;
        public bool SolarParticleEvent;
        public double SpeIntensity;               // 0..1
        public bool CrewSheltered;

        // ---- Communications (0..1) ------------------------------------------
        public double SignalQuality = 1.0;
        public bool EarthLinkUp = true;
        public double RelayHealth = 1.0;

        // ---- Crew ------------------------------------------------------------
        public int CrewCount = 9;                 // player + 8 NPCs
        public double AverageFatigue;             // 0..1
        public double AverageMorale = 0.8;        // 0..1
        public double SuitEnergy = 1.0;           // player suit, 0..1

        // ---- Mission ---------------------------------------------------------
        public int SamplesCollected;
        public double DataPoints;
        public bool GameOver;                     // never true in normal play (GDD: no permanent failure)

        public double BatteryPercent => BatteryCapacityKwh <= 0 ? 0 : BatteryKwh / BatteryCapacityKwh;
        public double O2Percent => O2CapacityKg <= 0 ? 0 : O2Kg / O2CapacityKg;
        public double WaterPercent => WaterCapacityL <= 0 ? 0 : WaterL / WaterCapacityL;
        public double FoodPercent => FoodCapacityKcal <= 0 ? 0 : FoodKcal / FoodCapacityKcal;

        /// <summary>CO₂ concentration in ppm, derived from mass and cabin volume (300 m³).</summary>
        public double Co2Ppm
        {
            get
            {
                const double cabinM3 = 300.0;
                const double co2DensityKgM3 = 1.98; // at 101 kPa, 20 °C
                double volume = CO2Kg / co2DensityKgM3;
                return Math.Max(400.0, volume / cabinM3 * 1_000_000.0);
            }
        }

        public double PressurePercent => CabinPressureKpa / 101.3;
    }
}
