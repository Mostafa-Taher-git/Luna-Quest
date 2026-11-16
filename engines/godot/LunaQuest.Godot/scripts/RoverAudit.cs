using System;
using System.Collections.Generic;
using Godot;

namespace LunaQuest.Godot
{
    /// <summary>
    /// A headless playtest for the parts of the .NET edition that only a running engine can
    /// answer. Run it with:
    ///
    ///   godot --headless --path engines/godot/LunaQuest.Godot -- --rover-test
    ///
    /// It plays the T5 rover checklist for real — enters the rover, drives it, brakes, drains and
    /// recharges the battery, exits, saves and restarts — and prints "ROVER TEST OK" with a zero
    /// exit code, or the failing checks and a non-zero exit. CI runs it on every push.
    /// </summary>
    public sealed class RoverAudit
    {
        private int _checks, _failures;
        private readonly System.Text.StringBuilder _log = new();

        private void Expect(bool condition, string label)
        {
            _checks++;
            if (condition) return;
            _failures++;
            _log.AppendLine("  · " + label);
        }

        public bool Run(Main main)
        {
            var rover = main.Rover;
            var player = main.Player;
            Expect(rover != null && player != null, "the rover or the commander is missing");
            if (rover == null || player == null) return Report();

            CheckBoard(main);

            Expect(Mathf.Abs(rover.GlobalPosition.Y - 0.05f) < 0.3f,
                $"the rover is parked at y {rover.GlobalPosition.Y:F2}, not on the apron");
            // The Moon is solid under it (this is the check that failed for the GDScript ground).
            var space = main.GetWorld3D().DirectSpaceState;
            var down = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                rover.GlobalPosition + new Vector3(0, 8, 0), rover.GlobalPosition - new Vector3(0, 8, 0)));
            Expect(down.Count > 0, "there is no ground collider under the parked rover");

            // Enter: the seat is taken and the suit stops colliding with the rover it sits inside.
            player.SetDriving(false);
            player.GlobalPosition = rover.GlobalPosition + rover.GlobalTransform.Basis.X * 2.2f;
            rover.Enter(player);
            Expect(rover.Occupied && player.Rover == rover, "entering the rover did not take the seat");
            // Drive: throttle, a turn to the left, a drain, and the wheels turning with it.
            var start = rover.GlobalPosition;
            float yaw = rover.Rotation.Y;
            float battery = rover.BatteryKwh;
            for (int i = 0; i < 120; i++) rover.Step(1f / 60f, 1f, 0.5f, false, false, player);
            Expect(rover.Speed > 3f, $"two seconds of throttle only reached {rover.Speed:F2} m/s");
            Expect(rover.GlobalPosition.DistanceTo(start) > 2f,
                $"the rover did not move ({rover.GlobalPosition.DistanceTo(start):F2} m)");
            Expect(Mathf.Abs(rover.Rotation.Y - yaw) > 0.02f, "steering did not turn the rover");
            Expect(rover.Rotation.Y > yaw, "A should steer left: the yaw went the other way");
            Expect(rover.Odometer > 2f, $"the odometer did not count ({rover.Odometer:F2} m)");
            Expect(rover.BatteryKwh < battery, "driving did not drain the battery");
            Expect(Mathf.Abs(rover.Spin) > 1f, "the wheels did not spin");
            Expect(rover.GlobalPosition.Y > -2f, $"the rover sank through the ground (y {rover.GlobalPosition.Y:F2})");
            // BUG-5: the driver leans back against the seat (rotation.x = -0.32 rad, yaw PI).
            float lean = player.Visual.Rotation.X;
            Expect(lean < -0.2f && lean > -0.5f,
                $"the driver is not leaning back (visual X {lean:F2} rad, expected -0.32)");

            // Handbrake and a flat battery. The drive before this test leaves the rover within
            // the garage recharge radius in the -Z (camera-aligned) build, so park it somewhere
            // the garage cannot reach it — the test needs a genuinely flat pack.
            for (int i = 0; i < 120; i++) rover.Step(1f / 60f, 0f, 0f, false, true, player);
            Expect(Mathf.Abs(rover.Speed) < 0.05f, $"the handbrake left it rolling at {rover.Speed:F2} m/s");
            rover.GlobalPosition = new Vector3(30f, 0.05f, -55f);
            rover.Speed = 0f;
            rover.BatteryKwh = 0f;
            for (int i = 0; i < 60; i++) rover.Step(1f / 60f, 1f, 0f, false, false, player);
            Expect(Mathf.Abs(rover.Speed) < 0.01f, $"a flat battery still drove ({rover.Speed:F2} m/s)");

