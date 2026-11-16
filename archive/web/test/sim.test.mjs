/**
 * Headless checks for the web build's rules engine. These are the browser twin of
 * shared/LunaQuest.Tests: same content, same numbers, same save contract.
 *
 *   npm test        (node --test)
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { ContentDatabase } from '../src/content.js';
import { GameClock, MINUTES_PER_SECOND } from '../src/sim/clock.js';
import { createState, read } from '../src/sim/state.js';
import { Power, Air, Water, Food, Crop, Dust } from '../src/sim/models.js';
import { Simulation, PlayerAction } from '../src/sim/simulation.js';
import { LunarTerrain } from '../src/world/terrain.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

async function loadContent() {
  const names = ['buildings', 'npcs', 'incidents', 'crops', 'learning', 'research', 'voices', 'dialogue'];
  const loaded = {};
  for (const name of names) {
    const json = JSON.parse(await readFile(join(root, 'content', `${name}.json`), 'utf8'));
    loaded[name] = json;
  }
  return new ContentDatabase(loaded);
}

const content = await loadContent();

test('content:', async (t) => {
  await t.test('sixteen buildings, seven crew, fifteen incident rules', () => {
    assert.equal(content.buildings.length, 16);
    assert.equal(content.npcs.length, 7);
    assert.equal(content.incidents.length, 15);
    assert.equal(content.learning.length, 11);
  });

  await t.test('exactly six humans plus NEXO, with the approved names', () => {
    const humans = content.npcs.filter((n) => !n.isRobot);
    assert.equal(humans.length, 6);
    assert.deepEqual(
      humans.map((n) => n.nameEn),
      ['Dr. Safaa', 'Noor', 'Dr. Alaa', 'Abdulaziz', 'Ahmed', 'Mostafa'],
    );
    assert.equal(content.npcs.find((n) => n.isRobot).nameEn, 'NEXO');
  });

  await t.test('every functional building has rooms and interactions that resolve', () => {
    for (const b of content.buildings) {
      if (b.category !== 'functional') continue;
      assert.ok(b.rooms.length > 0, `${b.id} has no rooms`);
      assert.ok(b.interactions.length > 0, `${b.id} has no interactions`);
      for (const i of b.interactions) {
        assert.ok(i.roomId, `${b.id}/${i.id} has no room`);
        assert.ok(b.rooms.some((r) => r.id === i.roomId), `${b.id}/${i.id} points at a missing room`);
      }
    }
  });

  await t.test('every interaction learning id points at a real objective', () => {
    for (const i of content.interactions) {
      if (!i.learning) continue;
      assert.ok(content.objective(i.learning), `${i.buildingId}/${i.id} -> ${i.learning} missing`);
    }
  });
});

test('clock:', async (t) => {
  await t.test('twelve simulated minutes per real second', () => {
    const clock = new GameClock(0);
    clock.step(1);
    assert.equal(clock.totalMinutes, MINUTES_PER_SECOND);
  });

  await t.test('a thirty-day mission is sixty real minutes', () => {
    const clock = new GameClock(0);
    assert.equal(clock.day, 1);
    assert.equal((30 * 1440) / MINUTES_PER_SECOND, 3600);
    clock.setTotalMinutes(30 * 1440 + 1);
    assert.equal(clock.day, 31);
    assert.ok(clock.isMissionComplete);
  });

  await t.test('the polar Sun stays low and the array never sees full power', () => {
    for (let m = 0; m < 40 * 1440; m += 37) {
      const clock = new GameClock(m);
      // A near-polar site: the Sun circles the horizon between about 8.6° and 13.4°, never
      // climbing anywhere near the zenith and never dipping far below the horizon.
      assert.ok(clock.sunElevationDeg > 6 && clock.sunElevationDeg < 18, `elevation ${clock.sunElevationDeg}`);
      assert.ok(clock.solarIrradianceFraction >= 0 && clock.solarIrradianceFraction <= 1);
      assert.ok(clock.solarIrradianceFraction < 1, 'full irradiance should never happen at the pole');
    }
  });
});

test('resources:', async (t) => {
  await t.test('array output peaks just under the 30 kW rating', () => {
    const s = createState();
    let best = 0;
    for (let m = 0; m < 40 * 1440; m += 30) {
      best = Math.max(best, Power.predictedKw(s, new GameClock(m)));
    }
    assert.ok(best > 25 && best < 30, `peak ${best.toFixed(1)} kW`);
  });

  await t.test('stored food is about 24.6 days for seven people', () => {
    const s = createState();
    assert.equal(s.crewCount, 7);
    const days = Food.daysRemaining(s);
    assert.ok(days > 23 && days < 26, `days ${days}`);
  });

  await t.test('water recycling loses only a few litres per day', () => {
    const s = createState();
    const loss = Water.netLossLitresPerDay(s);
    assert.ok(loss > 0 && loss < 12, `loss ${loss} L/day`);
  });

  await t.test('a healthy scrubber keeps CO2 near the baseline for one day', () => {
    const s = createState();
    const clock = new GameClock(6 * 60);
    for (let i = 0; i < 120; i += 1) {
      Air.step(s, clock, 1);
      clock.step(1);
    }
    assert.ok(read.co2Ppm(s) < 2500, `CO2 climbed to ${read.co2Ppm(s)} ppm`);
    assert.ok(s.o2Kg > 60, `O2 fell to ${s.o2Kg} kg`);
  });

  await t.test('crops need a full cycle and then feed the colony', () => {
    const s = createState();
    const clock = new GameClock(6 * 60);
    let days = 0;
    while (!Crop.readyToHarvest(s) && days < 40) {
      for (let i = 0; i < 120; i += 1) {
        Crop.step(s, clock, 1);
        clock.step(1);
      }
      days += 1;
    }
    assert.ok(days > 18 && days < 32, `crop matured in ${days} days`);
    const before = s.foodKcal;
    const kcal = Crop.harvest(s);
    assert.ok(kcal > 8000 && kcal < 12000, `harvest ${kcal} kcal`);
    assert.equal(s.foodKcal, Math.min(s.foodCapacityKcal, before + kcal));
    assert.equal(Math.round(Crop.harvest(s)), 0, 'a harvested pod is not immediately harvestable');
  });

  await t.test('dust accumulates and cleaning restores the array', () => {
    const s = createState();
    const clock = new GameClock(0);
    const before = Power.predictedKw(s, clock);
    for (let i = 0; i < 300; i += 1) Dust.step(s, clock, 1);
    assert.ok(s.arrayDust > 0.04);
    const dirty = Power.predictedKw(s, clock);
    assert.ok(dirty < before, 'dust must cost output');
    Dust.cleanArrays(s);
    assert.ok(Power.predictedKw(s, clock) > dirty);
  });
});

test('simulation:', async (t) => {
  const sim = () => new Simulation(content);

  await t.test('a fresh mission starts on day one at 06:00', () => {
    const s = sim();
    s.Clock.setTotalMinutes(6 * 60);
    assert.equal(s.Clock.day, 1);
    assert.equal(Math.round(s.Clock.hourOfDay), 6);
  });

  await t.test('cleaning the arrays resolves the dust incident and fills the journal', () => {
    const s = sim();
    s.Clock.setTotalMinutes(3 * 1440 + 11 * 60);
    s.State.arrayDust = 0.6;
    const raised = s.Incidents.evaluate(s.State, s.Clock.totalMinutes);
    assert.ok(raised.some((d) => d.id === 'inc_array_dust'), 'dust rule should fire');
    const result = s.interact('power_farm', 'clean_arrays');
    assert.ok(result.ok);
    assert.ok(s.Learning.hasEntry('obj_solar_dust'));
    assert.ok(!s.Incidents.active.some((a) => a.definitionId === 'inc_array_dust' && !a.resolved));
  });

  await t.test('at most three incidents are active at once', () => {
    const s = sim();
    s.State.arrayDust = 1;
    s.State.sensorDust = 1;
    s.State.airlockDust = 1;
    s.State.waterL = 10;
    s.State.batteryKwh = 1;
    s.Incidents.evaluate(s.State, 10000);
    assert.ok(s.Incidents.active.length <= 3);
  });

  await t.test('observation stations unlock their own objective', () => {
    const s = sim();
    const result = s.interact('mission_control', 'solar_weather');
    assert.ok(result.ok, 'solar weather station should answer');
    const point = content.building('mission_control').interactions.find((i) => i.id === 'solar_weather');
    assert.ok(s.Learning.hasEntry(point.learning));
  });

  await t.test('save round-trips with the shared schema-2 keys', () => {
    const s = sim();
    s.Clock.setTotalMinutes(4210.5);
    s.State.waterL = 712.25;
    s.State.arrayDust = 0.31;
    s.interact('power_farm', 'clean_arrays');
    const save = s.captureSave({ playerPosition: [1, 2, 3] });
    assert.equal(save.schemaVersion, 2);
    assert.equal(typeof save.colony.batteryKwh, 'number');
    assert.ok(Array.isArray(save.journal));

    const other = sim();
    other.applySave(JSON.parse(JSON.stringify(save)));
    assert.equal(other.Clock.totalMinutes, 4210.5);
    assert.equal(other.State.waterL, 712.25);
    assert.ok(other.Learning.hasEntry('obj_solar_dust'));
    assert.deepEqual(other.extras.playerPosition, [1, 2, 3]);
  });

  await t.test('a future schema is rejected, not silently loaded', () => {
    const s = sim();
    assert.throws(() => s.applySave({ schemaVersion: 99 }), /Unsupported save schema/);
  });

  await t.test('a full thirty-day mission stays finite and bounded', () => {
    const s = sim();
    s.Clock.setTotalMinutes(6 * 60);
    const actions = [
      () => s.interact('power_farm', 'clean_arrays'),
      () => s.interact('power_farm', 'clean_arrays'),
      () => s.interact('life_support', 'clean_filter'),
      () => s.interact('greenhouse', 'harvest'),
      () => s.interact('comms_tower', 'relay'),
    ];
    let tick = 0;
    for (let second = 0; second < 3600; second += 1) {
      s.step(1);
      if (second % 90 === 0) actions[tick++ % actions.length]();
    }
    const st = s.State;
    for (const [key, value] of Object.entries(st)) {
      if (typeof value === 'number') assert.ok(Number.isFinite(value), `${key} is ${value}`);
    }
    assert.ok(st.batteryKwh >= 0 && st.batteryKwh <= st.batteryCapacityKwh);
    assert.ok(st.waterL >= 0 && st.waterL <= st.waterCapacityL);
    assert.ok(st.o2Kg >= 0 && st.o2Kg <= st.o2CapacityKg);
    assert.ok(st.foodKcal >= 0 && st.foodKcal <= st.foodCapacityKcal);
    assert.ok(st.cabinTempC >= 3 && st.cabinTempC <= 35);
    assert.ok(st.arrayDust >= 0 && st.arrayDust <= 1);
    assert.equal(st.gameOver, false, 'the mission has no permanent failure state');
    assert.ok(s.Learning.journal.length >= 3, `journal only reached ${s.Learning.journal.length}`);
  });
});

test('terrain:', async (t) => {
  await t.test('is deterministic and flat under the colony', () => {
    const a = new LunarTerrain(7);
    const b = new LunarTerrain(7);
    for (const [x, z] of [[0, 0], [12, -40], [-120, 88], [500, 500]]) {
      assert.equal(a.height(x, z), b.height(x, z));
    }
    for (let r = 0; r < 60; r += 6) {
      assert.equal(a.height(Math.cos(r) * 40, Math.sin(r) * 40), 0, 'the colony pad stays flat');
    }
  });

  await t.test('has relief and craters outside the pad', () => {
    const terrain = new LunarTerrain(7);
    let min = Infinity;
    let max = -Infinity;
    for (let x = -600; x <= 600; x += 23) {
      for (let z = -600; z <= 600; z += 23) {
        const h = terrain.height(x, z);
        min = Math.min(min, h);
        max = Math.max(max, h);
      }
    }
    assert.ok(max - min > 4, `relief only ${max - min} m`);
    assert.ok(terrain.slopeDegrees(300, 300) >= 0);
  });
});
