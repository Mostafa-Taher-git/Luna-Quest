using System;
using Godot;
using LunaQuest.Core.Domain;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Suit movement tuned to lunar gravity: low gravity, long strides, a real jump arc.
    /// Third person by default so the astronaut model is visible; V switches to the helmet view.
    /// </summary>
    public partial class PlayerController : CharacterBody3D
    {
        public const float WalkSpeed = 3.4f;
        public const float RunSpeed = 6.2f;
        public const float JumpSpeed = 3.1f;
        public const float Gravity = 1.62f;

        public Node3D Visual = null!;
        public Camera3D Camera = null!;
        public bool ThirdPerson = true;
        public bool InputLocked = true;

        private SpringArm3D _arm = null!;
        private SpotLight3D _suitLight = null!;
        private float _yaw;
        private float _pitch = -0.12f;
        private float _bob;
        private Interactable? _focus;

        public override void _Ready()
        {
            CollisionLayer = 2;
            CollisionMask = 1 | 4 | 8;
            FloorMaxAngle = Mathf.DegToRad(55);

            var shape = new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = 0.34f, Height = 1.72f },
                Position = new Vector3(0, 0.9f, 0)
            };
            AddChild(shape);

            Visual = new Node3D { Name = "Astronaut", Rotation = new Vector3(0, Mathf.Pi, 0) };
            AddChild(Visual);

            _arm = new SpringArm3D
            {
                Name = "CameraArm",
                SpringLength = 3.9f,
                Margin = 0.25f,
                CollisionMask = 1 | 4 | 8,
                Position = new Vector3(0, 1.45f, 0),
            };
            _arm.AddToGroup("camera_arm");
            AddChild(_arm);

            Camera = new Camera3D { Current = true, Fov = 70, Near = 0.05f };
            _arm.AddChild(Camera);

            _suitLight = new SpotLight3D
            {
                LightColor = new Color(0.85f, 0.93f, 1f),
                LightEnergy = 1.1f,
                SpotRange = 16f,
                SpotAngle = 30f,
                ShadowEnabled = true,
                Position = Vector3.Zero,
                Visible = false,
            };
            _arm.AddChild(_suitLight);

            // The arm points backwards along +Z; start behind the player looking north.
            _arm.Rotation = new Vector3(_pitch, _yaw, 0);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (InputLocked) return;
            if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                _yaw -= mm.Relative.X * 0.0028f;
                _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.0022f, -1.15f, 0.55f);
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            if (e.IsActionPressed("toggle_view")) ToggleView();
            if (e.IsActionPressed("interact") && _focus != null)
                Main.Instance?.DoInteract(_focus);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (InputLocked)
            {
                Velocity = Vector3.Zero;
                MoveAndSlide();
                return;
            }

            Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            Vector3 dir = new Vector3(input.X, 0, input.Y).Rotated(Vector3.Up, _yaw);
            float speed = Input.IsActionPressed("sprint") ? RunSpeed : WalkSpeed;

            Vector3 v = Velocity;
            v.X = dir.X * speed;
            v.Z = dir.Z * speed;
            if (!IsOnFloor()) v.Y -= Gravity * dt;
            else if (v.Y < 0) v.Y = -0.2f;

            if (IsOnFloor() && Input.IsActionPressed("jump"))
                v.Y = JumpSpeed;

            Velocity = v;
            MoveAndSlide();

            if (dir.LengthSquared() > 0.01f)
            {
                float target = Mathf.Atan2(dir.X, dir.Z);
                Visual.Rotation = new Vector3(0, Mathf.LerpAngle(Visual.Rotation.Y, target, dt * 10f), 0);
            }

            UpdateFocus();
            UpdateBob(dt);
        }

        private void UpdateBob(float dt)
        {
            if (!ThirdPerson)
            {
                bool moving = new Vector2(Velocity.X, Velocity.Z).Length() > 0.5f && IsOnFloor();
                _bob = moving ? _bob + dt * (Input.IsActionPressed("sprint") ? 11f : 7f) : _bob * 0.9f;
                Camera.Position = new Vector3(0, 0.03f * Mathf.Sin(_bob), 0);
            }
            else Camera.Position = Vector3.Zero;
        }

        private void UpdateFocus()
        {
            _focus = null;
            float best = 2.9f * 2.9f;
            foreach (var node in GetTree().GetNodesInGroup("interactable"))
            {
                if (node is not Interactable it || !it.IsInsideTree()) continue;
                float d = GlobalPosition.DistanceSquaredTo(it.GlobalPosition);
                if (d < best) { best = d; _focus = it; }
            }
            Main.Instance?.Hud.SetPrompt(_focus != null ? _focus.Prompt : "");
        }

        public void ToggleView()
        {
            ThirdPerson = !ThirdPerson;
            Visual.Visible = ThirdPerson;
            _suitLight.Visible = !ThirdPerson;
            if (ThirdPerson)
            {
                _arm.SpringLength = 3.9f;
                _arm.Position = new Vector3(0, 1.45f, 0);
                Camera.Fov = 70;
            }
            else
            {
                _arm.SpringLength = 0.05f;
                _arm.Position = new Vector3(0, 1.58f, 0);
                Camera.Fov = 82;
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            Main.Instance?.Hud.ShowSubtitle(
                L.T(ThirdPerson ? "Third-person suit camera" : "Helmet view", ThirdPerson ? "كاميرا البدلة" : "منظور الخوذة"), 2.0);
        }

        /// <summary>Developer portrait framing used by the screenshot tool.</summary>
        public void PortraitCamera()
        {
            ThirdPerson = true;
            Visual.Visible = true;
            _suitLight.Visible = false;
            _arm.SpringLength = 2.5f;
            _arm.Position = new Vector3(0, 1.42f, 0);
            // Stand in front of the suit for the portrait, looking back at the visor.
            _arm.Rotation = new Vector3(-0.04f, Mathf.Pi, 0);
            Camera.Fov = 46;
        }

        public void CaptureMouse(bool captured)
        {
            Input.MouseMode = captured ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }

        public void ResetLook(float yaw = 0)
        {
            _yaw = yaw;
            _pitch = -0.12f;
            _arm.Rotation = new Vector3(_pitch, _yaw, 0);
        }
    }
}
