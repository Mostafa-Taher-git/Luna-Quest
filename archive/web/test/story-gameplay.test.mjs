/**
 * Story and gameplay checks: the commander is the person named in the official roster, and the
 * shift board is a real checklist of what the player can do rather than a few milestones.
 *
 * The board tests call Hud.currentShift()/Hud.taskList() through the prototype, so they exercise
 * the actual shipping logic without a GPU, a browser or a full DOM. The official name lives in
 * docs/design/Luna-Quest_Player_NPC_Roster_AR_v2.2.json and the v2.2 changelog.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { Hud } from '../src/ui/hud.js';
import { L } from '../src/i18n.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const file = (path) => readFile(join(root, path), 'utf8');
const OFFICIAL_NAME = 'Amara Okafor';

// Sources that used to open with a placeholder name and now open with the roster commander.
const ALIGNED_SOURCES = [
  'web/index.html',
  'web/src/main.js',
  'web/src/sim/simulation.js',
  'engines/godot-gdscript/LunaQuest.GDScript/scripts/main.gd',
  'engines/godot-gdscript/LunaQuest.GDScript/scripts/hud.gd',
  'engines/godot-gdscript/LunaQuest.GDScript/scripts/sim.gd',
  'engines/godot-gdscript/LunaQuest.GDScript/scripts/selftest.gd',
];

// The .NET and Unity builds already used the roster name; they are the cross-engine reference.
const REFERENCE_ENGINE_SOURCES = [
  'engines/godot/LunaQuest.Godot/scripts/Main.cs',
  'engines/godot/LunaQuest.Godot/scripts/Hud.cs',
  'engines/unity/LunaQuest.Unity/Assets/Scripts/ColonyHud.cs',
  'engines/unity/LunaQuest.Unity/Assets/Scripts/Core/ColonySimulation.cs',
  'engines/unity/LunaQuest.Unity/Assets/Scripts/Core/SaveData.cs',
  'engines/unity/LunaQuest.Unity/Assets/Scripts/LunaQuestGame.cs',
];

const ALL_RUNTIME_SOURCES = [...ALIGNED_SOURCES, ...REFERENCE_ENGINE_SOURCES];

test('every runtime opens with the commander named in the official roster', async () => {
  const roster = JSON.parse(await file('docs/design/Luna-Quest_Player_NPC_Roster_AR_v2.2.json'));
  assert.ok(
    JSON.stringify(roster).includes(OFFICIAL_NAME),
    'the roster no longer names the default commander',
  );
  for (const path of ALL_RUNTIME_SOURCES) {
    const text = await file(path);
    assert.ok(text.includes(OFFICIAL_NAME), `${path} does not use the roster commander`);
  }
});

test('the placeholder default is gone from every runtime source', async () => {
  for (const path of ALL_RUNTIME_SOURCES) {
    const text = await file(path);
    assert.ok(
      !/Mostafa/.test(text),
      `${path} still opens with the old placeholder name`,
    );
  }
});

test('the NPC whose id carries that name is left intact', async () => {
  const npcs = JSON.parse(await file('content/npcs.json')).npcs;
  const ids = JSON.stringify(npcs).match(/npc_mostafa/g) ?? [];
  assert.equal(ids.length, 3, 'the existing NPC id/person must not be renamed with the player');
  const npc = npcs.find((entry) => entry.id === 'npc_mostafa');
  assert.ok(npc, 'the NPC roster entry disappeared');
  assert.equal(npc.nameStatus, 'approved', 'an approved crew name was edited');
  assert.equal(npc.roleEn, 'Construction Engineer — Soil Printer', 'the NPC role changed');
});

function boardAt(hour) {
  const hud = Object.create(Hud.prototype);
  hud.sim = {
    Clock: { hourOfDay: hour, day: 4, missionProgress: 0.12 },
    Learning: { hasEntry: () => false },
    State: {},
  };
  return hud;
}

test('the shift board covers all four shifts and their boundaries', () => {
  const cases = [
    [0, 'night'], [5.99, 'night'], [6, 'morning'], [11.99, 'morning'],
    [12, 'afternoon'], [17.99, 'afternoon'], [18, 'evening'], [23.99, 'evening'],
  ];
  for (const [hour, id] of cases) {
    assert.equal(boardAt(hour).currentShift().id, id, `hour ${hour} should be ${id}`);
  }
  for (const [hour] of cases) {
    const shift = boardAt(hour).currentShift();
    assert.ok(shift.en.length > 10 && shift.ar.length > 10, 'each shift needs both languages');
  }
});

test('the board lists every learning objective plus mission progress', async () => {
  const learning = JSON.parse(await file('content/learning.json'));
  const objectiveIds = learning.learning.map((entry) => entry.id);
  assert.equal(objectiveIds.length, 11, 'the learning content changed; update the board');

  const tasks = boardAt(9).taskList();
  const ids = tasks.map((task) => task.id);
  // The board carries the eleven learning objectives, the 30-day mission line, and two
  // rover field tasks (take the LTV, drive it to the ice site). The rover tasks are movement
  // milestones, not learning objectives, so learning.json stays at eleven.
  assert.equal(tasks.length, 14, 'eleven objectives, one mission line and two rover field tasks');
  for (const id of objectiveIds) {
    assert.ok(ids.includes(id), `${id} has no station task on the board`);
  }
  assert.ok(ids.includes('mission'), 'the 30-day mission line is missing');
  assert.ok(ids.includes('obj_rover_take') && ids.includes('obj_rover_ice'), 'the rover field tasks are missing');
  assert.equal(new Set(ids).size, ids.length, 'a task is listed twice');
});

test('each station task names a place to go and reads in both languages', () => {
  L.arabic = false;
  const tasks = boardAt(9).taskList();
  for (const task of tasks) {
    assert.ok(task.label.length > 8, `${task.id} has no readable label`);
    assert.equal(typeof task.done, 'boolean', `${task.id} has no completion state`);
    if (task.id === 'mission') {
      assert.match(task.hint, /^\d+%$/, 'the mission line should read as a progress meter');
    } else {
      assert.ok(task.hint.length > 5, `${task.id} does not say where to go`);
      assert.ok(!/^\d+%$/.test(task.hint), `${task.id} needs a station, not a percentage`);
    }
  }
  L.arabic = true;
  const arabic = boardAt(9).taskList();
  assert.ok(arabic.every((task) => /[\u0600-\u06FF]/.test(task.label)), 'Arabic labels are missing');
  L.arabic = false;
});

test('this shift leads the board, but no task ever becomes unreachable', async () => {
  const learning = JSON.parse(await file('content/learning.json'));
  const objectiveIds = new Set(learning.learning.map((entry) => entry.id));
  for (const [hour, shift] of [[8, 'morning'], [14, 'afternoon'], [20, 'evening'], [2, 'night']]) {
    const hud = boardAt(hour);
    assert.equal(hud.currentShift().id, shift);
    const tasks = hud.taskList();
    assert.equal(tasks.length, 14, `${shift} lost a task`);
    assert.equal(
      new Set(tasks.map((task) => task.id)).size, objectiveIds.size + 3,
      `${shift} dropped an objective`,
    );
    const leading = tasks.filter((task) => task.shift === shift);
    assert.ok(leading.length >= 2, `${shift} has no tasks of its own`);
    assert.ok(
      tasks.slice(0, leading.length).every((task) => task.shift === shift),
      `${shift} tasks do not lead the board`,
    );
  }
});

test('the journal still renders the same full checklist', async () => {
  const text = await file('web/src/ui/hud.js');
  const journal = text.slice(text.indexOf('journalPanel()'));
  assert.match(journal, /this\.taskList\(\)/, 'the journal and the board drifted apart');
  assert.match(text, /renderTasks\(\)[\s\S]*?currentShift\(\)/, 'the board has no shift header');
});

test('the shift header is a stylesheet class, not inline styling', async () => {
  const hud = await file('web/src/ui/hud.js');
  const css = await file('web/src/styles.css');
  assert.match(hud, /head\.className = 'shift-head'/);
  assert.ok(!/head\.style\.cssText/.test(hud), 'the header still uses inline styling');
  assert.match(css, /#task-list li\.shift-head\s*\{[\s\S]*?list-style:\s*none/, 'the class is missing');
  assert.match(css, /#task-list li\.shift-head\s*\{[\s\S]*?text-transform:\s*uppercase/);
});