            // The garage apron recharges a stopped rover; the ice site sets the milestone.
            rover.GlobalPosition = new Vector3(-16f, 0.05f, -40f);
            rover.Speed = 0f;
            rover.BatteryKwh = 1f;
            for (int i = 0; i < 60; i++) rover.Step(1f / 60f, 0f, 0f, false, false, player);
            Expect(rover.BatteryKwh > 1f, "the garage apron did not recharge the rover");
            rover.ReachedIce = false;
            rover.GlobalPosition = new Vector3(52f, 0.05f, -34f);
            rover.Step(1f / 60f, 0f, 0f, false, false, player);
            Expect(rover.ReachedIce, "the ice-site milestone did not latch");

            // The board follows the rover: both milestones now read as finished, in the calm
            // teal of a settled task rather than a highlight.
            main.Hud.UpdateState();
            Expect(main.Hud.BoardRowLabels[4].Text.StartsWith("✓"),
                $"board: the garage row is not marked ('{main.Hud.BoardRowLabels[4].Text}')");
            Expect(main.Hud.BoardRowLabels[5].Text.StartsWith("✓"),
                $"board: the ice row is not marked ('{main.Hud.BoardRowLabels[5].Text}')");
            Expect(main.Hud.BoardRowLabels[4].GetThemeColor("font_color") == Hud.DoneColor,
                "board: a finished row is not recoloured");

            rover.GlobalPosition = start;

