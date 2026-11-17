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

        /// <summary>
        /// Last place the rover's own body sat. Physics transforms reach the physics server on the
        /// next tick, and input (which drives the physics process) runs before that tick — so at the
        /// moment E is pressed, GlobalPosition can be a frame ahead of where the collider actually
        /// is. exit() measures from the settled pose so the commander is never dropped into the
        /// rover's own box.
        /// </summary>
        private Vector3 _settledPosition = new Vector3(-11f, 0.05f, -28f);
        private float _settledYaw = 0.4f;

        private Node3D _model = null!;
        private AudioStreamPlayer3D? _hum;
        private readonly GpuParticles3D[] _dust = new GpuParticles3D[2];
        private readonly List<(Node3D node, bool front)> _wheels = new();
        private readonly List<(Node3D node, bool front)> _steers = new();
        private readonly Vector3 _seat = new Vector3(0f, 0.34f, 0.18f);

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
            BuildHum();
            BuildDust();
        }

        private Node3D LoadModel()
        {
            const string path = "res://assets/models/lunar_colony/open-rover-unpressuris-0da93181.glb";
            if (ResourceLoader.Exists(path))
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene != null)
                {
                    var instance = scene.Instantiate<Node3D>();
                    // The kit GLB's nose is +Z (front wheels at z = +1.16); the rover's
                    // forward is -Z (F0 control contract), so the visual nose is swung 180 degrees.
                    instance.Rotation = new Vector3(0, Mathf.Pi, 0);
                    return instance;
                }
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

        /// <summary>F7: a two-stroke hum generated at runtime — nothing is downloaded. Pitch follows speed.</summary>
        private void BuildHum()
        {
            _hum = new AudioStreamPlayer3D
            {
                Name = "EngineHum",
                MaxDistance = 26f,
                UnitSize = 4f,
                VolumeDb = -9f,
            };
            const int sampleRate = 22050;
            var frames = sampleRate;
            var data = new byte[frames * 2];
            float fundamental = sampleRate / 55f;
            for (int i = 0; i < frames; i++)
            {
                float phase = (i % fundamental) / fundamental;
                float saw = phase * 2f - 1f;
                float sub = Mathf.Sin(i * Mathf.Tau * 27.5f / sampleRate);
                float v = Mathf.Clamp(saw * 0.55f + sub * 0.35f, -1f, 1f) * 9000f;
                short s = (short)v;
                data[i * 2] = (byte)(s & 0xFF);
                data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = sampleRate,
                Stereo = false,
                LoopMode = AudioStreamWav.LoopModeEnum.Forward,
                Data = data,
            };
            _hum.Stream = wav;
            AddChild(_hum);
        }

        /// <summary>F7: regolith puffs kicked from the rear wheels while moving.</summary>
        private void BuildDust()
        {
            var process = new ParticleProcessMaterial
            {
                Direction = new Vector3(0f, 0.55f, 1f),
                Spread = 24f,
                Gravity = new Vector3(0f, -1.1f, 0f),
                InitialVelocityMin = 1.2f,
                InitialVelocityMax = 3.2f,
                ScaleMin = 0.12f,
                ScaleMax = 0.4f,
            };
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.62f, 0.58f, 0.53f, 0.55f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var p = new GpuParticles3D
                {
                    Name = i == 0 ? "DustLeft" : "DustRight",
                    Position = new Vector3(side * 0.96f, 0.2f, 1.15f),
                    Emitting = false,
                    Amount = 18,
                    Lifetime = 1.5f,
                    ProcessMaterial = process,
                    DrawPasses = 1,
                };
                p.MaterialOverride = mat;
                p.DrawPass1 = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 8, Rings = 4 };
                AddChild(p);
                _dust[i] = p;
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

        /// <summary>Keep the exit reference in step with a rover that was placed, not driven.</summary>
        public void Settle()
        {
            _settledPosition = GlobalPosition;
            _settledYaw = Rotation.Y;
        }

        public void Exit(PlayerController p)
        {
            Occupied = false;
            Speed = 0f;
            Velocity = Vector3.Zero;
            p.Rover = null;
            Vector3 side = new Basis(Vector3.Up, _settledYaw).X.Normalized();
            Vector3 spot = new Vector3(_settledPosition.X, GlobalPosition.Y, _settledPosition.Z) + side * 1.8f;
            spot.Y = 0.05f;
            p.GlobalPosition = spot;
            p.Velocity = Vector3.Zero;
            p.Visual.Rotation = new Vector3(0, Mathf.Pi, 0);
            p.RestoreWalkCamera();
        }

        /// <summary>
        /// Capture-only throttle for `--drive` (see Main.cs): a screenshot run has no hands on
        /// the keyboard, so the flag supplies the pedal. Zero in normal play, where the keys
        /// decide the throttle exactly as before.
        /// </summary>
        public static float CaptureThrottle;

        /// <summary>Called by PlayerController._PhysicsProcess while the commander is at the wheel.</summary>
        public void Drive(float dt, PlayerController p)
        {
            float throttle = CaptureThrottle != 0f
                ? CaptureThrottle
                : (Input.IsActionPressed("move_forward") ? 1f : 0f) - (Input.IsActionPressed("move_back") ? 1f : 0f);
            float steerIn = (Input.IsActionPressed("move_left") ? 1f : 0f) - (Input.IsActionPressed("move_right") ? 1f : 0f);
            bool handbrake = Input.IsActionPressed("jump");
            bool boost = Input.IsActionPressed("sprint");
            Step(dt, throttle, steerIn, boost, handbrake, p);
        }

        /// <summary>
        /// One physics step with the controls passed in — actions in play, the audit (--rover-test)
        /// when it drives the vehicle itself. Keeps the vehicle testable without a keyboard.
        /// </summary>
        public void Step(float dt, float throttle, float steerIn, bool boost, bool handbrake,
            PlayerController p)
        {
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

            Vector3 forward = -GlobalTransform.Basis.Z.Normalized();
            Vector3 v = forward * Speed;
            v.Y = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
            Velocity = v;
            MoveAndSlide();
            _settledPosition = GlobalPosition;
            _settledYaw = Rotation.Y;
            Odometer += Mathf.Abs(Speed) * dt;

            // F7: the ear leads the eye — hum pitch follows speed; dust kicks off the rear wheels.
            if (_hum != null)
            {
                float ratio = Mathf.Clamp(Mathf.Abs(Speed) / MaxForward, 0f, 1f);
                _hum.PitchScale = 0.55f + ratio * 1.55f;
                _hum.VolumeDb = -16f + ratio * 8f;
                bool driving = Mathf.Abs(Speed) > 0.4f;
                if (driving && !_hum.Playing) _hum.Play();
                else if (!driving && _hum.Playing) _hum.Stop();
            }
            bool floorHere = IsOnFloor();
            foreach (var dust in _dust) dust.Emitting = Mathf.Abs(Speed) > 1.6f && floorHere;

            // Suspension tilt from the four contact patches.
            var world = Main.Instance?.World;
            if (world != null && _model is Node3D model)
            {
                var right = new Vector3(Mathf.Cos(Rotation.Y), 0, Mathf.Sin(Rotation.Y));
                var fwd = new Vector3(Mathf.Sin(Rotation.Y), 0, -Mathf.Cos(Rotation.Y));
                float H(float r, float f)
                {
                    var p2 = GlobalPosition + right * r + fwd * f;
                    return (float)world.Terrain.Height(p2.X, p2.Z);
                }
                float front = (H(-0.96f, 1.16f) + H(0.96f, 1.16f)) * 0.5f;
                float rear = (H(-0.96f, -1.16f) + H(0.96f, -1.16f)) * 0.5f;
                float left = (H(-0.96f, 1.16f) + H(-0.96f, -1.16f)) * 0.5f;
                float rightSide = (H(0.96f, 1.16f) + H(0.96f, -1.16f)) * 0.5f;
                float pitch = Mathf.Clamp(Mathf.Atan((rear - front) / 2.32f), -0.38f, 0.38f);
                float roll = Mathf.Clamp(Mathf.Atan((rightSide - left) / 1.92f), -0.38f, 0.38f);
                model.Rotation = new Vector3(
                    Mathf.LerpAngle(model.Rotation.X, pitch, Mathf.Clamp(dt * 6f, 0f, 1f)),
                    model.Rotation.Y,
                    Mathf.LerpAngle(model.Rotation.Z, roll, Mathf.Clamp(dt * 6f, 0f, 1f)));
            }

            Spin -= (Speed / WheelRadius) * dt;
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

        public void Restore(Dictionary<string, object?> extras, PlayerController p)
        {
            // SaveData.Extras is Dictionary<string, object?>; the Json helpers already accept a
            // nullable payload (a missing or null extras key falls back to the default).
            float Num(string k, float d) => extras.TryGetValue(k, out var o) ? (float)LunaQuest.Core.Content.Json.Num(o, d) : d;
            bool Bool(string k, bool d) => extras.TryGetValue(k, out var o) && LunaQuest.Core.Content.Json.Bool(o, d);
            if (extras.ContainsKey("roverX"))
                GlobalPosition = new Vector3(Num("roverX", -11f), Num("roverY", 0.05f), Num("roverZ", -28f));
            Rotation = new Vector3(0, Num("roverYaw", 0.4f), 0);
            BatteryKwh = Mathf.Clamp(Num("roverBattery", BatteryKwhMax), 0f, BatteryKwhMax);
            Odometer = Num("roverOdometer", 0f);
            HasDriven = Bool("roverDriven", false);
            ReachedIce = Bool("roverIce", false);
            // The saved pose is where the rover now is: exit() measures the step-out from here.
            Settle();
            if (Bool("roverOccupied", false)) Enter(p);
        }
    }
}
