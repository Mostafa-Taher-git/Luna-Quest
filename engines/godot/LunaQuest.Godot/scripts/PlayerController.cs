using System;
using Godot;
using LunaQuest.Core.Domain;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Suit movement tuned to lunar gravity: proper acceleration, coyote time and a jump
    /// buffer so the low gravity feels deliberate instead of floaty. Third person by default
    /// so the astronaut is visible; V switches to the helmet view.
    /// </summary>
    public partial class PlayerController : CharacterBody3D
    {
        public const float WalkSpeed = 3.4f;
        public const float RunSpeed = 6.1f;
        public const float JumpSpeed = 3.0f;
        public const float Gravity = 1.62f;

        private const float GroundAcceleration = 24f;
        private const float GroundDeceleration = 30f;
        private const float AirAcceleration = 7f;
        private const float CoyoteSeconds = 0.14f;
        private const float JumpBufferSeconds = 0.16f;
        private const float InteractReach = 3.2f;

        public Node3D Visual = null!;
        public Camera3D Camera = null!;
        public bool ThirdPerson = true;
        public bool InputLocked = true;

        private SpringArm3D _arm = null!;
        private SpotLight3D _suitLight = null!;
        private float _yaw;
        private float _pitch = -0.12f;
        private float _bob;
        private float _coyote;
        private float _jumpBuffer;
        private Interactable? _focus;
        private AstronautAnimator? _animator;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        public override void _Ready()
        {
            CollisionLayer = 2;
            CollisionMask = 1 | 4 | 8;
            FloorMaxAngle = Mathf.DegToRad(55);
            FloorSnapLength = 0.35f;
            FloorStopOnSlope = false;
            SafeMargin = 0.04f;

            var shape = new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = 0.34f, Height = 1.75f },
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
                CollisionMask = 1 | 4,
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
                _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.0022f, -1.18f, 1.18f);
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            if (e.IsActionPressed("toggle_view")) ToggleView();
            if (e.IsActionPressed("interact") && _focus != null)
                Main.Instance?.DoInteract(_focus);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = Mathf.Min((float)delta, 0.05f);

            if (InputLocked)
            {
                Velocity = Vector3.Zero;
                MoveAndSlide();
                UpdateAnimation();
                UpdateFocus();
                return;
            }

            if (Input.IsActionJustPressed("jump")) _jumpBuffer = JumpBufferSeconds;
            else _jumpBuffer = Mathf.Max(0f, _jumpBuffer - dt);

            Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            Vector3 wish = new Vector3(input.X, 0, input.Y).Rotated(Vector3.Up, _yaw);
            if (wish.LengthSquared() > 1f) wish = wish.Normalized();

            bool sprinting = Input.IsActionPressed("sprint") && wish.LengthSquared() > 0.01f;
            float targetSpeed = sprinting ? RunSpeed : WalkSpeed;
            Vector3 targetVelocity = wish * targetSpeed;

            bool onFloor = IsOnFloor();
            if (onFloor) _coyote = CoyoteSeconds;
            else _coyote = Mathf.Max(0f, _coyote - dt);

            Vector3 v = Velocity;
            float accel = onFloor
                ? (wish.LengthSquared() > 0.01f ? GroundAcceleration : GroundDeceleration)
                : AirAcceleration;
            float blend = 1f - Mathf.Exp(-accel * dt);
            v.X = Mathf.Lerp(v.X, targetVelocity.X, blend);
            v.Z = Mathf.Lerp(v.Z, targetVelocity.Z, blend);

            if (onFloor)
            {
                // Keep the capsule glued to the ground instead of hovering at the apex of
                // every slope. The jump below overrides this when it fires.
                if (v.Y < 0f) v.Y = -2.0f;
            }
            else
            {
                v.Y -= Gravity * dt;
            }

            if (_jumpBuffer > 0f && _coyote > 0f)
            {
                v.Y = JumpSpeed;
                _jumpBuffer = 0f;
                _coyote = 0f;
            }

            Velocity = v;
            MoveAndSlide();

            if (wish.LengthSquared() > 0.01f)
            {
                float target = Mathf.Atan2(wish.X, wish.Z);
                Visual.Rotation = new Vector3(0, Mathf.LerpAngle(Visual.Rotation.Y, target, dt * 12f), 0);
            }

            UpdateFocus();
            UpdateBob(dt, sprinting);
            UpdateAnimation();
            UpdateFov(dt, sprinting && wish.LengthSquared() > 0.01f);
        }

        private void UpdateAnimation()
        {
            if (_animator == null)
            {
                foreach (var child in Visual.GetChildren())
                {
                    _animator = child.GetNodeOrNull<AstronautAnimator>("Animator");
                    if (_animator != null) break;
                }
            }
            if (_animator == null) return;

            float speed = new Vector2(Velocity.X, Velocity.Z).Length();
            _animator.SetLocomotion(speed > 0.35f && IsOnFloor(), speed);
        }

        private void UpdateBob(float dt, bool sprinting)
        {
            if (ThirdPerson)
            {
                _bob = 0f;
                Camera.HOffset = 0f;
                Camera.VOffset = 0f;
                return;
            }

            float speed = new Vector2(Velocity.X, Velocity.Z).Length();
            bool moving = speed > 0.5f && IsOnFloor();
            if (moving) _bob += dt * (sprinting ? 11f : 7.5f);
            else _bob = Mathf.Lerp(_bob, 0f, 1f - Mathf.Exp(-8f * dt));

            float amount = moving ? 1f : 0.15f;
            Camera.HOffset = Mathf.Cos(_bob) * 0.009f * amount;
            Camera.VOffset = Mathf.Sin(_bob * 2f) * 0.012f * amount;
        }

        private void UpdateFov(float dt, bool sprinting)
        {
            float target = ThirdPerson ? 70f : 78f;
            if (sprinting) target += 4f;
            Camera.Fov = Mathf.Lerp(Camera.Fov, target, 1f - Mathf.Exp(-8f * dt));
        }

        /// <summary>
        /// Focus comes from the centre of the camera, not from a distance sphere. That means the
        /// player has to face a console and have a clear line to it, so stations cannot be used
        /// through walls. The reach is still measured from the suit, not from the camera.
        /// </summary>
        private void UpdateFocus()
        {
            _focus = null;
            var space = GetWorld3D().DirectSpaceState;
            Vector3 from = Camera.GlobalPosition;
            Vector3 direction = -Camera.GlobalTransform.Basis.Z;
            Vector3 to = from + direction * 14f;

            var query = PhysicsRayQueryParameters3D.Create(from, to, 1 | 16);
            query.CollideWithAreas = true;
            query.Exclude = new global::Godot.Collections.Array<Rid> { GetRid() };
            var hit = space.IntersectRay(query);

            if (hit.Count > 0 && hit["collider"].AsGodotObject() is Interactable station)
            {
                if (GlobalPosition.DistanceTo(station.GlobalPosition) <= InteractReach)
                    _focus = station;
            }

            Main.Instance?.Hud.SetPrompt(_focus != null ? _focus.Prompt : "");
        }

        public void ToggleView() => SetThirdPerson(!ThirdPerson, true);

        public void SetThirdPerson(bool thirdPerson, bool announce)
        {
            ThirdPerson = thirdPerson;
            Visual.Visible = ThirdPerson;
            _suitLight.Visible = !ThirdPerson;
            _bob = 0f;
            Camera.HOffset = 0f;
            Camera.VOffset = 0f;
            if (ThirdPerson)
            {
                _arm.SpringLength = 3.9f;
                _arm.Position = new Vector3(0, 1.45f, 0);
                Camera.Fov = 70;
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            else
            {
                _arm.SpringLength = 0.05f;
                _arm.Position = new Vector3(0, 1.58f, 0);
                Camera.Fov = 78;
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            Main.Instance?.Hud.SetFirstPerson(!ThirdPerson);
            if (announce)
            {
                Main.Instance?.Hud.ShowSubtitle(
                    L.T(ThirdPerson ? "Third-person suit camera" : "Helmet view", ThirdPerson ? "كاميرا البدلة" : "منظور الخوذة"), 2.0);
            }
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

        public void ResetLook(float yaw = 0, float pitch = -0.12f)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -1.18f, 1.18f);
            _arm.Rotation = new Vector3(_pitch, _yaw, 0);
        }
    }
}
