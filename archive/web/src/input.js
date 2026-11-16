/**
 * Keyboard, mouse and pointer lock. Everything the player can do is listed in HELP and is
 * the same key set as the Godot build: WASD, Shift, Space, E, V, J, G, H, F5, F9, Esc, L.
 */
export class Input {
  constructor(target = window) {
    this.keys = new Set();
    this.pressed = new Set();
    this.mouseDX = 0;
    this.mouseDY = 0;
    this.wheel = 0;
    this.locked = false;
    this.enabled = true;
    this.handlers = { onPointerLock: [] };
    this.canvas = null;

    this._onKeyDown = (e) => {
      if (e.repeat) return;
      const code = e.code;
      if (['Tab', 'F5', 'F9'].includes(code)) e.preventDefault();
      this.keys.add(code);
      this.pressed.add(code);
    };
    this._onKeyUp = (e) => this.keys.delete(e.code);
    this._onMouseMove = (e) => {
      if (!this.locked) return;
      this.mouseDX += e.movementX ?? 0;
      this.mouseDY += e.movementY ?? 0;
    };
    this._onWheel = (e) => {
      this.wheel += Math.sign(e.deltaY);
    };
    this._onLockChange = () => {
      this.locked = document.pointerLockElement === this.canvas;
      for (const fn of this.handlers.onPointerLock) fn(this.locked);
    };
    this._onBlur = () => this.keys.clear();

    target.addEventListener('keydown', this._onKeyDown);
    target.addEventListener('keyup', this._onKeyUp);
    target.addEventListener('mousemove', this._onMouseMove);
    target.addEventListener('wheel', this._onWheel, { passive: true });
    target.addEventListener('blur', this._onBlur);
    document.addEventListener('pointerlockchange', this._onLockChange);
  }

  attach(canvas) {
    this.canvas = canvas;
  }

  requestLock() {
    if (!this.canvas || this.locked) return;
    const promise = this.canvas.requestPointerLock?.({ unadjustedMovement: true });
    if (promise?.catch) promise.catch(() => this.canvas.requestPointerLock?.());
  }

  releaseLock() {
    if (document.pointerLockElement) document.exitPointerLock();
  }

  down(...codes) {
    return codes.some((c) => this.keys.has(c));
  }

  consume(code) {
    if (!this.pressed.has(code)) return false;
    this.pressed.delete(code);
    return true;
  }

  /** Mouse motion since the last call, in radians-friendly pixels. */
  takeMouse() {
    const dx = this.mouseDX;
    const dy = this.mouseDY;
    this.mouseDX = 0;
    this.mouseDY = 0;
    return { dx, dy };
  }

  takeWheel() {
    const w = this.wheel;
    this.wheel = 0;
    return w;
  }

  endFrame() {
    this.pressed.clear();
  }
}

export const KEYS = {
  forward: ['KeyW', 'ArrowUp'],
  back: ['KeyS', 'ArrowDown'],
  left: ['KeyA', 'ArrowLeft'],
  right: ['KeyD', 'ArrowRight'],
  run: ['ShiftLeft', 'ShiftRight'],
  jump: ['Space'],
  use: ['KeyE'],
  view: ['KeyV'],
  journal: ['KeyJ'],
  math: ['KeyG'],
  help: ['KeyH'],
  save: ['F5'],
  load: ['F9'],
  pause: ['Escape'],
  language: ['KeyL'],
  mute: ['KeyM'],
};
