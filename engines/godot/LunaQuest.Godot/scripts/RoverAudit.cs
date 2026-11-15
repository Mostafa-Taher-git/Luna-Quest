using System;
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
            Expect(rover.Spin > 1f, "the wheels did not spin");
            Expect(rover.GlobalPosition.Y > -2f, $"the rover sank through the ground (y {rover.GlobalPosition.Y:F2})");
            // BUG-5: the driver leans back against the seat (rotation.x = -0.32 rad, yaw PI).
            float lean = player.Visual.Rotation.X;
            Expect(lean < -0.2f && lean > -0.5f,
                $"the driver is not leaning back (visual X {lean:F2} rad, expected -0.32)");

            // Handbrake and a flat battery.
            for (int i = 0; i < 120; i++) rover.Step(1f / 60f, 0f, 0f, false, true, player);
            Expect(Mathf.Abs(rover.Speed) < 0.05f, $"the handbrake left it rolling at {rover.Speed:F2} m/s");
            rover.BatteryKwh = 0f;
            for (int i = 0; i < 60; i++) rover.Step(1f / 60f, 1f, 0f, false, false, player);
            Expect(Mathf.Abs(rover.Speed) < 0.01f, $"a flat battery still drove ({rover.Speed:F2} m/s)");

            // The garage apron recharges a stopped rover; the ice site sets the milestone.
            rover.GlobalPosition = new Vector3(-16f, 0.05f, -40f);
            rover.BatteryKwh = 1f;
            for (int i = 0; i < 60; i++) rover.Step(1f / 60f, 0f, 0f, false, false, player);
            Expect(rover.BatteryKwh > 1f, "the garage apron did not recharge the rover");
            rover.ReachedIce = false;
            rover.GlobalPosition = new Vector3(52f, 0.05f, -34f);
            rover.Step(1f / 60f, 0f, 0f, false, false, player);
            Expect(rover.ReachedIce, "the ice-site milestone did not latch");
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
            rover.Restore(new System.Collections.Generic.Dictionary<string, object?>(), player);
            Expect(Mathf.Abs(rover.BatteryKwh - 7.5f) < 0.001f,
                "an old save with no rover keys disturbed the rover");

            // Restart: the rover is put back with full power and no milestones.
            rover.HasDriven = true;
            rover.ReachedIce = true;
            rover.Odometer = 42f;
            main.RestartMission();
            Expect(rover.BatteryKwh >= RoverController.BatteryKwhMax - 0.001f,
                $"a new mission left the battery at {rover.BatteryKwh:F2} kWh");
            Expect(!rover.ReachedIce && !rover.HasDriven && rover.Odometer < 0.001f,
                "a new mission kept the old rover milestones");

            return Report();
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
