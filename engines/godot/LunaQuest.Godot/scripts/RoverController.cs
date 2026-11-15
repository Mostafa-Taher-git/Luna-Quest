using System.Collections.Generic;
using Godot;

namespace LunaQuest.Godot
{
    /// <summary>
    /// The rideable crew rover — the Godot .NET edition's real LTV, mirroring the GDScript
    /// ColonyRover. The mesh is the CC0 "Moon Base Colony and Lunar Rovers" open unpressurised
    /// rover, whose separated wheel / steer sub-meshes spin and four-corner-steer. It is a
    /// CharacterBody3D, so MoveAndSlide does the collision work against the colony under lunar
    /// gravity (1.62 m/s²). The driver is the one Cute Astronaut, carried at the hand controller
    /// by transform — the GLB is frozen and never reshaped.
    /// </summary>
    public partial class RoverController : CharacterBody3D
    {
        public const float Gravity = 1.62f;
        public const float MaxForward = 8.0f;
        public const float MaxReverse = 2.2f;
        public const float Accel = 3.4f;
        public const float Brake = 5.0f;
        public const float Drag = 0.55f;
        public const float SteerMax = 0.52f;
        public const float WheelRadius = 0.44f;
        public const float BatteryKwhMax = 14.0f;
        private static readonly Vector2 Garage = new Vector2(-16f, -40f);
        private static readonly Vector2 IceSite = new Vector2(52f, -34f);

        public bool Occupied, HasDriven, ReachedIce;
        public float Speed, Steer, Spin, Odometer;
        public float BatteryKwh = BatteryKwhMax;

        private Node3D _model = null!;
        private readonly List<(Node3D node, bool front)> _wheels = new();
        private readonly List<(Node3D node, bool front)> _steers = new();
        private readonly Vector3 _seat = new Vector3(0f, 0.34f, -0.18f);

