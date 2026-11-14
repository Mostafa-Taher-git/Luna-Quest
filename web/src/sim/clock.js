/**
 * Deterministic mission clock — a direct port of shared/LunaQuest.Core/Time/GameClock.cs.
 *
 * One real second is twelve simulated minutes, so a thirty-day mission takes one hour of
 * play. The colony sits on the Shackleton rim (89.9° S): the Sun crawls along the horizon
 * and never climbs higher than about two degrees, which is why the shadows are long and
 * the array is vertical.
 */

export const MINUTES_PER_SECOND = 12;
export const MINUTES_PER_DAY = 1440;
export const SYNODIC_DAYS = 29.53059;
export const MISSION_DAYS = 30;

export class GameClock {
  constructor(totalMinutes = 0) {
    this.totalMinutes = Math.max(0, totalMinutes);
  }

  get totalSeconds() {
    return this.totalMinutes * 60;
  }

  /** 1-based mission day (1..30). */
  get day() {
    return Math.floor(this.totalMinutes / MINUTES_PER_DAY) + 1;
  }

  get dayFraction() {
    return (this.totalMinutes % MINUTES_PER_DAY) / MINUTES_PER_DAY;
  }

  get hourOfDay() {
    return this.dayFraction * 24;
  }

  /** 0..1 through the synodic month; drives the Sun's slow circle of the sky. */
  get lunarPhase() {
    return ((this.totalMinutes / MINUTES_PER_DAY) % SYNODIC_DAYS) / SYNODIC_DAYS;
  }

  get missionProgress() {
    return Math.min(1, this.totalMinutes / (MISSION_DAYS * MINUTES_PER_DAY));
  }

  get isMissionComplete() {
    return this.day > MISSION_DAYS;
  }

  /**
   * A near-polar site, so the Sun never climbs high — but 11° rather than 1°, because a grazing
   * Sun leaves the regolith at 2% of full illumination and the colony reads as a black hole with
   * lit roofs. At 11° the ground is legible, the shadows are still two hundred metres long, and
   * the lit faces of the modules still get their hard lunar key.
   */
  get sunElevationDeg() {
    const tilt = 2.4 * Math.sin(2 * Math.PI * this.lunarPhase - Math.PI / 2);
    return 11 + tilt;
  }

  get sunAzimuthDeg() {
    return (this.lunarPhase * 360 + 180) % 360;
  }

  /** Fraction of full irradiance reaching the farm; full noon power never happens here. */
  get solarIrradianceFraction() {
    const seasonal = 0.8 + 0.16 * Math.sin(2 * Math.PI * (this.lunarPhase - 0.15));
    return Math.max(0, Math.min(1, seasonal));
  }

  get isDaylight() {
    return this.sunElevationDeg > 0;
  }

  stamp() {
    const h = Math.floor(this.hourOfDay);
    const m = Math.floor((this.hourOfDay - h) * 60);
    return `Day ${this.day} · ${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  }

  step(realSeconds) {
    if (realSeconds <= 0) return;
    this.totalMinutes += realSeconds * MINUTES_PER_SECOND;
  }

  setTotalMinutes(minutes) {
    this.totalMinutes = Math.max(0, minutes);
    return this;
  }
}
