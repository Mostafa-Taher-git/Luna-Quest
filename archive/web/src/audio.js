/**
 * Score and crew voices.
 *
 * The voice pack is the same 94 recorded lines the engine builds use, generated offline with
 * Piper. Nothing calls a cloud service: the browser fetches the MP3 that matches the key, and
 * every line carries its English and Arabic subtitle.
 */
export class AudioManager {
  constructor({ baseUrl = './', onSubtitle = () => {} } = {}) {
    this.baseUrl = baseUrl;
    this.onSubtitle = onSubtitle;
    this.pack = {};
    this.music = null;
    this.voice = null;
    this.muted = false;
    this.musicVolume = 0.32;
    this.voiceVolume = 0.9;
    this.lastBarkAt = 0;
    this.greeted = new Set();
    this.ready = false;
  }

  async load() {
    try {
      const res = await fetch(new URL(`${this.baseUrl}audio/voice/pack.json`, location.href));
      if (res.ok) this.pack = await res.json();
    } catch {
      this.pack = {};
    }

    this.music = new Audio(new URL(`${this.baseUrl}audio/lunaquest-score.mp3`, location.href).href);
    this.music.loop = true;
    this.music.volume = this.musicVolume;
    this.music.preload = 'auto';

    this.voice = new Audio();
    this.voice.preload = 'auto';
    this.voice.volume = this.voiceVolume;
    this.ready = true;
    return this;
  }

  /** Browsers need a gesture before audio plays; call this from the first click. */
  async startMusic() {
    if (!this.music) return;
    try {
      await this.music.play();
    } catch {
      /* autoplay blocked — the next gesture will retry */
    }
  }

  toggleMute() {
    this.muted = !this.muted;
    if (this.music) this.music.volume = this.muted ? 0 : this.musicVolume;
    if (this.voice) this.voice.volume = this.muted ? 0 : this.voiceVolume;
    return this.muted;
  }

  entry(key) {
    return this.pack?.[key] ?? null;
  }

  _url(file) {
    return new URL(`${this.baseUrl}audio/voice/${file}`, location.href).href;
  }

  _play(file, { text, textAr } = {}) {
    if (!this.voice || this.muted) {
      if (text) this.onSubtitle(text, textAr);
      return;
    }
    try {
      this.voice.src = this._url(file);
      this.voice.play().catch(() => {});
      if (text) this.onSubtitle(text, textAr);
    } catch {
      if (text) this.onSubtitle(text, textAr);
    }
  }

  speakKey(key) {
    const entry = this.entry(key);
    if (!entry) return false;
    this._play(entry.file, { text: entry.text, textAr: entry.textAr });
    return true;
  }

  greeting(npcId) {
    const prefix = { npc_safaa: 'safaa', npc_nour: 'noor', npc_alaa: 'alaa', npc_abdelaziz: 'aziz', npc_ahmed: 'ahmed', npc_mostafa: 'mostafa', npc_nexo: 'nexo' }[npcId];
    if (!prefix) return false;
    return this.speakKey(`crew.${prefix}.greet`);
  }

  bark(npcId) {
    const prefix = { npc_safaa: 'safaa', npc_nour: 'noor', npc_alaa: 'alaa', npc_abdelaziz: 'aziz', npc_ahmed: 'ahmed', npc_mostafa: 'mostafa', npc_nexo: 'nexo' }[npcId];
    if (!prefix) return false;
    const now = performance.now();
    if (now - this.lastBarkAt < 9000) return false;
    this.lastBarkAt = now;
    const index = Math.floor(Math.random() * 4);
    return this.speakKey(`bark.${prefix}.${index}`);
  }

  incident(incidentId) {
    return this.speakKey(`inc.${incidentId}`);
  }

  topic(npcId, topicId) {
    const prefix = { npc_safaa: 'safaa', npc_nour: 'noor', npc_alaa: 'alaa', npc_abdelaziz: 'aziz', npc_ahmed: 'ahmed', npc_mostafa: 'mostafa', npc_nexo: 'nexo' }[npcId];
    if (!prefix) return false;
    return this.speakKey(`talk.${prefix}.${topicId}`);
  }

  /** Called when a panel wants the score quieter. */
  duck(on) {
    if (!this.music) return;
    this.music.volume = this.muted ? 0 : on ? this.musicVolume * 0.35 : this.musicVolume;
  }
}
