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
        private bool _jumpQueued;

        public string FocusPrompt
        {
            get
            {
                if (FocusCrew != null)
                    return L.T($"Talk to {FocusCrew.DisplayName}", $"تحدّث مع {FocusCrew.DisplayName}");
                if (FocusStation != null)
                    return FocusStation.Prompt;
                return "";
            }
        }

        public bool NearIncident =>
            Game != null && Game.Sim.Incidents.Active.Exists(a => !a.Resolved && !a.Ignored);

        public static ColonyPlayer Create(LunaQuestGame game)
        {
            var go = new GameObject("Commander");
            go.layer = 2; // Ignore Raycast, so the camera does not collide with the player
            var player = go.AddComponent<ColonyPlayer>();
            player.Game = game;

            player._controller = go.AddComponent<CharacterController>();
            player._controller.radius = 0.34f;
            player._controller.height = 1.72f;
            player._controller.center = new Vector3(0f, 0.88f, 0f);
            player._controller.slopeLimit = 55f;
            player._controller.stepOffset = 0.38f;
            player._controller.skinWidth = 0.04f;

            var visualGo = game.World.CreateAstronaut(1.78f, new Color(0.95f, 0.55f, 0.15f), true);
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

            if (Game.InputActive && Cursor.lockState == CursorLockMode.Locked)
            {
                _yaw += Input.GetAxis("Mouse X") * 2.1f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 2.1f, -72f, 72f);
            }

            if (Input.GetKeyDown(KeyCode.V) && Game.InputActive)
                ToggleView();

            _pivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 input = Vector3.zero;
            if (Game.InputActive)
            {
                if (Input.GetKey(KeyCode.W)) input.z += 1f;
                if (Input.GetKey(KeyCode.S)) input.z -= 1f;
                if (Input.GetKey(KeyCode.D)) input.x += 1f;
                if (Input.GetKey(KeyCode.A)) input.x -= 1f;
                if (Input.GetKeyDown(KeyCode.Space)) _jumpQueued = true;
            }

            Vector3 dir = Quaternion.Euler(0f, _yaw, 0f) * input.normalized;
            float speed = Input.GetKey(KeyCode.LeftShift) ? RunSpeed : WalkSpeed;

            Vector3 velocity = new Vector3(dir.x * speed, _vertical, dir.z * speed);

            if (_controller.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -1.0f;
                if (_jumpQueued)
                {
                    velocity.y = JumpSpeed;
                    _jumpQueued = false;
                }
            }
            else
            {
                velocity.y -= Gravity * Time.deltaTime;
            }
            _vertical = velocity.y;

            _controller.Move(velocity * Time.deltaTime);

            if (dir.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Visual.rotation = Quaternion.Slerp(Visual.rotation,
                    Quaternion.Euler(0f, targetYaw, 0f), Time.deltaTime * 10f);
            }

            UpdateCameraCollision();
            UpdateFocus();
        }

        private void UpdateCameraCollision()
        {
            if (!ThirdPerson)
            {
                Camera.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                return;
            }

            const float wanted = 3.9f;
            float distance = wanted;
            if (Physics.SphereCast(_pivot.position, 0.22f, -_pivot.forward, out RaycastHit hit,
                    wanted, ~(1 << 2), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(1.15f, hit.distance - 0.18f);

            Camera.transform.localPosition = new Vector3(0f, 0f, -distance);
        }

        private void UpdateFocus()
        {
            FocusStation = null;
            FocusCrew = null;
            if (!Game.Started || Game.World == null) return;

            float best = 3.0f * 3.0f;
            foreach (var station in Game.World.Stations)
            {
                if (station == null) continue;
                float d = (station.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; FocusStation = station; }
            }

            float bestCrew = 2.7f * 2.7f;
            foreach (var crew in Game.World.Crew)
            {
                if (crew == null) continue;
                float d = (crew.transform.position - transform.position).sqrMagnitude;
                if (d < bestCrew)
                {
                    bestCrew = d;
                    FocusCrew = crew;
                    FocusStation = null;
                }
            }
        }

        public void ToggleView()
        {
            ThirdPerson = !ThirdPerson;
            Visual.gameObject.SetActive(ThirdPerson);
            _suitLight.enabled = !ThirdPerson;
            _pivot.localPosition = new Vector3(0f, ThirdPerson ? 1.45f : 1.58f, 0f);
            Camera.fieldOfView = ThirdPerson ? 70f : 78f;
            Game.Hud.ShowSubtitle(
                L.T(ThirdPerson ? "Third-person suit camera" : "Helmet view",
                    ThirdPerson ? "كاميرا البدلة" : "منظور الخوذة"), 2f);
        }
    }
}
