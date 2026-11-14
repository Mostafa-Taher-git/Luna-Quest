/**
 * The physical models, ported one-for-one from shared/LunaQuest.Core/Sim/*.cs so the browser
 * build obeys exactly the same arithmetic as the Godot and Unity builds.
 *
 * Every number is tied to a real anchor and every anchor is named in a comment: the player
 * is meant to be able to argue with the model, not just watch a bar move.
 */
import { MINUTES_PER_SECOND, MINUTES_PER_DAY } from './clock.js';
import { read, clamp } from './state.js';

const gameDays = (realSeconds) => (realSeconds * MINUTES_PER_SECOND) / MINUTES_PER_DAY;
const dtHours = (realSeconds) => (realSeconds * MINUTES_PER_SECOND) / 60;

/** Solar array, battery pack and load priority. */
export const Power = {
  step(s, clock, realSeconds) {
    const dtH = dtHours(realSeconds);
    const irradiance = clock.solarIrradianceFraction;
    const dustFactor = 1 - Math.min(0.85, s.arrayDust * 0.9);
    s.solarKw = s.arrayPeakKw * irradiance * s.arrayHealth * dustFactor;

    let load = s.baseLoadKw;
    if (s.o2GeneratorRunning) load += s.o2GeneratorKw;
    if (s.growLightsOn) load += 2.0;
    load += s.heaterKw; // the thermostat holds the cabin; the heater is a real load

    let net = s.solarKw - load;
    s.labAndGreenhousePowered = true;
    if (s.batteryKwh + net * dtH < s.batteryCapacityKwh * 0.2 && s.growLightsOn) {
      const shedNet = s.solarKw - (load - 2.0);
      if (shedNet > net) {
        net = shedNet;
        load -= 2.0;
        s.labAndGreenhousePowered = false;
      }
    }

    s.batteryKwh = clamp(s.batteryKwh + net * dtH, 0, s.batteryCapacityKwh);

    // Never a hard failure: at an empty pack the colony runs on emergency reserves with
    // reduced service, and the HUD explains it.
    if (s.batteryKwh <= 0.01) {
      s.o2GeneratorRunning = false;
      s.growLightsOn = false;
      s.labAndGreenhousePowered = false;
      if (s.growLightsResumeAtMinute < 0) s.growLightsResumeAtMinute = clock.totalMinutes + 360;
    }

    // A shed is temporary; when the pack recovers, the grow lights return on their own.
    if (
      !s.growLightsOn &&
      s.growLightsResumeAtMinute >= 0 &&
      clock.totalMinutes >= s.growLightsResumeAtMinute &&
      read.batteryPercent(s) > 0.35
    ) {
      s.growLightsOn = true;
      s.growLightsResumeAtMinute = -1;
    }
  },

  predictedKw(s, clock) {
    return (
      s.arrayPeakKw *
      clock.solarIrradianceFraction *
      s.arrayHealth *
      (1 - Math.min(0.85, s.arrayDust * 0.9))
    );
  },
};

/** Cabin atmosphere: consumption, electrolysis, scrubber, leakage. */
export const Air = {
  O2_KG_PER_PERSON_DAY: 0.84,
  CO2_KG_PER_PERSON_DAY: 1.0,
  CABIN_VOLUME_M3: 300,

  step(s, clock, realSeconds) {
    const days = gameDays(realSeconds);
    const dtH = dtHours(realSeconds);

    const o2Use = this.O2_KG_PER_PERSON_DAY * s.crewCount * days;
    const co2Made = this.CO2_KG_PER_PERSON_DAY * s.crewCount * days;

    s.o2Kg = Math.max(0, s.o2Kg - o2Use);
    s.co2Kg += co2Made;

    // Electrolysis: 1 kg of water yields ~0.889 kg O2. The stack is sized to 90% of crew
    // demand, so the reserve declines slowly and the greenhouse's oxygen is a real help.
    if (s.o2GeneratorRunning && s.waterL > 20) {
      const o2Made = o2Use * 0.9;
      const waterUsed = o2Made / 0.889;
      s.waterL = Math.max(0, s.waterL - waterUsed);
      s.o2Kg = Math.min(s.o2CapacityKg, s.o2Kg + o2Made);
    } else if (!s.o2GeneratorRunning) {
      s.o2Kg = Math.max(0, s.o2Kg - o2Use * 0.05); // suit reserves / slow bleed
    }

    // A healthy scrubber holds the cabin near the 400 ppm baseline; a worn unit lets the
    // excess grow, which is exactly what the gauge shows.
    const baselineKg = 0.24;
    const excess = Math.max(0, s.co2Kg - baselineKg);
    const scrubRatePerHour = 0.55 * Math.max(0, s.scrubberHealth);
    const removed = excess * (1 - Math.exp(-scrubRatePerHour * dtH));
    s.co2Kg = Math.max(0, s.co2Kg - removed);
    s.co2Kg = Math.max(0, s.co2Kg - 0.0002 * realSeconds); // slow cabin leakage

    s.cabinPressureKpa = clamp(s.cabinPressureKpa - 0.00002 * realSeconds, 80, 101.3);
    if (s.o2GeneratorRunning && s.labAndGreenhousePowered) {
      s.cabinPressureKpa = Math.min(101.3, s.cabinPressureKpa + 0.00003 * realSeconds);
    }

    // About 3% of scrubber life per mission day: one replacement is forced, never a failure.
    s.scrubberHealth = clamp(s.scrubberHealth - 0.00025 * realSeconds, 0, 1);
  },

  co2Warning: (s) => read.co2Ppm(s) > 5000,
  co2Danger: (s) => read.co2Ppm(s) > 10000,
  o2Low: (s) => read.o2Percent(s) < 0.3,
};

