/**
 * Bilingual UI strings. The Godot build switches language on L; the web build uses the same
 * key set so the two HUDs read the same in English and Arabic. Arabic is a translation of the
 * interface, exactly like the subtitle track in the engine builds.
 */
/** Tiny language store: one flag, shared by the HUD, consoles and voice subtitles. */
export const L = {
  arabic: false,
  get code() {
    return this.arabic ? 'ar' : 'en';
  },
};

export const STRINGS = {
  colonyStatus: ['Colony status', 'حالة المستعمرة'],
  colonyLog: ['Colony log', 'سجل المستعمرة'],
  shiftBoard: ['Shift board', 'لوحة المناوبة'],
  resourceMath: ['G  resource math', 'G  حساب الموارد'],
  journal: ['J  journal', 'J  المفكرة'],
  help: ['H  help', 'H  المساعدة'],
  pause: ['Pause', 'إيقاف مؤقت'],
  resume: ['Resume', 'متابعة'],
  save: ['Save', 'حفظ'],
  load: ['Load', 'تحميل'],
  skipIntro: ['Skip intro', 'تخطي المقدمة'],
  beginShift: ['Begin shift', 'ابدأ المناوبة'],
  commanderName: ['Commander name', 'اسم القائد'],
  useConsole: ['Read the console', 'اقرأ اللوحة'],
  talkTo: ['Talk', 'تحدّث'],
  empty: ['Nothing logged yet.', 'لا يوجد سجل بعد.'],
  power: ['PWR', 'طاقة'],
  water: ['WTR', 'ماء'],
  oxygen: ['O2', 'أكسجين'],
  co2: ['CO2', 'CO2'],
  temp: ['TEMP', 'حرارة'],
  food: ['FOOD', 'غذاء'],
  dose: ['DOSE', 'جرعة'],
  link: ['LINK', 'رابط'],
  fatigue: ['CREW', 'الطاقم'],
  crop: ['CROP', 'محصول'],
  cycle: ['CYCLE', 'الدورة'],
  console: ['Console', 'لوحة'],
  observation: ['Observation station', 'محطة مراقبة'],
  close: ['Close', 'إغلاق'],
  take: ['Do it', 'نفّذ'],
  noChange: ['Nothing changed.', 'لم يتغير شيء.'],
  saved: ['Colony saved to this browser.', 'تم حفظ المستعمرة في هذا المتصفح.'],
  loaded: ['Colony restored.', 'تمت استعادة المستعمرة.'],
  noSave: ['No save found in this browser.', 'لا يوجد حفظ في هذا المتصفح.'],
  journalEmpty: ['Walk the colony and read a console. The journal fills with what you actually did.', 'امشِ في المستعمرة واقرأ لوحة. ستُسجل المفكرة ما فعلته فعلًا.'],
  paused: ['Colony paused — simulation clock stopped.', 'المستعمرة متوقفة — ساعة المحاكاة متوقفة.'],
};

/** Returns the string for the current language. */
export function t(key) {
  const pair = STRINGS[key];
  if (!pair) return key;
  return L.arabic ? pair[1] : pair[0];
}

/** Picks the right half of any {en, ar} record. */
export function pick(record, arabic = L.arabic) {
  if (!record) return '';
  if (typeof record === 'string') return record;
  const en = record.en ?? record.nameEn ?? record.titleEn ?? record.bodyEn ?? record.text ?? '';
  const ar = record.ar ?? record.nameAr ?? record.titleAr ?? record.bodyAr ?? record.textAr ?? en;
  return arabic ? ar || en : en;
}

export function setLanguage(arabic, doc = document) {
  L.arabic = Boolean(arabic);
  doc.documentElement.lang = arabic ? 'ar' : 'en';
  doc.documentElement.dir = arabic ? 'rtl' : 'ltr';
}

/** Formatting helpers shared by the HUD and the consoles. */
export const fmt = {
  pct: (v, digits = 0) => `${(v * 100).toFixed(digits)}%`,
  num: (v, digits = 0) => Number(v).toLocaleString('en-US', { minimumFractionDigits: digits, maximumFractionDigits: digits }),
  kw: (v) => `${v.toFixed(1)} kW`,
  kwh: (v) => `${Math.round(v)} kWh`,
  l: (v) => `${Math.round(v)} L`,
  kg: (v, digits = 1) => `${v.toFixed(digits)} kg`,
  kcal: (v) => `${Math.round(v).toLocaleString('en-US')} kcal`,
  ppm: (v) => `${Math.round(v).toLocaleString('en-US')} ppm`,
  c: (v) => `${v.toFixed(1)} °C`,
  dose: (v) => `${v.toFixed(1)} µGy/h`,
  msv: (v) => `${v.toFixed(2)} mSv`,
  day: (d) => `Day ${d}`,
  time: (h) => {
    const hh = Math.floor(h);
    const mm = Math.floor((h - hh) * 60);
    return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`;
  },
};
