using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;

namespace LunaQuest.Godot
{
    /// <summary>
    /// A crew member who follows the schedule in npcs.json whether or not the player watches.
    /// Routes come from the navigation mesh baked from the real colliders, so crew use doorways
    /// and walk around props. Until the bake finishes (or if a goal is off the mesh) the old
    /// door-to-plaza route is the fallback. Each crew member has a solid capsule so the
    /// commander cannot walk through them, and they wait instead of shoving the commander.
    /// </summary>
    public partial class NpcAgent : Node3D
    {
        public NpcProfile Profile = null!;
        public WorldBuilder World = null!;

        private const float WalkSpeed = 1.55f;
        private Node3D _visual = null!;

        /// <summary>Public so the playtest harness can measure the suit.</summary>
        public Node3D Visual => _visual;
        private AstronautAnimator? _animator;
        private Label3D _name = null!;
        private Label3D _activity = null!;
        private AnimatableBody3D _solid = null!;
        private readonly List<Vector3> _path = new List<Vector3>();
        private int _pathIndex;
        private string _lastLocation = "";
        private string _currentLocation = "";
        private string _pendingLocation = "";
        private bool _usingNav;
        private bool _yielding;
        private double _scheduleTimer;
        private double _speakTimer;
        private double _stuckTimer;
        private Vector3 _lastProgressPos;
        private int _slot;

        public override void _Ready()
        {
            _slot = World.Npcs.Count;
            _visual = World.CreateAstronaut(1.75f);
            AddChild(_visual);
            _animator = _visual.GetNodeOrNull<AstronautAnimator>("Animator");

            _currentLocation = Profile.LocationId;
            GlobalPosition = SlotIn(_currentLocation);

            _name = MakeLabel(40, new Color(0.88f, 0.94f, 1f), 2.25f, 6);
            _activity = MakeLabel(26, new Color(0.6f, 0.82f, 0.9f), 1.95f, 5);

            var talk = new Interactable
            {
                BuildingId = Profile.LocationId,
                InteractionId = "talk",
                NpcId = Profile.Id,
                PromptEn = "Talk to " + Profile.DisplayNameEn,
                PromptAr = "تحدث مع " + Profile.DisplayNameAr,
                Kind = "talk",
                Position = new Vector3(0, 1.0f, 0.9f),
            };
            AddChild(talk);

            _solid = new AnimatableBody3D { Name = "Body", CollisionLayer = 8, CollisionMask = 0, SyncToPhysics = false };
            _solid.AddChild(new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.7f },
                Position = new Vector3(0, 0.88f, 0),
            });
            AddChild(_solid);