/** Water storage, crew use and closed-loop recycling (ISS-class ~93%). */
export const Water = {
  LITRES_PER_PERSON_DAY: 11,

  step(s, clock, realSeconds) {
    const days = gameDays(realSeconds);
    const demand = Math.min(this.LITRES_PER_PERSON_DAY * s.crewCount * days, s.waterL);

    let recovered = 0;
    if (s.recyclerRunning && s.filterHealth > 0.05) {
      recovered = demand * s.recyclerEfficiency * s.filterHealth * s.waterQuality;
    }
    s.waterL = clamp(s.waterL - demand + recovered, 0, s.waterCapacityL);

    // Filter clog: roughly 4% of capacity per mission day at full crew.
    s.filterHealth = clamp(s.filterHealth - demand * 0.0004, 0, 1);

    const qualityDelta = (s.filterHealth - 0.5) * 0.00002;
    s.waterQuality = clamp(s.waterQuality + qualityDelta, 0.4, 1);

    if (s.waterL < 40) s.cropWaterStress = Math.min(1, s.cropWaterStress + 0.01);
    else s.cropWaterStress = Math.max(0, s.cropWaterStress - 0.004);
  },

  netLossLitresPerDay(s) {
    return this.LITRES_PER_PERSON_DAY * s.crewCount * (1 - s.recyclerEfficiency * s.filterHealth * s.waterQuality);
  },
};

/** Food is a consumable, not a score. Seven people at 2,800 kcal each. */
export const Food = {
  KCAL_PER_PERSON_DAY: 2800,

  step(s, clock, realSeconds) {
    const days = gameDays(realSeconds);
    const need = this.KCAL_PER_PERSON_DAY * s.crewCount * days;

    if (s.foodKcal >= need) {
      s.foodKcal -= need;
      s.foodShortage = false;
      s.foodShortageDays = Math.max(0, s.foodShortageDays - days);
    } else {
      const shortfall = need - s.foodKcal;
      s.foodKcal = 0;
      s.foodShortage = true;
      s.foodShortageDays += days;
      // A shortfall is not a death screen: the crew slows down and morale falls.
      s.averageFatigue = Math.min(1, s.averageFatigue + days * 0.1);
      if (shortfall > 0 && s.averageMorale > 0.2) s.averageMorale = Math.max(0, s.averageMorale - days * 0.05);
    }
  },

  daysRemaining(s) {
    return s.foodKcal / (this.KCAL_PER_PERSON_DAY * Math.max(1, s.crewCount));
  },
};

/** Sealed grow pod: light, water, CO₂ in; biomass and oxygen out. */
export const Crop = {
  POD_AREA_M2: 24,
  WATER_L_PER_M2_DAY: 3,
  CO2_KG_PER_M2_DAY: 0.03,
  LIGHT_KW_PER_M2: 0.05,
  HARVEST_KCAL_PER_M2: 450,
  CYCLE_DAYS: 24,

  step(s, clock, realSeconds) {
    const days = gameDays(realSeconds);
    const dt = days / this.CYCLE_DAYS;

    const lightOk = s.growLightsOn && s.labAndGreenhousePowered && s.batteryKwh > 0.5;
    const waterOk = s.waterL > 30 && s.cropWaterStress < 0.5;
    const airOk = read.co2Ppm(s) > 350;

    if (lightOk && waterOk && airOk) {
      s.cropBiomassKg = Math.min(1, s.cropBiomassKg + dt * (0.55 + 0.45 * s.cropHealth));
    }

    if (lightOk) {
      // Irrigation is 90% recovered by the condensate loop; only the net loss leaves the tank.
      s.waterL = Math.max(0, s.waterL - this.POD_AREA_M2 * this.WATER_L_PER_M2_DAY * days * 0.1);
      const co2Used = Math.min(s.co2Kg, this.POD_AREA_M2 * this.CO2_KG_PER_M2_DAY * days);
      s.co2Kg = Math.max(0, s.co2Kg - co2Used);
      // Photosynthesis returns O2 at the stoichiometric 32/44 mass ratio.
      s.o2Kg = Math.min(s.o2CapacityKg, s.o2Kg + co2Used * (32 / 44));
      s.cropLightHours += (realSeconds * MINUTES_PER_SECOND) / 60;
    }

    if (!lightOk || !waterOk || !airOk) {
      s.cropHealth = Math.max(0, s.cropHealth - 0.0006 * realSeconds);
      s.cropBiomassKg = Math.max(0, s.cropBiomassKg - 0.00002 * realSeconds);
    } else {
      s.cropHealth = Math.min(1, s.cropHealth + 0.00004 * realSeconds);
    }
  },

  readyToHarvest(s) {
    return s.cropBiomassKg >= 0.98;
  },

  harvest(s) {
    if (!this.readyToHarvest(s)) return 0;
    const kcal = this.HARVEST_KCAL_PER_M2 * this.POD_AREA_M2 * Math.max(0.4, s.cropHealth);
    s.foodKcal = Math.min(s.foodCapacityKcal, s.foodKcal + kcal);
    s.cropBiomassKg = 0;
    s.cropLightHours = 0;
    s.cropHealth = Math.min(1, s.cropHealth + 0.05);
    return kcal;
  },

  stage(s) {
    if (s.cropBiomassKg < 0.15) return 'seedling';
    if (s.cropBiomassKg < 0.55) return 'vegetative';
    if (s.cropBiomassKg < 0.95) return 'maturing';
    return 'ready';
  },
};