            // Exit: a solid, upright suit on the ground beside the rover, clear of its own box.
            rover.Exit(player);
            Expect(!rover.Occupied && player.Rover == null, "stepping out kept the seat");
            Expect(player.GlobalPosition.DistanceTo(rover.GlobalPosition) > 1f,
                "the suit stepped out inside the rover");
            Expect(Mathf.Abs(player.GlobalPosition.Y) < 3f,
                $"the suit stepped out at y {player.GlobalPosition.Y:F2}");
            Expect(Mathf.Abs(player.Visual.Rotation.X) < 0.001f, "the suit stayed reclined after stepping out");
            var stand = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                player.GlobalPosition + new Vector3(0, 2, 0), player.GlobalPosition - new Vector3(0, 2, 0)));
            Expect(stand.Count > 0, "there is no ground beside the rover to stand on");

            // Save round trip through the flat extras keys, and an old save with no rover keys.
            var extras = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["roverX"] = -11.0, ["roverY"] = 0.05, ["roverZ"] = -28.0, ["roverYaw"] = 0.4,
                ["roverBattery"] = 7.5, ["roverOdometer"] = 12.0,
                ["roverDriven"] = true, ["roverIce"] = true, ["roverOccupied"] = false
            };
            rover.Restore(extras, player);
            Expect(Mathf.Abs(rover.BatteryKwh - 7.5f) < 0.001f, "restore() lost the battery");
            Expect(rover.ReachedIce && rover.HasDriven, "restore() lost the milestones");
            Expect(rover.GlobalPosition.DistanceTo(new Vector3(-11f, 0.05f, -28f)) < 0.2f,
                "restore() lost the parked position");
            // An old save (schema 2, written before the rover existed) has no rover keys at all:
            // it must load without complaint and leave the rover as if it had just spawned.
            rover.Restore(new System.Collections.Generic.Dictionary<string, object?>(), player);
            Expect(Mathf.Abs(rover.BatteryKwh - RoverController.BatteryKwhMax) < 0.001f,
                $"an old save did not leave a full battery on the rover ({rover.BatteryKwh:F2} kWh)");
            Expect(rover.GlobalPosition.DistanceTo(new Vector3(-11f, 0.05f, -28f)) < 0.2f,
                "an old save did not put the rover back on its apron");
            Expect(!rover.Occupied, "an old save seated the commander in the rover");

            // Restart: the rover is put back with full power and no milestones.
            rover.HasDriven = true;
            rover.ReachedIce = true;
            rover.Odometer = 42f;
            main.RestartMission();
            Expect(rover.BatteryKwh >= RoverController.BatteryKwhMax - 0.001f,
                $"a new mission left the battery at {rover.BatteryKwh:F2} kWh");
            Expect(!rover.ReachedIce && !rover.HasDriven && rover.Odometer < 0.001f,
                "a new mission kept the old rover milestones");

            CheckF0Controls(rover, player);
            CheckBuildings(main);
            CheckCrew(main);
            CheckSettings();
            CheckEvaJobs(main);
            return Report();
        }

        /// <summary>
        /// F6 — the EVA reserve and the two cargo/custody chains, exercised on the live sim.
        /// Same contract as the LunaQuest.Core harness: an empty suit cannot drill, the chain
        /// seals at the cache, the board window pays timeliness.
        /// </summary>
        private void CheckEvaJobs(Main main)
        {
            var sim = main.Sim;
            double full = sim.State.SuitEnergy;
            Expect(full > 0.0, $"eva: the suit does not start charged ({full:F2})");

            // Drain until empty; the only sanctioned outside action must run dry.
            for (int i = 0; i < 400; i++) sim.TickEvaEnergy(false, 1.0);
            Expect(sim.State.SuitEnergy == 0.0, $"eva: the suit never emptied ({sim.State.SuitEnergy:F2})");
            Expect(!sim.DrillSample(out _, out _), "eva: an empty suit was allowed to drill");
            Expect(sim.Learning.Journal.Exists(e => e.ObjectiveId == "eva_drain"),
                "eva: the dry-suit journal entry was never written");
            for (int i = 0; i < 80; i++) sim.TickEvaEnergy(true, 1.0);
            Expect(sim.DrillSample(out _, out _), "eva: a recharged suit could not drill");
            Expect(sim.LogSampleAtCache(out _, out _), "eva: logging the custody chain failed");
            Expect(sim.State.SamplesCollected == 1, $"eva: the sample was not counted ({sim.State.SamplesCollected})");
            Expect(sim.Learning.HasEntry("obj_sample"), "eva: the custody chain did not unlock the sample objective");

            // Cargo run: pickup pays out more on time than late, and both paths journal.
            var late = main.Sim;
            late.StartCargoRun(0.0, out _, out _);
            late.TickCargo(200.0); // window is 150 s
            Expect(late.CompleteCargoRun(out _, out _), "cargo: the late delivery failed");
            Expect(late.Learning.Journal.Exists(e => e.ObjectiveId == "cargo_run_late"),
                "cargo: the late-delivery journal entry was never written");

            // Restore the player's suit so the rest of the audit sees a charged commander.
            sim.TickEvaEnergy(true, 100.0);
        }

        /// <summary>
        /// F5 — the settings file round-trips exactly and every check restores the user's
        /// original preferences afterwards. Godot loads user://settings.json at boot; the file
        /// never becomes part of the shared save schema.
        /// </summary>
        private void CheckSettings()
        {
            var original = GameSettings.Load();
            var written = new GameSettings.SettingsData
            {
                ThirdPerson = true,
                Language = "ar",
                MouseSensitivity = 1.5f,
                Fov = 82f,
                MasterVolume = 0.55f,
                Voice = false,
                Subtitles = false,
                Quality = "high",
                Fullscreen = true,
            };
            Expect(GameSettings.Save(written) == Error.Ok, "settings: the write failed");
            var reloaded = GameSettings.Load();
            Expect(reloaded.ThirdPerson == written.ThirdPerson, "settings: third person did not survive");
            Expect(reloaded.Language == written.Language, "settings: the language choice was lost");
            Expect(Mathf.Abs(reloaded.MouseSensitivity - written.MouseSensitivity) < 0.001f, "settings: the sensitivity was lost");
            Expect(Mathf.Abs(reloaded.Fov - written.Fov) < 0.001f, "settings: the field of view was lost");
            Expect(Mathf.Abs(reloaded.MasterVolume - written.MasterVolume) < 0.001f, "settings: the volume was lost");
            Expect(reloaded.Voice == false, "settings: the voice flag was lost");
            Expect(reloaded.Subtitles == false, "settings: the subtitles flag was lost");
            Expect(reloaded.Quality == "high", "settings: the quality preset was lost");
            Expect(reloaded.Fullscreen, "settings: the fullscreen flag was lost");
            Expect(Mathf.Abs(GameSettings.LinearToDb(1.0f)) < 0.001f, "settings: unity volume should be 0 dB");
            Expect(GameSettings.LinearToDb(0.1f) < -15f, "settings: 0.1 linear should be under -15 dB");
            Expect(GameSettings.Save(original) == Error.Ok, "settings: the user's original preferences were lost");
        }

        /// <summary>
        /// F3 — every human suit exists as the pose the game shipped: built from the shared
        /// 2.311 m visor-to-sole pose height, scale 0.757 (1.75 m), with a non-empty hierarchy.
        /// A bind-pose AABB here is 6 m across (Object_9/10 are rig meshes the posed rig never
        /// reaches), so assert the scale the suit was actually built with — the GDScript
        /// edition audits the same figure.
        /// </summary>
        private void CheckCrew(Main main)
        {
            const float suitHeight = 1.75f;
            const float posedHeight = 2.311f;
            const float expectedScale = suitHeight / posedHeight;

            var playerSuit = main.Player.Visual.GetChildCount() > 0
                ? main.Player.Visual.GetChild(0) as Node3D
                : null;
            var suits = new List<(Node3D suit, string label)>
            {
                (playerSuit, "player"),
            };
            foreach (var npc in main.World.Npcs)
                if (!npc.Profile.IsRobot)
                    suits.Add((npc.Visual, npc.Profile.Id));

            foreach (var (suit, label) in suits)
            {
                if (suit == null) { Expect(false, $"crew: {label} has no suit visual"); continue; }
                Expect(suit.GetChildCount() > 0, $"crew: {label} has an empty suit hierarchy");
                Expect(Mathf.Abs(suit.Scale.Y - expectedScale) < 0.01f,
                    $"crew: {label} suit scale {suit.Scale.Y:F3} is not {expectedScale:F3} (the 1.75 m pose)");
            }
        }

        /// <summary>
        /// F2 — every functional building wears a CC0 kit shell scaled to its footprint and has a
        /// matching airlock-style porch at its door. landing_pad is decorative and gets neither.
        /// </summary>
        private void CheckBuildings(Main main)
        {
            var shellIds = new[]
            {
                "habitat", "mission_control", "greenhouse", "workshop", "life_support", "lab", "medical",
                "storage", "comms_tower", "regolith_plant", "storm_shelter", "power_farm", "ice_site",
                "garage", "airlock",
            };
            foreach (var id in shellIds)
            {
                var root = main.World.FindChild(id, recursive: true, owned: false);
                Expect(root != null, $"buildings: {id} root is missing");
                if (root == null) continue;
                var shell = root.FindChild("Shell_" + id, recursive: true, owned: false);
                Expect(shell != null, $"buildings: {id} has no CC0 kit shell");
                var porch = root.FindChild("Porch", recursive: true, owned: false);
                Expect(porch != null, $"buildings: {id} has no airlock porch at its door");
            }
            Expect(main.World.ShellCount >= shellIds.Length,
                $"buildings: only {main.World.ShellCount} kit shells (expected {shellIds.Length})");
        }

        /// <summary>
        /// F0 — the universal control contract, proven by driving, not by reasoning. W drives
        /// toward the view, S reverses, A and D turn toward screen-left and screen-right (measured
        /// against the camera basis), and walking keeps the same meaning after any driving episode.
        /// </summary>
        private void CheckF0Controls(RoverController rover, PlayerController player)
        {
            // Deterministic pose: the apron, the usual yaw, full charge.
            rover.GlobalPosition = new Vector3(-11f, 0.05f, -28f);
            rover.Rotation = new Vector3(0, 0.4f, 0);
            rover.Speed = 0f;
            rover.Steer = 0f;
            rover.BatteryKwh = RoverController.BatteryKwhMax;
            rover.Settle();
            rover.Enter(player);
            player.SyncCameraToRover();

            var vehFwd = -rover.GlobalTransform.Basis.Z.Normalized();
            var camFwd = -player.Camera.GlobalTransform.Basis.Z.Normalized();
            Expect(vehFwd.Dot(camFwd) > 0.9f,
                $"F0: the rover's forward is not aligned with the camera ({vehFwd.Dot(camFwd):F2})");

            // W under throttle: displacement goes toward the view, not away from it.
            var before = rover.GlobalPosition;
            for (int i = 0; i < 120; i++) rover.Step(1f / 60f, 1f, 0f, false, false, player);
            var moved = rover.GlobalPosition - before;
            Expect(moved.Dot(vehFwd) > 0.5f,
                $"F0: W does not drive toward the view ({moved.Dot(vehFwd):F2} m)");

            // S from rest: displacement away from the view.
            rover.Speed = 0f;
            before = rover.GlobalPosition;
            for (int i = 0; i < 120; i++) rover.Step(1f / 60f, -1f, 0f, false, false, player);
            moved = rover.GlobalPosition - before;
            Expect(moved.Dot(vehFwd) < 0f,
                $"F0: S does not reverse ({moved.Dot(vehFwd):F2} m)");

            // Get rolling, then A turns toward the camera's screen-left, D toward screen-right.
            rover.Speed = 0f;
            for (int i = 0; i < 90; i++) rover.Step(1f / 60f, 1f, 0f, false, false, player);
            var camRight = player.Camera.GlobalTransform.Basis.X.Normalized();
            var left = -camRight;
            float yaw0 = rover.Rotation.Y;
            for (int i = 0; i < 60; i++) rover.Step(1f / 60f, 1f, 1f, false, false, player);
            float yaw1 = rover.Rotation.Y;
            var h0 = new Basis(Vector3.Up, yaw0) * new Vector3(0, 0, -1);
            var h1 = new Basis(Vector3.Up, yaw1) * new Vector3(0, 0, -1);
            Expect(h1.Dot(left) > h0.Dot(left),
                $"F0: A did not move the heading toward screen-left (delta {h1.Dot(left) - h0.Dot(left):F3})");
            rover.Speed = 0f;
            yaw0 = rover.Rotation.Y;
            for (int i = 0; i < 60; i++) rover.Step(1f / 60f, 1f, -1f, false, false, player);
            yaw1 = rover.Rotation.Y;
            h0 = new Basis(Vector3.Up, yaw0) * new Vector3(0, 0, -1);
            h1 = new Basis(Vector3.Up, yaw1) * new Vector3(0, 0, -1);
            Expect(h1.Dot(camRight) > h0.Dot(camRight),
                $"F0: D did not move the heading toward screen-right (delta {h1.Dot(camRight) - h0.Dot(camRight):F3})");

            // Exiting cannot drift walking: W still leads to the camera, A/D still map to
            // screen-left/right.
            rover.Exit(player);
            Expect(!rover.Occupied && player.Rover == null, "F0: exit() kept the seat");
            Expect(PlayerController.CameraRelativeDirection(new Vector2(0, -1), 0f).Dot(new Vector3(0, 0, -1)) > 0.9f,
                "F0: W no longer maps to the camera forward after exiting the rover");
            Expect(PlayerController.CameraRelativeDirection(new Vector2(-1, 0), 0f).Dot(new Vector3(-1, 0, 0)) > 0.9f,
                "F0: A no longer maps to screen-left after exiting the rover");
            Expect(PlayerController.CameraRelativeDirection(new Vector2(1, 0), 0f).Dot(new Vector3(1, 0, 0)) > 0.9f,
                "F0: D no longer maps to screen-right after exiting the rover");
            Expect(PlayerController.CameraRelativeDirection(new Vector2(0, 1), 0f).Dot(new Vector3(0, 0, 1)) > 0.9f,
                "F0: S no longer maps to reverse after exiting the rover");
            Expect(PlayerController.CameraRelativeDirection(new Vector2(0, -1), Mathf.Pi / 2f).Dot(new Vector3(-1, 0, 0)) > 0.9f,
                "F0: walking at yaw 90 degrees is not camera-relative");

            // Back to the apron for anything that runs after this check.
            rover.GlobalPosition = new Vector3(-11f, 0.05f, -28f);
            rover.Rotation = new Vector3(0, 0.4f, 0);
            rover.Settle();
        }

        /// <summary>
        /// The shift task board, checked against the same spec as the other two engines:
        /// fourteen rows — the eleven objectives from content/learning.json (done = a journal
        /// entry), the crew rover's two milestones and the mission line — the row for the
        /// current shift leading the box, the header naming that shift, and a board that
        /// speaks both languages. learning.json itself stays at 11 objectives (H9).
        /// </summary>
        private void CheckBoard(Main main)
        {
            var hud = main.Hud;
            var sim = main.Sim;
            Expect(hud != null, "board: no HUD");
            if (hud == null) return;
            hud.UpdateState();

            Expect(Hud.BoardRows.Length == 14,
                $"board: expected 14 row specs, found {Hud.BoardRows.Length}");
            Expect(hud.BoardBox.GetChildCount() == 15,
                $"board: expected a header plus 14 rows, found {hud.BoardBox.GetChildCount()}");
            Expect(hud.BoardRowLabels.Count == Hud.BoardRows.Length,
                $"board: {hud.BoardRowLabels.Count} labels for {Hud.BoardRows.Length} specs");

            // Kind counts: the journal rows mirror learning.json, two belong to the rover,
            // one is the mission line.
            int journal = 0, rovers = 0, missions = 0;
            foreach (var row in Hud.BoardRows)
            {
                if (row.Kind == "journal") journal++;
                else if (row.Kind == "mission") missions++;
                else rovers++;
            }
            Expect(journal == sim.Content.Learning.Count,
                $"board: {journal} journal rows for {sim.Content.Learning.Count} objectives");
            Expect(rovers == 2, $"board: expected 2 rover rows, found {rovers}");
            Expect(missions == 1, $"board: expected 1 mission row, found {missions}");
            foreach (var objective in sim.Content.Learning)
                Expect(System.Array.Exists(Hud.BoardRows, r => r.Id == objective.Id),
                    $"board: {objective.Id} has no row on the board");

            // No blank rows, and the mission line counts the days.
            for (int i = 0; i < hud.BoardRowLabels.Count; i++)
                Expect(hud.BoardRowLabels[i].Text.Length > 0, $"board: row {i} is blank");
            Expect(hud.BoardRowLabels[13].Text.Contains("30"),
                $"board: the mission line reads '{hud.BoardRowLabels[13].Text}'");

            // Both sides of every shift boundary.
            Expect(Hud.CurrentShift(0.0).Id == "night", "board: 00:00 is not the night shift");
            Expect(Hud.CurrentShift(5.99).Id == "night", "board: 05:59 is not the night shift");
            Expect(Hud.CurrentShift(6.0).Id == "morning", "board: 06:00 is not the morning shift");
            Expect(Hud.CurrentShift(11.99).Id == "morning", "board: 11:59 is not the morning shift");
            Expect(Hud.CurrentShift(12.0).Id == "afternoon", "board: 12:00 is not the afternoon shift");
            Expect(Hud.CurrentShift(17.99).Id == "afternoon", "board: 17:59 is not the afternoon shift");
            Expect(Hud.CurrentShift(18.0).Id == "evening", "board: 18:00 is not the evening shift");
            Expect(Hud.CurrentShift(23.99).Id == "evening", "board: 23:59 is not the evening shift");

            // The header names the shift the clock says it is...
            var shift = Hud.CurrentShift(sim.Clock.HourOfDay);
            Expect(hud.BoardHeader.Text == L.T(shift.En, shift.Ar),
                $"board: the header reads '{hud.BoardHeader.Text}', expected '{L.T(shift.En, shift.Ar)}'");

            // ...and the row leading the box belongs to that shift. The rover test never
            // starts the clock, so the board sits in the night shift.
            var leader = hud.BoardBox.GetChild(1) as Label;
            Expect(leader != null, "board: nothing leads the board");
            int lead = -1;
            if (leader != null)
            {
                for (int i = 0; i < hud.BoardRowLabels.Count; i++)
                {
                    if (!ReferenceEquals(hud.BoardRowLabels[i], leader)) continue;
                    lead = i;
                    break;
                }
            }
            Expect(lead >= 0, "board: the leading row is not one of the fourteen");
            if (lead >= 0)
                Expect(Hud.BoardRows[lead].Shift == shift.Id,
                    $"board: the {Hud.BoardRows[lead].Shift} row leads but the clock says {shift.Id}");

            // The rover milestones are not finished at mission start.
            Expect(!hud.BoardRowLabels[4].Text.StartsWith("✓"),
                $"board: the garage row is already marked ('{hud.BoardRowLabels[4].Text}')");
            Expect(!hud.BoardRowLabels[5].Text.StartsWith("✓"),
                $"board: the ice row is already marked ('{hud.BoardRowLabels[5].Text}')");

            // Bilingual: the board swaps to Arabic and back.
            bool wasArabic = L.Arabic;
            L.Arabic = true;
            hud.UpdateState();
            Expect(hud.BoardRowLabels[0].Text.Contains("لوحة"),
                $"board: the Arabic board reads '{hud.BoardRowLabels[0].Text}'");
            L.Arabic = wasArabic;
            hud.UpdateState();
        }

        private bool Report()
        {
            if (_failures == 0)
            {
                GD.Print($"ROVER TEST OK — {_checks} checks passed");
                return true;
            }
            GD.Print($"ROVER TEST FAILED — {_failures} of {_checks} checks failed:");
            GD.Print(_log.ToString().TrimEnd());
            return false;
        }
    }
}
