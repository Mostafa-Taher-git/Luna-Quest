/**
 * Threshold-driven incidents — port of shared/LunaQuest.Core/Domain/IncidentDirector.cs.
 * Rules live in content/incidents.json; nothing here is random text. Ignoring an incident
 * records it, it never becomes a permanent failure.
 */
import { read } from './state.js';

export const MAX_ACTIVE = 3;

export class IncidentDirector {
  constructor(definitions) {
    this.definitions = definitions ?? [];
    this.active = [];
    this.lastRaised = new Map();
  }

  definition(id) {
    return this.definitions.find((d) => d.id === id) ?? null;
  }

  tryRaise(id, minute) {
    const def = this.definition(id);
    if (!def) return null;
    if (this.active.some((a) => a.definitionId === id && !a.resolved && !a.ignored)) return null;
    if (this.active.length >= MAX_ACTIVE) return null;
    const last = this.lastRaised.get(id);
    if (last !== undefined && minute - last < (def.cooldownMin ?? 720)) return null;

    this.active.push({ definitionId: id, raisedAtMinute: minute, resolved: false, ignored: false, hintLevel: 0 });
    this.lastRaised.set(id, minute);
    return def;
  }

  /** Evaluates every rule against live state and returns newly raised definitions. */
  evaluate(state, minute) {
    const raised = [];
    for (const def of this.definitions) {
      const value = readMetric(state, def.metric, minute);
      const hit = def.op === 'below' ? value < def.threshold : value > def.threshold;
      if (!hit) continue;
      const r = this.tryRaise(def.id, minute);
      if (r) raised.push(r);
    }
    return raised;
  }

  resolve(id, action) {
    const def = this.definition(id);
    const active = this.active.find((a) => a.definitionId === id && !a.resolved);
    if (!def || !active) return false;
    if (def.action && def.action !== action) return false;
    active.resolved = true;
    return true;
  }

  ignore(id) {
    const a = this.active.find((x) => x.definitionId === id && !x.resolved);
    if (a) a.ignored = true;
  }

  dismissResolved() {
    this.active = this.active.filter((a) => !a.resolved && !a.ignored);
  }

  markResolved(id, minute) {
    this.lastRaised.set(id, minute);
    this.active = this.active.filter((a) => a.definitionId !== id);
  }
}

export function readMetric(s, metric, minute = 0) {
  switch (metric) {
    case 'arrayDust': return s.arrayDust;
    case 'sensorDust': return s.sensorDust;
    case 'airlockDust': return s.airlockDust;
    case 'batteryPercent': return read.batteryPercent(s);
    case 'waterPercent': return read.waterPercent(s);
    case 'o2Percent': return read.o2Percent(s);
    case 'co2Ppm': return read.co2Ppm(s);
    case 'filterHealth': return s.filterHealth;
    case 'scrubberHealth': return s.scrubberHealth;
    case 'cropHealth': return s.cropHealth;
    case 'signalQuality': return s.signalQuality;
    case 'doseRate': return s.doseRateMicroGyH;
    case 'cabinTemp': return s.cabinTempC;
    case 'foodPercent': return read.foodPercent(s);
    case 'relayHealth': return s.relayHealth;
    case 'crewFatigue': return s.averageFatigue;
    case 'pressure': return s.cabinPressureKpa;
    case 'samples': return s.samplesCollected;
    case 'suitEnergy': return s.suitEnergy;
    case 'foodKcal': return s.foodKcal;
    case 'cropBiomass': return s.cropBiomassKg;
    case 'day': return minute / 1440 + 1;
    default: return 0;
  }
}