/** Regolith dust: the most visible maintenance loop in the game. */
export const Dust = {
  step(s, clock, realSeconds) {
    s.arrayDust = clamp(s.arrayDust + 0.0003 * realSeconds, 0, 1);
    s.sensorDust = clamp(s.sensorDust + 0.00017 * realSeconds, 0, 1);
    s.airlockDust = clamp(s.airlockDust + 0.00025 * realSeconds, 0, 1);
    s.arrayHealth = clamp(s.arrayHealth - s.arrayDust * 0.0004 * realSeconds, 0.2, 1);
  },
  cleanArrays: (s, amount = 0.6) => (s.arrayDust = clamp(s.arrayDust - amount, 0, 1)),
  cleanSensors: (s, amount = 0.8) => (s.sensorDust = clamp(s.sensorDust - amount, 0, 1)),
  cleanAirlock: (s, amount = 0.9) => (s.airlockDust = clamp(s.airlockDust - amount, 0, 1)),
  onAirlockUse: (s) => (s.airlockDust = clamp(s.airlockDust + 0.03, 0, 1)),
};

/** Radiation: Chang'e-4 LND measured 13.2 ± 1 µGy/h absorbed dose in silicon. */
export const Radiation = {
  SURFACE_MICRO_GY_H: 13.2,

  step(s, clock, realSeconds) {
    const hours = (realSeconds * MINUTES_PER_SECOND) / 60;
    let rate = this.SURFACE_MICRO_GY_H;
    if (s.solarParticleEvent) rate *= 1 + 120 * s.speIntensity;
    if (s.crewSheltered) rate *= 0.25; // regolith berms and the shielded section
    s.doseRateMicroGyH = rate;
    s.cumulativeDoseMsv += (rate * hours) / 1000;
  },

  dailyDoseMsv(s) {
    return (s.doseRateMicroGyH * 24) / 1000;
  },
};

/** Cabin thermal balance: -180 °C in shadow to -50 °C in grazing sunlight outside. */
export const Thermal = {
  TARGET_C: 21,

  step(s, clock, realSeconds) {
    const illum = clock.solarIrradianceFraction;
    s.externalTempC = -180 + 130 * illum;

    const deltaT = this.TARGET_C - s.externalTempC;
    const lossKw = 0.03 * deltaT * (1 - s.insulation * 0.75);

    let heatInKw;
    if (s.batteryKwh > 1) heatInKw = Math.min(s.heaterKw, lossKw);
    else heatInKw = lossKw * 0.92; // residual thermal mass and the emergency heater

    const netKw = heatInKw - lossKw;
    const dtH = dtHours(realSeconds);
    const thermalMassKwhPerK = 6;
    s.cabinTempC = clamp(s.cabinTempC + (netKw * dtH) / thermalMassKwhPerK, 3, 35);

    if (s.cabinTempC < 16) s.averageFatigue = Math.min(1, s.averageFatigue + 0.0015);
  },
};

/** Earth link and local network: the relay and antenna are the bottleneck, not light time. */
export const Comms = {
  step(s, clock, realSeconds) {
    const dustPenalty = 1 - Math.min(0.45, s.sensorDust * 0.5);
    const sunNoise = 1 - 0.06 * Math.max(0, Math.sin(clock.lunarPhase * 2 * Math.PI));
    s.signalQuality = clamp(s.relayHealth * dustPenalty * sunNoise, 0, 1);
    s.earthLinkUp = s.signalQuality > 0.25;
  },

  band(s) {
    return s.signalQuality > 0.75 ? 'nominal' : s.signalQuality > 0.45 ? 'degraded' : s.earthLinkUp ? 'weak' : 'lost';
  },
};
