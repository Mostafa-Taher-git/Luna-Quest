/**
 * Evidence-based learning journal — port of LearningDirector.cs.
 * Learning is proof of work done, never a quiz. Each completed activity records one concept
 * and one skill, and the objective text is content, not generated prose.
 */
export class LearningDirector {
  constructor(content) {
    this.content = content;
    this.journal = [];
    this.skillEvidence = new Map();
  }

  hasEntry(objectiveId) {
    return this.journal.some((j) => j.objectiveId === objectiveId);
  }

  /** Returns the new journal entry, or null if it was already recorded / unknown. */
  unlock(objectiveId, minute) {
    if (!objectiveId || this.hasEntry(objectiveId)) return null;
    const obj = this.content.objective(objectiveId);
    if (!obj) return null;

    const entry = {
      objectiveId: obj.id,
      titleEn: obj.titleEn,
      titleAr: obj.titleAr,
      bodyEn: obj.bodyEn,
      bodyAr: obj.bodyAr,
      skill: obj.skill ?? '',
      evidenceClass: obj.evidenceClass ?? '',
      source: obj.source ?? '',
      unlockedAtMinute: minute,
    };
    this.journal.push(entry);
    if (entry.skill) this.skillEvidence.set(entry.skill, (this.skillEvidence.get(entry.skill) ?? 0) + 1);
    return entry;
  }

  evidenceFor(skill) {
    return this.skillEvidence.get(skill) ?? 0;
  }
}