        public override void _Ready()
        {
            CollisionLayer = 1;
            CollisionMask = 1;
            FloorMaxAngle = Mathf.DegToRad(50f);
            FloorSnapLength = 0.4f;
            var shape = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(2.3f, 1.4f, 3.0f) },
                Position = new Vector3(0, 0.7f, 0)
            };
            AddChild(shape);
            _model = LoadModel();
            AddChild(_model);
            RigWheels();
        }

        private Node3D LoadModel()
        {
            const string path = "res://assets/models/lunar_colony/open-rover-unpressuris-0da93181.glb";
            if (ResourceLoader.Exists(path))
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene != null) return scene.Instantiate<Node3D>();
            }
            // Fallback for a fresh clone without the kit: a simple box LTV, never a new character.
            var holder = new Node3D();
            var body = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.4f, 0.6f, 3.0f) },
                Position = new Vector3(0, 0.8f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.83f, 0.86f, 0.89f), Roughness = 0.6f }
            };
            holder.AddChild(body);
            return holder;
        }

        private void RigWheels()
        {
            string[] corners = { "front-left", "front-right", "rear-left", "rear-right" };
            foreach (var corner in corners)
            {
                bool front = corner.StartsWith("front");
                foreach (Node child in _model.FindChildren("*", "Node3D", true, false))
                {
                    if (child is not Node3D n) continue;
                    string name = n.Name.ToString().ToLower();
                    if (name == "wheel-" + corner) _wheels.Add((n, front));
                    else if (name == "steer-" + corner) _steers.Add((n, front));
                }
            }
        }

        public Vector3 SeatGlobal => GlobalTransform * _seat;
        public float SpeedKmh() => Mathf.Abs(Speed) * 3.6f;
        public float BatteryPercent() => BatteryKwh / BatteryKwhMax;

        public void Enter(PlayerController p)
        {
            Occupied = true;
            HasDriven = true;
            p.Rover = this;
            p.ConfigureDriveCamera();
        }

        public void Exit(PlayerController p)
        {
            Occupied = false;
            Speed = 0f;
            Velocity = Vector3.Zero;
            p.Rover = null;
            Vector3 side = GlobalTransform.Basis.X.Normalized();
            Vector3 spot = GlobalPosition + side * 1.8f;
            spot.Y = 0.05f;
            p.GlobalPosition = spot;
            p.Velocity = Vector3.Zero;
            p.Visual.Rotation = new Vector3(0, Mathf.Pi, 0);
            p.RestoreWalkCamera();
        }

        /// <summary>Called by PlayerController._PhysicsProcess while the commander is at the wheel.</summary>
        public void Drive(float dt, PlayerController p)
        {
            float throttle = (Input.IsActionPressed("move_forward") ? 1f : 0f) - (Input.IsActionPressed("move_back") ? 1f : 0f);
            float steerIn = (Input.IsActionPressed("move_left") ? 1f : 0f) - (Input.IsActionPressed("move_right") ? 1f : 0f);
            bool handbrake = Input.IsActionPressed("jump");
            bool boost = Input.IsActionPressed("sprint");
            bool flat = BatteryKwh <= 0.05f;
            float maxF = flat ? 0f : MaxForward * (boost ? 1f : 0.72f);

            if (throttle > 0f && !flat) Speed += Accel * dt;
            else if (throttle < 0f) Speed -= (Speed > 0f ? Brake : Accel * 0.6f) * dt;
            else Speed = Mathf.MoveToward(Speed, 0f, Drag * dt);
            if (handbrake) Speed = Mathf.MoveToward(Speed, 0f, Brake * 1.4f * dt);
            Speed = Mathf.Clamp(Speed, -MaxReverse, maxF);

            float steerTarget = steerIn * SteerMax * (0.55f + 0.45f / (1f + Mathf.Abs(Speed) * 0.35f));
            Steer = Mathf.Lerp(Steer, steerTarget, Mathf.Clamp(dt * 6f, 0f, 1f));
            float yaw = Rotation.Y + (Speed / 2.3f) * Mathf.Tan(Steer) * dt;
            Rotation = new Vector3(0, yaw, 0);

            Vector3 forward = GlobalTransform.Basis.Z.Normalized();
            Vector3 v = forward * Speed;
            v.Y = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
            Velocity = v;
            MoveAndSlide();
            Odometer += Mathf.Abs(Speed) * dt;

            Spin += (Speed / WheelRadius) * dt;
            foreach (var (node, front) in _wheels)
                node.Rotation = new Vector3(Spin, front ? Steer : -Steer * 0.4f, 0);
            foreach (var (node, front) in _steers)
                node.Rotation = new Vector3(0, front ? Steer : -Steer * 0.4f, 0);

            var pos2 = new Vector2(GlobalPosition.X, GlobalPosition.Z);
            if (pos2.DistanceTo(Garage) < 12f && Mathf.Abs(Speed) < 0.2f)
                BatteryKwh = Mathf.Min(BatteryKwhMax, BatteryKwh + 6f * dt / 60f);
            else
                BatteryKwh = Mathf.Max(0f, BatteryKwh - Mathf.Abs(Speed) * 0.9f * dt / 60f);
            if (!ReachedIce && pos2.DistanceTo(IceSite) < 12f) ReachedIce = true;

            // Carry the commander at the hand controller, facing the way the rover points.
            p.GlobalPosition = SeatGlobal;
            p.Velocity = Vector3.Zero;
            p.Visual.Rotation = new Vector3(-0.32f, Mathf.Pi, 0);
        }

        public void Restore(Dictionary<string, object> extras, PlayerController p)
        {
            float Num(string k, float d) => extras.TryGetValue(k, out var o) ? (float)LunaQuest.Core.Content.Json.Num(o, d) : d;
            bool Bool(string k, bool d) => extras.TryGetValue(k, out var o) && LunaQuest.Core.Content.Json.Bool(o, d);
            if (extras.ContainsKey("roverX"))
                GlobalPosition = new Vector3(Num("roverX", -11f), Num("roverY", 0.05f), Num("roverZ", -28f));
            Rotation = new Vector3(0, Num("roverYaw", 0.4f), 0);
            BatteryKwh = Mathf.Clamp(Num("roverBattery", BatteryKwhMax), 0f, BatteryKwhMax);
            Odometer = Num("roverOdometer", 0f);
            HasDriven = Bool("roverDriven", false);
            ReachedIce = Bool("roverIce", false);
            if (Bool("roverOccupied", false)) Enter(p);
        }
    }
}