            _speakTimer = 8.0 + _slot * 2.5;
            _scheduleTimer = 0;
            _lastProgressPos = GlobalPosition;
            ApplySchedule(true);
        }

        private Label3D MakeLabel(int size, Color c, float y, int outline)
        {
            var l = new Label3D
            {
                Text = "",
                Font = L.Arabic ? L.ArabicFont : L.LatinFont,
                FontSize = size,
                PixelSize = 0.004f,
                Modulate = c,
                OutlineSize = outline,
                OutlineModulate = new Color(0, 0, 0, 0.9f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, y, 0),
            };
            AddChild(l);
            return l;
        }

        private Vector3 SlotIn(string location)
        {
            var home = World.InsideWorld.TryGetValue(location, out var p) ? p : Vector3.Zero;
            return home + new Vector3((_slot % 3 - 1f) * 0.9f, 0, (_slot / 3 - 0.5f) * 1.2f);
        }

        public override void _Process(double delta)
        {
            if (Main.Instance?.Started != true) return;
            _scheduleTimer -= delta;
            _speakTimer -= delta;
            if (_scheduleTimer <= 0)
            {
                _scheduleTimer = 2.0;
                ApplySchedule(false);
                // The navigation mesh bakes after start-up: re-plan the first walk on it.
                if (_pendingLocation != "" && World.NavReady)
                {
                    string loc = _pendingLocation;
                    _pendingLocation = "";
                    BuildPath(loc);
                }
            }
            bool walking = _pathIndex < _path.Count && !_yielding;
            _animator?.SetLocomotion(walking, WalkSpeed);
            UpdateLabels();
            MaybeSpeak(delta);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Main.Instance?.Started != true) return;
            FollowPath((float)delta);
        }

        private void ApplySchedule(bool immediate)
        {
            var sim = Main.Instance?.Sim;
            if (sim == null) return;
            var entry = Profile.CurrentEntry(sim.Clock.HourOfDay);
            if (entry == null) return;

            if (entry.LocationId == _lastLocation && !immediate) return;
            _lastLocation = entry.LocationId;

            // Already standing in the scheduled room: do not walk out and back in.
            if (World.InsideWorld.TryGetValue(entry.LocationId, out var target) &&
                GlobalPosition.DistanceTo(target) < 2.5f)
            {
                _path.Clear();
                _pathIndex = 0;
                _currentLocation = entry.LocationId;
                return;
            }
            BuildPath(entry.LocationId);
        }

        private void BuildPath(string location)
        {
            _path.Clear();
            _pathIndex = 0;
            _usingNav = false;
            _stuckTimer = 0;
            Vector3 goal = SlotIn(location);

            var nav = World.NavPath(GlobalPosition, goal);
            if (nav != null)
            {
                _path.AddRange(nav);
                _pathIndex = 1;
                _usingNav = true;
                _currentLocation = location;
                return;
            }
            if (!World.NavReady) _pendingLocation = location;

            // Fallback: leave the current interior, cross the plaza, enter the destination.
            string current = _currentLocation;
            if (World.InsideWorld.TryGetValue(current, out var inside)) _path.Add(inside);
            if (World.DoorWorld.TryGetValue(current, out var door)) _path.Add(door);
            _path.Add(new Vector3(0, 0.2f, 0));
            if (World.DoorWorld.TryGetValue(location, out var targetDoor)) _path.Add(targetDoor);
            _path.Add(goal);
            if (_path.Count > 1 && GlobalPosition.DistanceTo(_path[0]) < 1.0f) _pathIndex = 1;
            _currentLocation = location;
        }

        private void FollowPath(float delta)
        {
            _yielding = false;
            if (_pathIndex >= _path.Count) return;
            var target = _path[_pathIndex];
            Vector3 flat = new Vector3(target.X, GlobalPosition.Y, target.Z);
            if (GlobalPosition.DistanceTo(flat) < (_usingNav ? 0.25f : 0.5f))
            {
                _pathIndex++;
                return;
            }
            var dir = (flat - GlobalPosition).Normalized();

            // Give way: if the commander stands right in front, wait instead of shoving.
            var player = Main.Instance!.Player.GlobalPosition;
            Vector3 toPlayer = new Vector3(player.X - GlobalPosition.X, 0, player.Z - GlobalPosition.Z);
            if (toPlayer.Length() < 1.15f && toPlayer.Normalized().Dot(dir) > 0.45f)
            {
                _yielding = true;
                FaceTowards(toPlayer.Normalized(), delta);
                return;
            }

            Vector3 step = dir * WalkSpeed * delta;
            // Follow the height of the path (module floors, ramps, terrain) smoothly.
            float y = _usingNav
                ? Mathf.Lerp(GlobalPosition.Y, target.Y, Mathf.Clamp(delta * 8f, 0f, 1f))
                : GlobalPosition.Y;
            GlobalPosition = new Vector3(GlobalPosition.X + step.X, y, GlobalPosition.Z + step.Z);
            FaceTowards(dir, delta);

            // Stuck guard: if no progress for 6 s (e.g. door shut), snap to the next waypoint.
            _stuckTimer += delta;
            if (_stuckTimer > 1.0f)
            {
                if (GlobalPosition.DistanceTo(_lastProgressPos) < 0.3f && _pathIndex < _path.Count)
                {
                    GlobalPosition = _path[_pathIndex];
                    _pathIndex++;
                }
                _lastProgressPos = GlobalPosition;
                _stuckTimer = 0;
            }
        }

        private void FaceTowards(Vector3 dir, float delta)
        {
            float yaw = Mathf.Atan2(dir.X, dir.Z);
            _visual.Rotation = new Vector3(0, Mathf.LerpAngle(_visual.Rotation.Y, yaw, delta * 6f), 0);
        }

        private void UpdateLabels()
        {
            var sim = Main.Instance?.Sim;
            if (sim == null) return;
            var st = sim.Npcs.State(Profile.Id);
            _name.Text = L.Arabic ? Profile.DisplayNameAr : Profile.DisplayNameEn;
            _activity.Text = L.Arabic ? st.ActivityAr : st.ActivityEn;
            _name.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
            _activity.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
            float d = GlobalPosition.DistanceTo(Main.Instance!.Player.GlobalPosition);
            _activity.Visible = d < 12f;
            _name.Visible = d < 24f;
        }

        private void MaybeSpeak(double delta)
        {
            if (_speakTimer > 0) return;
            var main = Main.Instance;
            if (main == null) return;
            if (GlobalPosition.DistanceTo(main.Player.GlobalPosition) > 6f) return;
            _speakTimer = 26.0 + GD.Randf() * 18.0;
            main.Voice.Speak(Profile, false);
        }
    }
}
