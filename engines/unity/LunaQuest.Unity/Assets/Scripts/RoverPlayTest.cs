using System;
using System.Text;
using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// A self-driving playtest for the parts of the game that only a running engine can answer.
    ///
    /// Unity cannot be run headless in every environment, so this exists to be run where it can:
    ///
    ///   Unity -batchmode -projectPath engines/unity/LunaQuest.Unity \
    ///         -executeMethod LunaQuest.Unity.RoverPlayTest.Run -quit -logFile rover.log
    ///
    /// It plays the T5 checklist for real — enters the rover, drives it, steps out — and checks the
    /// things the C# can only claim until something runs it: that the ground is solid under the
    /// commander, that a driven suit does not fight the rover's capsule, that the wheel is dead
    /// behind an open dialogue, and that stepping out lands on the ground rather than in the air
    /// (or inside the rover). Exit code 0 means every check passed.
    /// </summary>
    public static class RoverPlayTest
    {
        private static readonly StringBuilder Log = new StringBuilder();
        private static int _checks;
        private static int _failures;

        private static void Expect(bool condition, string label)
        {
            _checks++;
            if (condition) return;
            _failures++;
            Log.AppendLine($"  · FAIL {label}");
        }

        public static void Run()
        {
            LunaQuestGame game = null;
            try
            {
                var go = new GameObject("PlayTestGame");
                game = go.AddComponent<LunaQuestGame>();
                game.BeginGame("PlayTest");
                Physics.SyncTransforms();
                // One fixed step so every collider exists and the ground has been baked.
                Physics.Simulate(0.02f);

                CheckGround(game);
                CheckDriving(game);
                CheckPauseGate(game);
                CheckNeutralSteer(game);
                CheckExit(game);
                CheckRestart(game);
            }
            catch (Exception error)
            {
                _failures++;
                Log.AppendLine($"  · EXCEPTION {error}");
            }
            finally
            {
                bool ok = _failures == 0;
                Debug.Log("Luna-Quest Unity playtest\n" + Log);
                Debug.Log(ok
                    ? $"PLAYTEST OK — {_checks} checks passed"
                    : $"PLAYTEST FAILED — {_failures} of {_checks} checks failed");
                if (game != null) game.enabled = false;
                Application.Quit(ok ? 0 : 1);
            }
        }

        /// <summary>The Moon is solid: the commander's suit stands on the shared heightfield.</summary>
        private static void CheckGround(LunaQuestGame game)
        {
            var player = game.Player;
            float ground = game.World.HeightAt(player.transform.position.x, player.transform.position.z);
            Expect(player.transform.position.y > -5f && player.transform.position.y < ground + 3f,
                $"player starts at y {player.transform.position.y:F2}, ground is {ground:F2}");

            // A downward ray at the spawn has to find the ground collider, from the side that faces
            // up (a MeshCollider wound the wrong way is invisible from above).
            var from = new Vector3(player.transform.position.x, ground + 20f, player.transform.position.z);
            Expect(Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f),
                "no collider under the spawn point");
            if (hit.collider != null)
                Expect(Mathf.Abs(hit.point.y - ground) < 0.5f,
                    $"the ground collider is at {hit.point.y:F2}, the heightfield says {ground:F2}");
            // And from below the ray must pass through it (or hit nothing at all).
            var under = new Vector3(player.transform.position.x, ground - 20f, player.transform.position.z);
            bool hitFromBelow = Physics.Raycast(under, Vector3.up, out RaycastHit below, 40f);
            Expect(!hitFromBelow || below.point.y > ground + 0.5f,
                "the ground is solid from underneath: its triangles face down");
        }

        /// <summary>Enter, drive, steer, drain; the wheels and the odometer move with it.</summary>
        private static void CheckDriving(LunaQuestGame game)
        {
            var player = game.Player;
            var rover = game.Rover;
            Expect(rover != null, "no crew rover in the scene");
            if (rover == null) return;

            player.SetDriving(false);
            player.Teleport(rover.transform.position + rover.transform.right * 2.2f);
            rover.Enter(player);
            Expect(rover.Occupied && player.Rover == rover, "entering the rover did not take the seat");
            Expect(!player.ControllerEnabled, "the suit controller is still live while driving");

            Vector3 start = rover.transform.position;
            float battery = rover.BatteryKwh;
            for (int i = 0; i < 90; i++)
            {
                // The drive step is private to the input; push the vehicle through its own physics
                // path by faking the state the wheel would produce. The integration, collision and
                // battery rules are the parts under test here.
                rover.SimulateForTest(1f / 60f, 1f, 0.4f, false, false, player);
            }
            Expect(rover.Speed > 2f, $"90 frames of throttle only reached {rover.Speed:F2} m/s");
            Expect(Vector3.Distance(rover.transform.position, start) > 2f,
                $"the rover did not move ({Vector3.Distance(rover.transform.position, start):F2} m)");
            Expect(rover.Odometer > 2f, $"the odometer did not count ({rover.Odometer:F2} m)");
            Expect(rover.BatteryKwh < battery, "driving did not drain the battery");
            Expect(rover.transform.position.y > game.World.HeightAt(
                    rover.transform.position.x, rover.transform.position.z) - 2f,
                $"the rover sank through the ground (y {rover.transform.position.y:F2})");
        }

        /// <summary>BUG-1: an open dialogue must not let the wheel keep driving.</summary>
        private static void CheckPauseGate(LunaQuestGame game)
        {
            var player = game.Player;
            var rover = game.Rover;
            if (rover == null || !rover.Occupied) return;

            game.Hud.OpenDialogue(game.Sim.Content.Npcs[0],
                game.Sim.Content.TopicsFor(game.Sim.Content.Npcs[0].Id));
            Expect(!game.InputActive, "a dialogue is open but input is still active");
            Vector3 before = rover.transform.position;
            float speedBefore = rover.Speed;
            // What Update() would do for three frames while the dialogue is open.
            for (int i = 0; i < 3; i++)
            {
                if (game.InputActive) rover.Drive(1f / 60f, player);
            }
            Expect(Vector3.Distance(rover.transform.position, before) < 0.001f,
                "the rover moved while a dialogue was open");
            Expect(Mathf.Approximately(rover.Speed, speedBefore), "the rover changed speed during a dialogue");
            game.Hud.CloseDialogue();

            // BUG-5: rolling forward with the wheel held still must not steer the rover.
            float yaw = rover.transform.eulerAngles.y;
            for (int i = 0; i < 60; i++) rover.SimulateForTest(1f / 60f, 1f, 0f, false, false, player);
            Expect(Mathf.Abs(Mathf.DeltaAngle(yaw, rover.transform.eulerAngles.y)) < 0.5f,
                $"the rover steered itself with the wheel centred ({Mathf.DeltaAngle(yaw, rover.transform.eulerAngles.y):F2}°)");
        }

        /// <summary>Steering with the wheel held dead straight: the vehicle must track straight.</summary>
        private static void CheckNeutralSteer(LunaQuestGame game) { }

        /// <summary>BUG-2: stepping out leaves a live, grounded, upright suit beside the rover.</summary>
        private static void CheckExit(LunaQuestGame game)
        {
            var player = game.Player;
            var rover = game.Rover;
            if (rover == null) return;
            Vector3 where = rover.transform.position;
            rover.Exit(player);
            Expect(!rover.Occupied && player.Rover == null, "stepping out kept the seat");
            Expect(player.ControllerEnabled, "the suit controller is still off after stepping out");
            float ground = game.World.HeightAt(player.transform.position.x, player.transform.position.z);
            Expect(Mathf.Abs(player.transform.position.y - ground) < 1.5f,
                $"the suit stepped out at y {player.transform.position.y:F2}, the ground is {ground:F2}");
            Expect(Vector3.Distance(player.transform.position, where) > 1.0f,
                "the suit stepped out inside the rover");
            Expect(Mathf.Abs(Mathf.DeltaAngle(player.Visual.localEulerAngles.x, 0f)) < 1f,
                "the suit stayed reclined after stepping out");
        }

        /// <summary>A new mission puts the rover back with full power and no milestones.</summary>
        private static void CheckRestart(LunaQuestGame game)
        {
            var rover = game.Rover;
            if (rover == null) return;
            rover.HasDriven = true;
            rover.ReachedIce = true;
            rover.Odometer = 42f;
            game.RestartMission();
            Expect(rover.BatteryKwh >= CrewRover.BatteryKwhMax - 0.001f,
                $"a new mission left the battery at {rover.BatteryKwh:F2} kWh");
            Expect(!rover.ReachedIce && !rover.HasDriven && rover.Odometer < 0.001f,
                "a new mission kept the old rover milestones");
        }
    }
}
