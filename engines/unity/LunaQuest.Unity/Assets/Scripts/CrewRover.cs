using System.Collections.Generic;
using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// The rideable crew rover — Unity edition, mirroring the GDScript ColonyRover and the
    /// Godot .NET RoverController (same constants, same battery/garage/ice-site rules).
    /// Unity has no glTF package in this project, so the LTV body is built from primitives
    /// (the same fallback the other engines use when the kit is absent); the wheels are
    /// separate objects so they spin and four-corner-steer. The driver is the one Cute
    /// Astronaut, carried at the hand controller by transform — never a reshaped character.
    /// </summary>
    public sealed class CrewRover : MonoBehaviour
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

        private CharacterController _controller;
        private readonly List<(Transform wheel, Transform steer, bool front)> _corners = new();
        private readonly Vector3 _seat = new Vector3(0f, 0.34f, -0.18f);
        private float _vertical;

        public float SpeedKmh() => Mathf.Abs(Speed) * 3.6f;
        public float BatteryPercent() => BatteryKwh / BatteryKwhMax;
        public Vector3 SeatGlobal => transform.TransformPoint(_seat);

        public static CrewRover Create(LunaQuestGame game)
        {
            var go = new GameObject("CrewRover");
            var rover = go.AddComponent<CrewRover>();
            rover._controller = go.AddComponent<CharacterController>();
            rover._controller.radius = 1.1f;
            rover._controller.height = 1.4f;
            rover._controller.center = new Vector3(0f, 0.7f, 0f);
            rover._controller.slopeLimit = 50f;
            rover._controller.stepOffset = 0.35f;
            rover.BuildBody();
            return rover;
        }

        private void BuildBody()
        {
            var hull = new Material(Shader.Find("Standard")) { color = new Color(0.83f, 0.86f, 0.89f) };
            var dark = new Material(Shader.Find("Standard")) { color = new Color(0.13f, 0.13f, 0.14f) };
            var alloy = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.58f, 0.62f) };
            Body("Chassis", PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(2.2f, 0.5f, 3.0f), hull);
            Body("Deck", PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.4f), new Vector3(1.4f, 0.3f, 1.4f), dark);
            Body("Mast", PrimitiveType.Cylinder, new Vector3(0.6f, 1.7f, 0.9f), new Vector3(0.08f, 0.7f, 0.08f), alloy);
            Body("Dish", PrimitiveType.Cube, new Vector3(0.6f, 2.2f, 0.9f), new Vector3(0.4f, 0.3f, 0.1f), hull);
            float[,] corners = { { -0.96f, 1.16f }, { 0.96f, 1.16f }, { -0.96f, -1.16f }, { 0.96f, -1.16f } };
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var steer = new GameObject("steer-" + i).transform;
                steer.SetParent(transform, false);
                steer.localPosition = new Vector3(corners[i, 0], 0.44f, corners[i, 1]);
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "wheel-" + i;
                Object.Destroy(wheel.GetComponent<Collider>());
                wheel.transform.SetParent(steer, false);
                wheel.transform.localPosition = Vector3.zero;
                wheel.transform.localScale = new Vector3(0.88f, 0.16f, 0.88f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.GetComponent<Renderer>().sharedMaterial = dark;
                _corners.Add((wheel.transform, steer, front));
            }
        }

        private void Body(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        public void Enter(ColonyPlayer p)
        {
            Occupied = true;
            HasDriven = true;
            p.Rover = this;
            p.ConfigureDriveCamera();
        }

        public void Exit(ColonyPlayer p)
        {
            Occupied = false;
            Speed = 0f;
            _vertical = 0f;
            p.Rover = null;
            Vector3 side = transform.right;
            Vector3 spot = transform.position + side * 1.8f;
            spot.y = 0.05f;
            p.Teleport(spot);
            p.Halt();
            p.Visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            p.RestoreWalkCamera();
        }

        /// <summary>Called by ColonyPlayer.Update while the commander is at the wheel.</summary>
        public void Drive(float dt, ColonyPlayer p)
        {
            float throttle = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float steerIn = (Input.GetKey(KeyCode.A) ? 1f : 0f) - (Input.GetKey(KeyCode.D) ? 1f : 0f);
            bool handbrake = Input.GetKey(KeyCode.Space);
            bool boost = Input.GetKey(KeyCode.LeftShift);
            bool flat = BatteryKwh <= 0.05f;
            float maxF = flat ? 0f : MaxForward * (boost ? 1f : 0.72f);

            if (throttle > 0f && !flat) Speed += Accel * dt;
            else if (throttle < 0f) Speed -= (Speed > 0f ? Brake : Accel * 0.6f) * dt;
            else Speed = Mathf.MoveTowards(Speed, 0f, Drag * dt);
            if (handbrake) Speed = Mathf.MoveTowards(Speed, 0f, Brake * 1.4f * dt);
            Speed = Mathf.Clamp(Speed, -MaxReverse, maxF);

            float steerTarget = steerIn * SteerMax * (0.55f + 0.45f / (1f + Mathf.Abs(Speed) * 0.35f));
            Steer = Mathf.Lerp(Steer, steerTarget, Mathf.Clamp(dt * 6f, 0f, 1f));
            transform.Rotate(0f, (Speed / 2.3f) * Mathf.Tan(Steer) * dt * Mathf.Rad2Deg, 0f);

            if (_controller.isGrounded) _vertical = -2f;
            else _vertical -= Gravity * dt;
            Vector3 velocity = transform.forward * Speed + new Vector3(0f, _vertical, 0f);
            _controller.Move(velocity * dt);
            Odometer += Mathf.Abs(Speed) * dt;

            Spin += (Speed / WheelRadius) * dt;
            float spinDeg = Spin * Mathf.Rad2Deg;
            float steerDeg = Steer * Mathf.Rad2Deg;
            foreach (var (wheel, steer, front) in _corners)
            {
                steer.localRotation = Quaternion.Euler(0f, front ? steerDeg : -steerDeg * 0.4f, 0f);
                wheel.localRotation = Quaternion.Euler(spinDeg, 0f, 90f);
            }

            var pos2 = new Vector2(transform.position.x, transform.position.z);
            if (Vector2.Distance(pos2, Garage) < 12f && Mathf.Abs(Speed) < 0.2f)
                BatteryKwh = Mathf.Min(BatteryKwhMax, BatteryKwh + 6f * dt / 60f);
            else
                BatteryKwh = Mathf.Max(0f, BatteryKwh - Mathf.Abs(Speed) * 0.9f * dt / 60f);
            if (!ReachedIce && Vector2.Distance(pos2, IceSite) < 12f) ReachedIce = true;

            p.transform.position = SeatGlobal;
            p.Halt();
            p.Visual.localRotation = Quaternion.Euler(18f, 180f, 0f);
        }

        public void Restore(Dictionary<string, object> extras, ColonyPlayer p)
        {
            float Num(string k, float d) => extras.TryGetValue(k, out var o) ? (float)LunaQuest.Core.Content.Json.Num(o, d) : d;
            bool Bool(string k, bool d) => extras.TryGetValue(k, out var o) && LunaQuest.Core.Content.Json.Bool(o, d);
            if (extras.ContainsKey("roverX"))
                transform.position = new Vector3(Num("roverX", -11f), Num("roverY", 0.05f), Num("roverZ", -28f));
            transform.rotation = Quaternion.Euler(0f, Num("roverYaw", 0.4f) * Mathf.Rad2Deg, 0f);
            BatteryKwh = Mathf.Clamp(Num("roverBattery", BatteryKwhMax), 0f, BatteryKwhMax);
            Odometer = Num("roverOdometer", 0f);
            HasDriven = Bool("roverDriven", false);
            ReachedIce = Bool("roverIce", false);
            if (Bool("roverOccupied", false)) Enter(p);
        }
    }
}
