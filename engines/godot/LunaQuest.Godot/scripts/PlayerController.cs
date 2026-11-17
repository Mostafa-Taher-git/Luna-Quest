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

        /// <summary>F0 contract: W moves toward the way the camera looks. Static for regression tests.</summary>
        public static Vector3 CameraRelativeDirection(Vector2 input, float yaw)
        {
            Vector3 wish = new Vector3(input.X, 0f, input.Y).Rotated(Vector3.Up, yaw);
            return wish.LengthSquared() > 1f ? wish.Normalized() : wish;
        }
        public bool ThirdPerson = true;

        /// <summary>Settings-controlled turn rate multiplier (default 1.0).</summary>
        public float MouseSensitivity = 1.0f;
        /// <summary>Settings-controlled field of view (default 70).</summary>
        public float BaseFov = 70f;
        public bool InputLocked = true;

        private SpringArm3D _arm = null!;
        private SpotLight3D _suitLight = null!;
        /// <summary>F8: builds the footstep puff and the landing thud. Called once at _Ready.</summary>
        private void BuildFeel()
        {
            var footMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.60f, 0.56f, 0.50f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            _footPuff = new GpuParticles3D
            {
                Name = "FootDust",
                Position = new Vector3(0, 0.06f, 0),
                OneShot = true,
                Amount = 6,
                Lifetime = 1.0f,
                Explosiveness = 0.8f,
                Emitting = false,
                ProcessMaterial = new ParticleProcessMaterial
                {
                    Direction = new Vector3(0, 0.75f, 0),
                    Spread = 75f,
                    Gravity = new Vector3(0, -0.65f, 0),
                    InitialVelocityMin = 0.25f,
                    InitialVelocityMax = 1.0f,
                    ScaleMin = 0.07f,
                    ScaleMax = 0.24f,
                },
                MaterialOverride = footMat,
                DrawPasses = 1,
                DrawPass1 = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 6, Rings = 3 },
            };
            AddChild(_footPuff);

            const int sampleRate = 16000;
            int frames = sampleRate * 35 / 100;
            var data = new byte[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)sampleRate;
                float v = Mathf.Sin(t * Mathf.Tau * 48f) * Mathf.Exp(-t * 14f) * 0.9f;
                short s = (short)(v * 12000f);
                data[i * 2] = (byte)(s & 0xFF);
                data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            _thud = new AudioStreamPlayer3D
            {
                Name = "LandThud",
                MaxDistance = 18f,
                Stream = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = sampleRate,
                    Stereo = false,
                    Data = data,
                },
            };
            AddChild(_thud);
        }
        private float _yaw;
        private float _pitch = -0.12f;
        private float _bob;
        private float _coyote;
        private float _jumpBuffer;
        private Interactable? _focus;
        private float _stepTimer, _fallVel;
        private bool _wasAirborne;
        private GpuParticles3D? _footPuff;
        private AudioStreamPlayer3D? _thud;
        private AstronautAnimator? _animator;
        public RoverController? Rover;

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
            BuildFeel();

            // The arm points backwards along +Z; start behind the player looking north.
            _arm.Rotation = new Vector3(_pitch, _yaw, 0);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (InputLocked) return;
            if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                _yaw -= mm.Relative.X * 0.0028f * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.0022f * MouseSensitivity, -1.18f, 1.18f);
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            if (e.IsActionPressed("toggle_view")) ToggleView();
            if (e.IsActionPressed("interact"))
            {
                if (_focus != null) Main.Instance?.DoInteract(_focus);
                else Main.Instance?.TryRoverInteraction();
            }
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

            if (Rover != null)
            {
                // F0: the chase camera follows the vehicle heading every frame so W drives
                // toward the view.
                Rover.Drive(dt, this);
                SyncCameraToRover();
                return;
            }

            if (Input.IsActionJustPressed("jump")) _jumpBuffer = JumpBufferSeconds;
            else _jumpBuffer = Mathf.Max(0f, _jumpBuffer - dt);

            Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            Vector3 wish = CameraRelativeDirection(input, _yaw);

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

            // F8: above 30 degrees of slope the suit starts to slide, not stop dead.
            float slopeDeg = Mathf.RadToDeg(GetFloorAngle());
            if (onFloor && slopeDeg > 30f && slopeDeg < 90f)
            {
                Vector3 n = GetFloorNormal();
                Vector3 downhill = new Vector3(-n.X, 0f, -n.Z).Normalized();
                float excess = Mathf.Clamp((slopeDeg - 30f) / 60f, 0f, 0.6f);
                v = Velocity;
                v.X += downhill.X * excess * 6.5f;
                v.Z += downhill.Z * excess * 6.5f;
                Velocity = v;
            }

            // F8: footstep whisper, landing thump.
            if (IsOnFloor())
            {
                float horizontal = new Vector2(Velocity.X, Velocity.Z).Length();
                if (horizontal > 1f)
                {
                    _stepTimer += dt * (sprinting ? 8f : 5f);
                    if (_stepTimer >= 1f)
                    {
                        _stepTimer = 0f;
                        if (_footPuff != null) { _footPuff.Emitting = false; _footPuff.Restart(); }
                    }
                }
                if (_wasAirborne && _fallVel < -2.4f)
                {
                    _thud?.Play();
                    if (_footPuff != null) { _footPuff.Emitting = false; _footPuff.Restart(); }
                }
                _wasAirborne = false;
            }
            else
            {
                _wasAirborne = true;
                _fallVel = Velocity.Y;
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
            float target = BaseFov + (ThirdPerson ? 0f : 8f);
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

            string prompt = "";
            if (_focus != null) prompt = _focus.Prompt;
            else if (Rover != null && Rover.Occupied)
                prompt = L.T($"Step out (E) · {Rover.SpeedKmh():F0} km/h · battery {Rover.BatteryPercent() * 100f:F0}%",
                    $"اخرج (E) · {Rover.SpeedKmh():F0} كم/س · البطارية {Rover.BatteryPercent() * 100f:F0}%");
            else if (Rover != null && GlobalPosition.DistanceTo(Rover.GlobalPosition) < 4.2f)
                prompt = L.T("Drive the crew rover (E)", "قد مركبة الطاقم (E)");
            Main.Instance?.Hud.SetPrompt(prompt);
        }

        public void ConfigureDriveCamera()
        {
            _arm.SpringLength = ThirdPerson ? 7.5f : 0.05f;
            _arm.Position = new Vector3(0, ThirdPerson ? 1.5f : 1.2f, 0);
        }

        /// <summary>
        /// The .NET player body is on collision layer 2 and the rover's mask is layer 1, so the
        /// driven suit never fights the rover's shape. This only makes the intent explicit and
        /// stops the suit's own movement while it is carried.
        /// </summary>
        public void SetDriving(bool on)
        {
            if (on) Velocity = Vector3.Zero;
        }

        public void RestoreWalkCamera()
        {
            _arm.SpringLength = ThirdPerson ? 3.9f : 0.05f;
            _arm.Position = new Vector3(0, ThirdPerson ? 1.45f : 1.58f, 0);
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
                Camera.Fov = BaseFov;
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            else
            {
                _arm.SpringLength = 0.05f;
                _arm.Position = new Vector3(0, 1.58f, 0);
                Camera.Fov = BaseFov + 8f;
                _arm.Rotation = new Vector3(_pitch, _yaw, 0);
            }
            if (Rover != null) ConfigureDriveCamera();
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

        /// <summary>F0 contract: the chase camera follows the vehicle heading every frame.</summary>
        public void SyncCameraToRover()
        {
            if (Rover == null) return;
            _yaw = Rover.Rotation.Y;
            _arm.Rotation = new Vector3(_pitch, _yaw, 0);
        }
    }
}
