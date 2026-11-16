/**
 * Crew scheduling — port of NpcDirector.cs. Six humans and one robot follow the schedule in
 * content/npcs.json whether or not the player is watching, and fatigue moves per simulated
 * hour so a bad shift is something the player can actually fix.
 */
import { MINUTES_PER_SECOND } from './clock.js';
import { clamp } from './state.js';

export class NpcDirector {
  constructor(content) {
    this.content = content;
    this.states = new Map();
    for (const profile of content.npcs) {
      this.states.set(profile.id, {
        id: profile.id,
        locationId: profile.locationId,
        activityEn: 'on duty',
        activityAr: 'في المناوبة',
        trust: 0.5,
        fatigue: 0.2,
        memoryTags: [],
      });
    }
  }

  state(id) {
    let s = this.states.get(id);
    if (!s) {
      s = { id, locationId: '', activityEn: '', activityAr: '', trust: 0.5, fatigue: 0, memoryTags: [] };
      this.states.set(id, s);
    }
    return s;
  }

  /** Re-evaluates schedules; called from the fixed simulation step, not per frame. */
  tick(hourOfDay) {
    for (const profile of this.content.npcs) {
      const entry = currentEntry(profile, hourOfDay);
      if (!entry) continue;
      const st = this.state(profile.id);
      st.locationId = entry.location;
      st.activityEn = entry.activityEn;
      st.activityAr = entry.activityAr;

      const resting = /rest|sleep/i.test(entry.activityEn ?? '');
      const ratePerHour = resting ? -0.035 : 0.03;
      const dtHours = MINUTES_PER_SECOND / 60;
      st.fatigue = profile.isRobot ? 0 : clamp(st.fatigue + ratePerHour * dtHours, 0, 1);
    }
  }

  /** The line a crew member says when the commander walks up. Fixed, approved content. */
  line(npcId, arabic, incidentNearby = false) {
    const p = this.content.npc(npcId);
    if (!p) return '';
    const st = this.state(npcId);
    if (incidentNearby) {
      return arabic ? 'القراءة غير طبيعية. لا تصلحها قبل أن تعرف سببها.' : "That reading is off. Don't fix it before you know why.";
    }
    if (st.fatigue > 0.7 && !p.isRobot) {
      return arabic ? 'أنا متعب. هل يمكننا إعادة ترتيب المناوبة؟' : "I'm running tired. Can we rebalance the shift?";
    }
    if (!arabic && p.greetingEn) return p.greetingEn;
    if (arabic && p.greetingAr) return p.greetingAr;
    return arabic ? `${p.nameAr} — ${p.roleAr}` : `${p.nameEn} — ${p.roleEn}`;
  }
}

function currentEntry(profile, hourOfDay) {
  for (const e of profile.schedule ?? []) {
    const start = Number(e.start ?? 0);
    const end = Number(e.end ?? 24);
    const wraps = start > end;
    const inside = wraps ? hourOfDay >= start || hourOfDay < end : hourOfDay >= start && hourOfDay < end;
    if (inside) return e;
  }
  return (profile.schedule ?? [])[0] ?? null;
}
