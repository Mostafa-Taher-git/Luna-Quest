using System;
using System.Collections.Generic;
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
    /// It plays the T5 checklist for real — enters the rover, drives it, steps out, saves and
    /// restores it — and checks the things the C# can only claim until something runs it: that
    /// the ground is solid under the commander, that a driven suit does not fight the rover's
    /// capsule, that the wheel is dead behind an open dialogue, and that stepping out lands on
    /// the ground rather than in the air (or inside the rover). Exit code 0 means every check
    /// passed.
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
                CheckBoard(game);
                CheckDriving(game);
                CheckBoardAfterDrive(game);
                CheckPauseGate(game);
                CheckExit(game);
                CheckF0Controls(game);
                CheckSaveRestore(game);
                CheckBuildings(game);
                CheckSuitScale(game);
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

        /// <summary>
        /// The shift task board at mission start: fourteen rows mirroring learning.json, the
        /// header and the leading row matching the clock, the mission line last, the rover
        /// rows still pending, and a board that speaks both languages.
        /// </summary>
        private static void CheckBoard(LunaQuestGame game)
        {
            var hud = game.Hud;
            hud.UpdateBoard();

            Expect(ColonyHud.BoardRows.Length == 14,
                $"expected 14 row specs, found {ColonyHud.BoardRows.Length}");
            Expect(hud.BoardTexts.Length == ColonyHud.BoardRows.Length,
                $"{hud.BoardTexts.Length} texts for {ColonyHud.BoardRows.Length} specs");
            Expect(hud.BoardOrder.Length == ColonyHud.BoardRows.Length,
                "the draw order does not have one slot per row");

            // Kind counts: the journal rows mirror learning.json, two belong to the rover,
            // one is the mission line. learning.json itself stays at 11 objectives (H9).
            int journal = 0, rovers = 0, missions = 0;
            foreach (var row in ColonyHud.BoardRows)
            {
                if (row.Kind == "journal") journal++;
                else if (row.Kind == "mission") missions++;
                else rovers++;
            }
            Expect(journal == game.Sim.Content.Learning.Count,
                $"board: {journal} journal rows for {game.Sim.Content.Learning.Count} objectives");
            Expect(rovers == 2, $"board: expected 2 rover rows, found {rovers}");
            Expect(missions == 1, $"board: expected 1 mission row, found {missions}");
            foreach (var objective in game.Sim.Content.Learning)
                Expect(Array.Exists(ColonyHud.BoardRows, r => r.Id == objective.Id),
                    $"board: {objective.Id} has no row on the board");

            // No blank rows, and the mission line counts the days.
            for (int i = 0; i < hud.BoardTexts.Length; i++)
                Expect(!string.IsNullOrEmpty(hud.BoardTexts[i]), $"board: row {i} is blank");
            Expect(hud.BoardTexts[13].Contains("30"),
                $"board: the mission line reads '{hud.BoardTexts[13]}'");

            // Both sides of every shift boundary.
            Expect(ColonyHud.CurrentShift(0.0).Id == "night", "board: 00:00 is not the night shift");
            Expect(ColonyHud.CurrentShift(5.99).Id == "night", "board: 05:59 is not the night shift");
            Expect(ColonyHud.CurrentShift(6.0).Id == "morning", "board: 06:00 is not the morning shift");
            Expect(ColonyHud.CurrentShift(11.99).Id == "morning", "board: 11:59 is not the morning shift");
            Expect(ColonyHud.CurrentShift(12.0).Id == "afternoon", "board: 12:00 is not the afternoon shift");
            Expect(ColonyHud.CurrentShift(17.99).Id == "afternoon", "board: 17:59 is not the afternoon shift");
            Expect(ColonyHud.CurrentShift(18.0).Id == "evening", "board: 18:00 is not the evening shift");
            Expect(ColonyHud.CurrentShift(23.99).Id == "evening", "board: 23:59 is not the evening shift");

            // The header names the shift the clock says it is, and the row leading the board
            // belongs to that shift; the mission line always sits last.
            var shift = ColonyHud.CurrentShift(game.Sim.Clock.HourOfDay);
            Expect(hud.BoardHeader == L.T(shift.En, shift.Ar),
                $"board: the header reads '{hud.BoardHeader}', expected '{L.T(shift.En, shift.Ar)}'");
            int lead = hud.BoardOrder[0];
            Expect(ColonyHud.BoardRows[lead].Shift == shift.Id,
                $"board: the {ColonyHud.BoardRows[lead].Shift} row leads but the clock says {shift.Id}");
            Expect(hud.BoardOrder[13] == 13, "board: the mission line does not sit last");

            // The rover milestones are not finished at mission start.
            Expect(!hud.BoardDone[4] && !hud.BoardDone[5],
                "board: the rover rows are already marked at mission start");

            // Bilingual: the board swaps to Arabic and back.
            bool wasArabic = L.Arabic;
            L.Arabic = true;
            hud.UpdateBoard();
            Expect(hud.BoardTexts[0].Contains("لوحة"),
                $"board: the Arabic board reads '{hud.BoardTexts[0]}'");
            L.Arabic = wasArabic;
            hud.UpdateBoard();
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
            // BUG-5: the suit leans back against the seat. Euler(18, 180, 0) on a suit facing the
            // rover's forward tips its head backwards; a forward lean would mean the sign is wrong.
            float lean = player.Visual.localEulerAngles.x;
            if (lean > 180f) lean -= 360f;
            Expect(lean > 5f && lean < 40f,
                $"the driver is not leaning back against the seat (X {lean:F1}°, expected +18)");
            Expect(player.Visual.localPosition.y > rover.SeatGlobal.y - 0.6f,
                "the suit is not sitting at the hand controller");
        }

        /// <summary>
        /// The board follows the rover: taking the seat marks the garage row, and driving out
        /// to the ice site latches the second milestone and marks that row too — both drawn
        /// in the calm teal DONE colour (the colour is applied from BoardDone when drawing).
        /// </summary>
        private static void CheckBoardAfterDrive(LunaQuestGame game)
        {
            var hud = game.Hud;
            var rover = game.Rover;
            if (rover == null) return;

            hud.UpdateBoard();
            Expect(hud.BoardDone[4], "board: taking the rover did not mark the garage row");
            Expect(!hud.BoardDone[5], "board: the ice row is marked without reaching the ice");

            // Out to the ice site: teleport the way RestartMission does, take one step, and
            // the milestone latches from the rover's own distance check.
            Vector3 home = rover.transform.position;
            rover.transform.position = new Vector3(52f, home.y, -34f);
            rover.SimulateForTest(1f / 60f, 0f, 0f, false, false, game.Player);
            Expect(rover.ReachedIce, "the ice-site milestone did not latch");

            hud.UpdateBoard();
            Expect(hud.BoardDone[5], "board: reaching the ice did not mark the ice row");
            Expect(hud.BoardTexts[4].StartsWith("✓") && hud.BoardTexts[5].StartsWith("✓"),
                "board: the finished rover rows are not marked with the check");

            rover.transform.position = home;
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

            // With the wheel centred the rover must track straight.
            float yaw = rover.transform.eulerAngles.y;
            for (int i = 0; i < 60; i++) rover.SimulateForTest(1f / 60f, 1f, 0f, false, false, player);
            Expect(Mathf.Abs(Mathf.DeltaAngle(yaw, rover.transform.eulerAngles.y)) < 0.5f,
                $"the rover steered itself with the wheel centred ({Mathf.DeltaAngle(yaw, rover.transform.eulerAngles.y):F2}°)");

            // A steers LEFT in every engine. Unity's yaw is clockwise, so a left turn must make the
            // yaw angle decrease; a positive delta here means this edition is mirrored.
            yaw = rover.transform.eulerAngles.y;
            for (int i = 0; i < 90; i++) rover.SimulateForTest(1f / 60f, 1f, 1f, false, false, player);
            float turned = Mathf.DeltaAngle(yaw, rover.transform.eulerAngles.y);
            Expect(turned < -1f,
                $"A should steer left: the yaw moved {turned:F2}° (Godot turns the other way)");
        }

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

        /// <summary>
        /// The rover survives a save: its parked pose, battery, odometer, milestones and the seat
        /// all come back through the flat extras keys LunaQuestGame.SaveGame writes. An old save
        /// from before the rover existed (schema 2, no rover keys at all) must load without
        /// complaint and leave a full battery, clear milestones and an empty seat — the same
        /// contract as the Godot .NET edition's RoverAudit.
        /// </summary>
        private static void CheckSaveRestore(LunaQuestGame game)
        {
            var player = game.Player;
            var rover = game.Rover;
            if (rover == null) return;

            // Exactly the keys SaveGame writes for the rover, at a pose away from the apron so a
            // restored position cannot be mistaken for a default one.
            var extras = new Dictionary<string, object>
            {
                ["roverX"] = 3.5, ["roverY"] = 0.05, ["roverZ"] = -7.25, ["roverYaw"] = 1.1,
                ["roverBattery"] = 6.25, ["roverOdometer"] = 42.5,
                ["roverDriven"] = true, ["roverIce"] = true, ["roverOccupied"] = true
            };
            rover.Exit(player);
            rover.Restore(extras, player);

            Expect(rover.Occupied && player.Rover == rover, "restore() did not give the seat back");
            Expect(Mathf.Abs(rover.BatteryKwh - 6.25f) < 0.001f,
                $"restore() lost the battery ({rover.BatteryKwh:F2} kWh)");
            Expect(Mathf.Abs(rover.Odometer - 42.5f) < 0.001f,
                $"restore() lost the odometer ({rover.Odometer:F2} m)");
            Expect(rover.HasDriven && rover.ReachedIce, "restore() lost the milestones");
            Expect(Vector3.Distance(rover.transform.position, new Vector3(3.5f, 0.05f, -7.25f)) < 0.001f,
                $"restore() lost the parked position ({rover.transform.position})");

            // The old-save case: no rover keys at all. The numeric defaults still apply (a full
            // battery, zero odometer), the flags stay clear and nobody is seated. Like the .NET
            // edition, a missing pose key leaves the rover exactly where it already was.
            rover.Exit(player);
            rover.Restore(new Dictionary<string, object>(), player);
            Expect(Mathf.Abs(rover.BatteryKwh - CrewRover.BatteryKwhMax) < 0.001f,
                $"an old save did not leave a full battery on the rover ({rover.BatteryKwh:F2} kWh)");
            Expect(rover.Odometer < 0.001f, $"an old save kept the odometer ({rover.Odometer:F2} m)");
            Expect(!rover.HasDriven && !rover.ReachedIce, "an old save kept the rover milestones");
            Expect(!rover.Occupied && player.Rover == null, "an old save seated the commander in the rover");
            Expect(Vector3.Distance(rover.transform.position, new Vector3(3.5f, 0.05f, -7.25f)) < 0.001f,
                "an old save moved the rover although it carried no pose");
        }

        /// <summary>
        /// F0 — the universal control contract, proven by driving, not by reasoning. W drives
        /// toward the view, S reverses, A and D turn toward screen-left and screen-right (measured
        /// against the camera basis), and walking keeps the same meaning after any driving episode.
        /// </summary>
        private static void CheckF0Controls(LunaQuestGame game)
        {
            var player = game.Player;
            var rover = game.Rover;
            if (rover == null || player == null) return;

            // Deterministic pose: the apron, the usual yaw, full charge.
            rover.transform.position = new Vector3(-11f, 0.05f, -28f);
            rover.transform.rotation = Quaternion.Euler(0f, 0.4f * Mathf.Rad2Deg, 0f);
            rover.Speed = 0f;
            rover.Steer = 0f;
            rover.BatteryKwh = CrewRover.BatteryKwhMax;
            rover.Settle();

            player.SetDriving(false);
            player.Teleport(rover.transform.position + rover.transform.right * 2.2f);
            rover.Enter(player);
            player.SyncCameraToRover();

            var vehFwd = rover.transform.forward; vehFwd.y = 0f; vehFwd.Normalize();
            var camFwd = player.Camera.transform.forward; camFwd.y = 0f; camFwd.Normalize();
            Expect(Vector3.Dot(vehFwd, camFwd) > 0.9f,
                $"F0: the rover's forward is not aligned with the camera ({Vector3.Dot(vehFwd, camFwd):F2})");

            // W under throttle: displacement goes toward the view, not away from it.
            var before = rover.transform.position;
            for (int i = 0; i < 120; i++) rover.SimulateForTest(1f / 60f, 1f, 0f, false, false, player);
            var moved = rover.transform.position - before;
            Expect(Vector3.Dot(moved, vehFwd) > 0.5f,
                $"F0: W does not drive toward the view ({Vector3.Dot(moved, vehFwd):F2} m)");

            // S from rest: displacement away from the view.
            rover.Speed = 0f;
            before = rover.transform.position;
            for (int i = 0; i < 120; i++) rover.SimulateForTest(1f / 60f, -1f, 0f, false, false, player);
            moved = rover.transform.position - before;
            Expect(Vector3.Dot(moved, vehFwd) < 0f,
                $"F0: S does not reverse ({Vector3.Dot(moved, vehFwd):F2} m)");

            // Get rolling, then A turns toward screen-left, D toward screen-right.
            rover.Speed = 0f;
            for (int i = 0; i < 90; i++) rover.SimulateForTest(1f / 60f, 1f, 0f, false, false, player);
            var camRight = player.Camera.transform.right; camRight.y = 0f; camRight.Normalize();
            var left = -camRight;
            float yaw0 = rover.transform.eulerAngles.y;
            for (int i = 0; i < 60; i++)
            {
                rover.SimulateForTest(1f / 60f, 1f, 1f, false, false, player);
                player.SyncCameraToRover();
            }
            var h0 = Quaternion.Euler(0f, yaw0, 0f) * Vector3.forward;
            var h1 = Quaternion.Euler(0f, rover.transform.eulerAngles.y, 0f) * Vector3.forward;
            Expect(Vector3.Dot(h1, left) > Vector3.Dot(h0, left),
                "F0: A did not move the heading toward screen-left");

            rover.Speed = 0f;
            yaw0 = rover.transform.eulerAngles.y;
            for (int i = 0; i < 60; i++)
            {
                rover.SimulateForTest(1f / 60f, 1f, -1f, false, false, player);
                player.SyncCameraToRover();
            }
            h0 = Quaternion.Euler(0f, yaw0, 0f) * Vector3.forward;
            h1 = Quaternion.Euler(0f, rover.transform.eulerAngles.y, 0f) * Vector3.forward;
            Expect(Vector3.Dot(h1, camRight) > Vector3.Dot(h0, camRight),
                "F0: D did not move the heading toward screen-right");

            // Exiting cannot drift walking: W still leads to the camera, A/D still map to
            // screen-left/right.
            rover.Exit(player);
            Expect(!rover.Occupied && player.Rover == null, "F0: exit() kept the seat");
            Expect(ColonyPlayer.CameraRelativeDirection(new Vector2(0, 1), 0f).z > 0.9f,
                "F0: W no longer maps to the camera forward after exiting the rover");
            Expect(ColonyPlayer.CameraRelativeDirection(new Vector2(-1, 0), 0f).x < -0.9f,
                "F0: A no longer maps to screen-left after exiting the rover");
            Expect(ColonyPlayer.CameraRelativeDirection(new Vector2(1, 0), 0f).x > 0.9f,
                "F0: D no longer maps to screen-right after exiting the rover");
            Expect(ColonyPlayer.CameraRelativeDirection(new Vector2(0, -1), 0f).z < -0.9f,
                "F0: S no longer maps to reverse after exiting the rover");
            Expect(ColonyPlayer.CameraRelativeDirection(new Vector2(0, 1), 90f).x > 0.9f,
                "F0: walking at yaw 90 degrees is not camera-relative");

            // Back to the apron for anything that runs after this check.
            rover.transform.position = new Vector3(-11f, 0.05f, -28f);
            rover.transform.rotation = Quaternion.Euler(0f, 0.4f * Mathf.Rad2Deg, 0f);
            rover.Settle();
        }

        /// <summary>
        /// F2 — every functional building wears a primitive shell with MLI panels and a ribs-and-core
        /// cylinder silhouette, and an airlock-style porch at its door. landing_pad is decorative.
        /// </summary>
        private static void CheckBuildings(LunaQuestGame game)
        {
            var world = game.World;
            if (world == null) return;
            Expect(world.ShellCount >= 15,
                $"F2: only {world.ShellCount} building shells (expected at least 15)");
            Expect(world.PorchCount >= 15,
                $"F2: only {world.PorchCount} porches (expected at least 15)");
            var hidden = 0;
            foreach (var t in world.GetComponentsInChildren<Transform>())
            {
                if (t.gameObject.name is "FrontL" or "FrontR" or "Lintel" or "Back" or "BackL"
                    or "BackR" or "BackLintel" or "SideL" or "SideR")
                {
                    var r = t.GetComponent<Renderer>();
                    if (r != null && !r.enabled) hidden++;
                }
            }
            Expect(hidden >= 9,
                $"F2: {hidden} plain wall meshes were still enabled (expected at least 9 hidden)");
        }

        /// <summary>
        /// F3 — every suit is built from the shared 2.311 m visor-to-sole pose height, which
        /// puts every human at exactly 1.75 m in the baked pose. The Unity bake carries no clip,
        /// so the procedural 1.6 Hz hover/step offsets are the locomotion cue by design.
        /// </summary>
        private static void CheckSuitScale(LunaQuestGame game)
        {
            const float expectedScale = 1.75f / 2.311f;
            Expect(game.Player != null && game.Player.Visual != null, "F3: the player suit is missing");
            if (game.Player?.Visual != null)
                Expect(Mathf.Abs(game.Player.Visual.localScale.x - expectedScale) < 0.01f,
                    $"F3: the player suit's scale is {game.Player.Visual.localScale.x:F3}, expected {expectedScale:F3} (1.75 m)");

            int humans = 0;
            foreach (var agent in game.World.Crew)
            {
                if (agent.transform.childCount == 0) { Expect(false, $"F3: {agent.NpcId} has no visual"); continue; }
                var suit = agent.transform.GetChild(0);
                if (suit.name == "NEXO") continue;   // NEXO is the one robot — not human, not 1.75 m
                humans++;
                Expect(suit.childCount > 0, $"F3: {agent.NpcId}'s suit is empty");
                Expect(Mathf.Abs(suit.localScale.x - expectedScale) < 0.01f,
                    $"F3: {agent.NpcId}'s suit scale is {suit.localScale.x:F3}, expected {expectedScale:F3} (1.75 m)");
            }
            Expect(humans == 6, $"F3: expected 6 human crew visuals, found {humans}");
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
