/**
 * All mutable colony state — a direct port of shared/LunaQuest.Core/Sim/ColonyState.cs.
 * Units are declared next to every field, and the HUD never guesses.
 */
export function createState(overrides = {}) {
  return {
    // ---- power (kW, kWh) ---------------------------------------------------
    batteryKwh: 96, // 80% of a 120 kWh pack
    batteryCapacityKwh: 120,
    arrayPeakKw: 30, // ~100 m² at 22% efficiency
    arrayHealth: 1, // 0..1, dust + damage
    baseLoadKw: 12.4, // habitat, lights, pumps, comms
    shedLoadKw: 4.2, // lab + greenhouse, shed first
    labAndGreenhousePowered: true,
    solarKw: 0,

    // ---- water (litres) ----------------------------------------------------
    waterL: 900,
    waterCapacityL: 1200,
    recyclerEfficiency: 0.93, // ISS-class recovery
    filterHealth: 1,
    waterQuality: 0.98,
    recyclerRunning: true,

    // ---- air (kg, kPa, ppm) ------------------------------------------------
    o2Kg: 83.7, // 300 m³ at 21% O2
    o2CapacityKg: 110,
    co2Kg: 0.35, // ~400 ppm baseline
    cabinPressureKpa: 101.3,
    scrubberHealth: 1,
    o2GeneratorKw: 2.4,
    o2GeneratorRunning: true,

    // ---- thermal (°C) ------------------------------------------------------
    cabinTempC: 21,
    externalTempC: -55,
    heaterKw: 1.8,
    insulation: 0.86,

    // ---- dust (0..1 coverage) ---------------------------------------------
    arrayDust: 0.04,
    sensorDust: 0.03,
    airlockDust: 0.05,

    // ---- food (kcal) -------------------------------------------------------
    foodKcal: 482000, // ~24.6 days for seven people
    foodCapacityKcal: 607000,
    foodShortageDays: 0,
    foodShortage: false,
    cropWaterStress: 0,
    cropLightHours: 0,
    cropBiomassKg: 0,
    cropHealth: 1,
    growLightsOn: true,
    growLightsResumeAtMinute: -1,

    // ---- radiation (µGy/h, mSv cumulative) --------------------------------
    doseRateMicroGyH: 13.2, // Chang'e-4 LND surface median
    cumulativeDoseMsv: 0,
    solarParticleEvent: false,
    speIntensity: 0,
    crewSheltered: false,

    // ---- communications (0..1) --------------------------------------------
    signalQuality: 1,
    earthLinkUp: true,
    relayHealth: 1,

    // ---- crew --------------------------------------------------------------
    crewCount: 7, // commander + six crew; NEXO does not breathe
    averageFatigue: 0,
    averageMorale: 0.8,
    suitEnergy: 1,

    // ---- mission -----------------------------------------------------------
    samplesCollected: 0,
    dataPoints: 0,
    gameOver: false, // never true in normal play: no permanent failure by design

    ...overrides,
  };
}

const CABIN_M3 = 300;
const CO2_DENSITY_KG_M3 = 1.98; // at 101 kPa, 20 °C

export const read = {
  batteryPercent: (s) => (s.batteryCapacityKwh <= 0 ? 0 : s.batteryKwh / s.batteryCapacityKwh),
  o2Percent: (s) => (s.o2CapacityKg <= 0 ? 0 : s.o2Kg / s.o2CapacityKg),
  waterPercent: (s) => (s.waterCapacityL <= 0 ? 0 : s.waterL / s.waterCapacityL),
  foodPercent: (s) => (s.foodCapacityKcal <= 0 ? 0 : s.foodKcal / s.foodCapacityKcal),
  co2Ppm: (s) => Math.max(400, (s.co2Kg / CO2_DENSITY_KG_M3 / CABIN_M3) * 1e6),
  pressurePercent: (s) => s.cabinPressureKpa / 101.3,
};

export const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
