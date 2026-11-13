using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;

namespace LunaQuest.Godot
{
    /// <summary>
    /// A crew member who follows the schedule in npcs.json whether or not the player watches.
    /// Movement is a simple door-to-plaza route; the meaningful decisions live in the core.
    /// </summary>
    public partial class NpcAgent : Node3D
    {
        public NpcProfile Profile = null!;
        public WorldBuilder World = null!;

        private Node3D _visual = null!;
        private Label3D _name = null!;
        private Label3D _activity = null!;
        private readonly List<Vector3> _path = new List<Vector3>();
        private int _pathIndex;
        private string _lastLocation = "";
        private double _scheduleTimer;
        private double _speakTimer;
        private int _slot;

        public override void _Ready()
        {
            _slot = World.Npcs.Count;
            _visual = World.CreateAstronaut(1.72f);
            AddChild(_visual);

            var home = World.InsideWorld.TryGetValue(Profile.LocationId, out var p) ? p : Vector3.Zero;
            GlobalPosition = home + new Vector3((_slot % 4 - 1.5f) * 0.9f, 0, (_slot / 4 - 0.5f) * 1.2f);

            _name = new Label3D
            {
                Text = L.Arabic ? Profile.DisplayNameAr : Profile.DisplayNameEn,
                Font = L.Arabic ? L.ArabicFont : L.LatinFont,
                FontSize = 40,
                PixelSize = 0.004f,
                Modulate = new Color(0.88f, 0.94f, 1f),
                OutlineSize = 6,
                OutlineModulate = new Color(0, 0, 0, 0.9f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, 2.25f, 0),
            };
            AddChild(_name);

            _activity = new Label3D
            {
                Text = "",
                Font = L.Arabic ? L.ArabicFont : L.LatinFont,
                FontSize = 26,
                PixelSize = 0.004f,
                Modulate = new Color(0.6f, 0.82f, 0.9f),
                OutlineSize = 5,
                OutlineModulate = new Color(0, 0, 0, 0.9f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, 1.95f, 0),
            };
            AddChild(_activity);

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

            _speakTimer = 8.0 + _slot * 2.5;
            _scheduleTimer = 0;
            ApplySchedule(true);
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
            }
            FollowPath((float)delta);
            UpdateLabels();
            MaybeSpeak(delta);
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
                return;
            }
            BuildPath(entry.LocationId);
        }

        private void BuildPath(string location)
        {
            _path.Clear();
            _pathIndex = 0;
            string current = Profile.LocationId;
            // Leave the current interior first, cross the plaza, then enter the destination.
            if (World.InsideWorld.TryGetValue(current, out var inside)) _path.Add(inside);
            if (World.DoorWorld.TryGetValue(current, out var door)) _path.Add(door);
            _path.Add(new Vector3(0, 0.2f, 0));
            if (World.DoorWorld.TryGetValue(location, out var targetDoor)) _path.Add(targetDoor);
            if (World.InsideWorld.TryGetValue(location, out var targetInside))
                _path.Add(targetInside + new Vector3((_slot % 4 - 1.5f) * 0.9f, 0, (_slot / 4 - 0.5f) * 1.2f));

            // If the NPC is already very close to the first waypoint, skip it.
            if (_path.Count > 1 && GlobalPosition.DistanceTo(_path[0]) < 1.0f) _pathIndex = 1;
        }

        private void FollowPath(float delta)
        {
            if (_pathIndex >= _path.Count) return;
            var target = _path[_pathIndex];
            Vector3 flat = new Vector3(target.X, GlobalPosition.Y, target.Z);
            if (GlobalPosition.DistanceTo(flat) < 0.5f)
            {
                _pathIndex++;
                return;
            }
            var dir = (flat - GlobalPosition).Normalized();
            GlobalPosition += dir * 1.55f * delta;
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
            bool near = GlobalPosition.DistanceTo(Main.Instance!.Player.GlobalPosition) < 12f;
            _activity.Visible = near;
            _name.Visible = near || GlobalPosition.DistanceTo(Main.Instance!.Player.GlobalPosition) < 24f;
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
