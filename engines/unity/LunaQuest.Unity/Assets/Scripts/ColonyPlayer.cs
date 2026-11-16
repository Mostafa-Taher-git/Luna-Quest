using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// Lunar suit movement: 1.62 m/s² gravity, long low-gravity strides, a third-person
    /// camera by default and a helmet view on V. Movement is camera-relative, and the
    /// astronaut turns to face the direction of travel.
    /// </summary>
    public sealed class ColonyPlayer : MonoBehaviour
    {
        public const float WalkSpeed = 3.4f;
        public const float RunSpeed = 6.2f;
        public const float JumpSpeed = 3.1f;
        public const float Gravity = 1.62f;

        public LunaQuestGame Game;
        public Transform Visual;
        public Camera Camera;
        public bool ThirdPerson = true;

        public Station FocusStation { get; private set; }
        public CrewAgent FocusCrew { get; private set; }

        private CharacterController _controller;
        private Transform _pivot;
        private Light _suitLight;
        private float _yaw = 180f;
        private float _pitch = -8f;
        private float _vertical;
        private float _coyote;
        private float _jumpBuffer;
        private float _bob;
        private Vector3 _horizontal;
        public CrewRover Rover;

        private const float GroundAcceleration = 24f;
        private const float GroundDeceleration = 30f;
        private const float AirAcceleration = 7f;
        private const float CoyoteSeconds = 0.14f;
        private const float JumpBufferSeconds = 0.16f;
        private const float InteractReach = 3.2f;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        public string FocusPrompt
        {
            get
            {
                if (Rover != null && Rover.Occupied)
                    return L.T($"Step out (E) · {Rover.SpeedKmh():F0} km/h · battery {Rover.BatteryPercent() * 100f:F0}%",
                        $"اخرج (E) · {Rover.SpeedKmh():F0} كم/س · البطارية {Rover.BatteryPercent() * 100f:F0}%");
                if (Rover != null && (Rover.transform.position - transform.position).sqrMagnitude < 4.2f * 4.2f)
                    return L.T("Drive the crew rover (E)", "قد مركبة الطاقم (E)");
                if (FocusCrew != null)
                    return L.T($"Talk to {FocusCrew.DisplayName}", $"تحدّث مع {FocusCrew.DisplayName}");
                if (FocusStation != null)
                    return FocusStation.Prompt;
                return "";
            }
        }

        /// <summary>True while the suit's CharacterController is live (false at the wheel).</summary>
        public bool ControllerEnabled => _controller != null && _controller.enabled;

        public bool NearIncident =>
            Game != null && Game.Sim.Incidents.Active.Exists(a => !a.Resolved && !a.Ignored);

        /// <summary>F0 contract: W moves toward the way the camera looks. Static for regression tests.</summary>
        public static Vector3 CameraRelativeDirection(Vector2 input, float yawDegrees)
        {
            Vector3 dir = Quaternion.Euler(0f, yawDegrees, 0f) * new Vector3(input.x, 0f, input.y);
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        public static ColonyPlayer Create(LunaQuestGame game)
        {
            var go = new GameObject("Commander");
            go.layer = 2; // Ignore Raycast, so the camera does not collide with the player
            var player = go.AddComponent<ColonyPlayer>();
            player.Game = game;

            player._controller = go.AddComponent<CharacterController>();
            player._controller.radius = 0.34f;
            player._controller.height = 1.75f;
            player._controller.center = new Vector3(0f, 0.875f, 0f);
            player._controller.slopeLimit = 55f;
            player._controller.stepOffset = 0.38f;
            player._controller.skinWidth = 0.04f;

            var visualGo = game.World.CreateAstronaut(1.75f, new Color(0.95f, 0.55f, 0.15f), true);
            player.Visual = visualGo.transform;
            player.Visual.SetParent(go.transform, false);
            player.Visual.localPosition = Vector3.zero;
            player.Visual.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(go.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            pivot.transform.localRotation = Quaternion.Euler(player._pitch, player._yaw, 0f);
            player._pivot = pivot.transform;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            player.Camera = camGo.AddComponent<Camera>();
            player.Camera.clearFlags = CameraClearFlags.Skybox;
            player.Camera.nearClipPlane = 0.05f;
            player.Camera.farClipPlane = 1500f;
            player.Camera.fieldOfView = 70f;
            player.Camera.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -3.9f);
            camGo.transform.localRotation = Quaternion.identity;

            var lightGo = new GameObject("SuitLamp");
            lightGo.transform.SetParent(camGo.transform, false);
            player._suitLight = lightGo.AddComponent<Light>();
            player._suitLight.type = LightType.Spot;
            player._suitLight.range = 18f;
            player._suitLight.spotAngle = 36f;
            player._suitLight.intensity = 1.1f;
            player._suitLight.color = new Color(0.82f, 0.92f, 1f);
            player._suitLight.shadows = LightShadows.None;
            player._suitLight.enabled = false;
            lightGo.transform.localPosition = new Vector3(0f, -0.05f, 0.05f);

            return player;
        }

        /// <summary>
        /// Disables the suit's own CharacterController while the commander is at the wheel.
        /// The rover carries the player's transform onto the seat, so a live suit capsule
        /// inside the rover's own capsule would fight it every frame (jitter and push-out).
        /// Teleport() re-enables the controller, so Exit calls this BEFORE the teleport — then
        /// the final state is "controller enabled", exactly what walking needs.
        /// </summary>
        public void SetDriving(bool on)
        {
            if (_controller != null) _controller.enabled = !on;
        }

        public void Teleport(Vector3 position)
        {
            if (_controller == null) return;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }

        private void Update()
        {
            if (Game == null || Game.Hud == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (Game.InputActive && Cursor.lockState == CursorLockMode.Locked)
            {
                _yaw += Input.GetAxis("Mouse X") * 2.1f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 2.1f, -72f, 72f);
            }

            if (Input.GetKeyDown(KeyCode.V) && Game.InputActive)
                ToggleView();

            _pivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (Rover != null)
            {
                // Dialogue and menus do not stop time, so the wheel itself has to be gated:
                // without this the rover keeps driving behind an open dialogue box.
                if (Game.InputActive)
                {
                    // The chase camera follows the vehicle heading every frame so W drives
                    // toward the view (F0 contract).
                    SyncCameraToRover();
                    Rover.Drive(dt, this);
                }
                return;
            }

            Vector2 moveInput = Vector2.zero;
            bool sprinting = false;
            if (Game.InputActive)
            {
                if (Input.GetKey(KeyCode.W)) moveInput.y += 1f;
                if (Input.GetKey(KeyCode.S)) moveInput.y -= 1f;
                if (Input.GetKey(KeyCode.D)) moveInput.x += 1f;
                if (Input.GetKey(KeyCode.A)) moveInput.x -= 1f;
                sprinting = Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > 0.01f;
                if (Input.GetKeyDown(KeyCode.Space)) _jumpBuffer = JumpBufferSeconds;
            }

            if (_jumpBuffer > 0f) _jumpBuffer = Mathf.Max(0f, _jumpBuffer - dt);

            Vector3 dir = CameraRelativeDirection(moveInput, _yaw);
            float speed = sprinting ? RunSpeed : WalkSpeed;
            Vector3 target = dir * speed;

            bool grounded = _controller.isGrounded;
            if (grounded) _coyote = CoyoteSeconds;
            else _coyote = Mathf.Max(0f, _coyote - dt);

            float accel = grounded
                ? (input.sqrMagnitude > 0.01f ? GroundAcceleration : GroundDeceleration)
                : AirAcceleration;
            _horizontal = Vector3.Lerp(_horizontal, target, 1f - Mathf.Exp(-accel * dt));

            if (grounded && _vertical < 0f) _vertical = -2.0f;
            else if (!grounded) _vertical -= Gravity * dt;

            if (_jumpBuffer > 0f && _coyote > 0f)
            {
                _vertical = JumpSpeed;
                _jumpBuffer = 0f;
                _coyote = 0f;
            }

            Vector3 velocity = new Vector3(_horizontal.x, _vertical, _horizontal.z);
            _controller.Move(velocity * dt);

            if (dir.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Visual.rotation = Quaternion.Slerp(Visual.rotation,
                    Quaternion.Euler(0f, targetYaw, 0f), dt * 12f);
            }

            float targetFov = ThirdPerson ? 70f : 78f;
            if (sprinting) targetFov += 4f;
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, targetFov, 1f - Mathf.Exp(-8f * dt));

            UpdateCameraCollision(dt, sprinting);
            UpdateFocus();
        }

        private void UpdateCameraCollision(float dt, bool sprinting)
        {
            if (!ThirdPerson)
            {
                float speed = new Vector2(_horizontal.x, _horizontal.z).magnitude;
                bool moving = speed > 0.5f && _controller.isGrounded;
                if (moving) _bob += dt * (sprinting ? 11f : 7.5f);
                else _bob = Mathf.Lerp(_bob, 0f, 1f - Mathf.Exp(-8f * dt));

                float amount = moving ? 1f : 0.15f;
                Camera.transform.localPosition = new Vector3(
                    Mathf.Cos(_bob) * 0.009f * amount,
                    Mathf.Sin(_bob * 2f) * 0.012f * amount,
                    -0.05f);
                return;
            }

            _bob = 0f;
            const float wanted = 3.9f;
            float distance = wanted;
            if (Physics.SphereCast(_pivot.position, 0.22f, -_pivot.forward, out RaycastHit hit,
                    wanted, ~(1 << 2), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(1.15f, hit.distance - 0.18f);

            Camera.transform.localPosition = new Vector3(0f, 0f, -distance);
        }

        /// <summary>
        /// Focus comes from the centre of the camera so stations cannot be used through walls.
        /// The distance is measured from the suit, keeping both cameras equally usable.
        /// </summary>
        private void UpdateFocus()
        {
            FocusStation = null;
            FocusCrew = null;
            if (Rover != null) return;
            if (!Game.Started || Game.World == null) return;

            Ray ray = Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, 60f, ~(1 << 2), QueryTriggerInteraction.Collide))
                return;

            var station = hit.collider.GetComponentInParent<Station>();
            if (station != null)
            {
                if ((station.transform.position - transform.position).sqrMagnitude <= InteractReach * InteractReach)
                    FocusStation = station;
                return;
            }

            var crew = hit.collider.GetComponentInParent<CrewAgent>();
            if (crew != null && (crew.transform.position - transform.position).sqrMagnitude <= 2.9f * 2.9f)
                FocusCrew = crew;
        }

        public void Halt() { _horizontal = Vector3.zero; _vertical = 0f; }

        public void ConfigureDriveCamera()
        {
            _pivot.localPosition = new Vector3(0f, ThirdPerson ? 1.5f : 1.2f, 0f);
            Camera.transform.localPosition = new Vector3(0f, 0f, ThirdPerson ? -7.5f : -0.05f);
        }

        public void SyncCameraToRover()
        {
            if (Rover == null) return;
            _yaw = Rover.transform.eulerAngles.y;
            _pivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        public void RestoreWalkCamera()
        {
            _pivot.localPosition = new Vector3(0f, ThirdPerson ? 1.45f : 1.58f, 0f);
            Camera.transform.localPosition = new Vector3(0f, 0f, ThirdPerson ? -3.9f : -0.05f);
        }

        public void SetThirdPerson(bool thirdPerson, bool announce)
        {
            ThirdPerson = thirdPerson;
            Visual.gameObject.SetActive(ThirdPerson);
            _suitLight.enabled = !ThirdPerson;
            _pivot.localPosition = new Vector3(0f, ThirdPerson ? 1.45f : 1.58f, 0f);
            Camera.fieldOfView = ThirdPerson ? 70f : 78f;
            Camera.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            _bob = 0f;
            if (Rover != null) ConfigureDriveCamera();
            Game.Hud?.SetFirstPerson(!ThirdPerson);
            if (announce)
            {
                Game.Hud.ShowSubtitle(
                    L.T(ThirdPerson ? "Third-person suit camera" : "Helmet view",
                        ThirdPerson ? "كاميرا البدلة" : "منظور الخوذة"), 2f);
            }
        }

        public void ToggleView() => SetThirdPerson(!ThirdPerson, true);

        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -72f, 72f);
            _pivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
