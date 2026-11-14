using System;
using Godot;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;
namespace LunaQuest.Godot
{
    /// <summary>
    /// NEXO, the colony robot and the commander's mission buddy. It hovers at shoulder height,
    /// keeps out of walls with a ray test, looks where the commander looks, blinks, and turns its
    /// eyes orange when the colony has a warning. It runs the greenhouse and the relay remotely
    /// (the core simulation owns those duties), and it can be talked to like any crew member.
    /// </summary>
    public partial class RobotCompanion : Node3D
    {
        public NpcProfile Profile = null!;
        public WorldBuilder World = null!;
        private Node3D _body = null!;
        private ShaderMaterial _face = null!;
        private OmniLight3D _glow = null!;
        private MeshInstance3D _thruster = null!;
        private Label3D _name = null!;
        private Label3D _activity = null!;
        private Vector3 _vel;
        private double _t;
        private double _blinkTimer = 2.5;
        private double _blinkT = -1;
        private double _speakTimer = 35;
        private float _alert;
        private const float FollowHeight = 1.75f;

        public override void _Ready()
        {
            Name = "NEXO";
            BuildModel();
            var talk = new Interactable
            {
                BuildingId = Profile.LocationId,
                InteractionId = "talk",
                NpcId = Profile.Id,
                PromptEn = "Talk to NEXO",
                PromptAr = "تحدث مع نيكسو",
                Kind = "talk",
                Radius = 0.38f,
                Position = new Vector3(0, 0, 0),
            };
            AddChild(talk);
            _name = MakeLabel(Profile.DisplayNameEn, 22, new Color(0.75f, 0.9f, 1f), 0.5f);
            _activity = MakeLabel("", 15, new Color(0.55f, 0.8f, 0.95f), 0.4f);
            var spawn = World.InsideWorld.TryGetValue("habitat", out var h) ? h : Vector3.Zero;
            GlobalPosition = spawn + new Vector3(1.2f, FollowHeight, 1.0f);
        }

        private void BuildModel()
        {
            _body = new Node3D { Name = "Body" };
            AddChild(_body);
            var white = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.96f, 0.98f), Roughness = 0.35f, Metallic = 0.05f, ClearcoatEnabled = true, Clearcoat = 0.6f };
            var blue = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.45f, 0.86f), Roughness = 0.4f, Metallic = 0.2f };
            var orange = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.55f, 0.15f), EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.1f), EmissionEnergyMultiplier = 1.6f };
            var glowMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.45f, 0.8f, 1f), EmissionEnabled = true, Emission = new Color(0.45f, 0.8f, 1f) };
            // Head-body: a slightly squashed sphere, like the cartoon.
            var shell = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.26f, Height = 0.46f, RadialSegments = 32, Rings = 16 }, MaterialOverride = white };
            _body.AddChild(shell);
            // Blue equator band.
            var band = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.245f, OuterRadius = 0.275f, Rings = 40, RingSegments = 10 }, MaterialOverride = blue, Position = new Vector3(0, -0.07f, 0) };
            _body.AddChild(band);
            // Screen face on the front (+Z): a flattened disc with the eye shader.
            _face = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/nexo_face.gdshader") };
            var face = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 32, Rings = 16 },
                MaterialOverride = _face,
                Position = new Vector3(0, 0.03f, 0.105f),
                Scale = new Vector3(0.95f, 0.72f, 0.55f),
            };
            _body.AddChild(face);
            // Ear pods.
            foreach (float side in new[] { -1f, 1f })
            {
                var pod = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 0.08f }, MaterialOverride = blue, Position = new Vector3(side * 0.26f, 0.02f, 0), Rotation = new Vector3(0, 0, Mathf.Pi / 2) };
                _body.AddChild(pod);
            }
            // Antenna with an orange tip.
            var stalk = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.012f, Height = 0.16f }, MaterialOverride = white, Position = new Vector3(0, 0.29f, 0) };
            _body.AddChild(stalk);
            var tip = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.025f, Height = 0.05f }, MaterialOverride = orange, Position = new Vector3(0, 0.38f, 0) };
            _body.AddChild(tip);
            // Hover thruster underneath and its glow.
            _thruster = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.05f, Height = 0.06f }, MaterialOverride = glowMat, Position = new Vector3(0, -0.23f, 0) };
            _body.AddChild(_thruster);
            _glow = new OmniLight3D { LightColor = new Color(0.45f, 0.78f, 1f), LightEnergy = 0.9f, OmniRange = 2.2f, Position = new Vector3(0, -0.35f, 0), ShadowEnabled = false };
            _body.AddChild(_glow);
            foreach (var n in _body.GetChildren())
                if (n is GeometryInstance3D g) g.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }

        private Label3D MakeLabel(string text, int size, Color c, float y)
        {
            var l = new Label3D
            {
                Text = text, FontSize = size, PixelSize = 0.0032f, Modulate = c, OutlineSize = 6,
                OutlineModulate = new Color(0, 0, 0, 0.9f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, y, 0), Font = L.LatinFont,
            };
            AddChild(l);
            return l;
        }

        public override void _PhysicsProcess(double delta)
        {
            var main = Main.Instance;
            if (main == null) return;
            float dt = (float)delta;
            _t += delta;
            var player = main.Player;
            // Shoulder position: to the commander's right and slightly behind, relative to the view yaw.
            var basis = Basis.FromEuler(new Vector3(0, player.Yaw, 0));
            Vector3 right = basis * Vector3.Right;
            Vector3 back = basis * Vector3.Back;
            Vector3 anchor = player.GlobalPosition + Vector3.Up * 1.45f;
            Vector3 desired = player.GlobalPosition + right * 1.05f + back * 0.55f + Vector3.Up * FollowHeight;
            if (!main.Started) desired = GlobalPosition;
            // Keep out of walls: clamp the target to the first thing between the commander and it.
            var space = GetWorld3D().DirectSpaceState;
            var q = PhysicsRayQueryParameters3D.Create(anchor, desired, 1 | 4);
            var hit = space.IntersectRay(q);
            if (hit.Count > 0)
            {
                Vector3 p = (Vector3)hit["position"];
                desired = p + (anchor - p).Normalized() * 0.35f;
            }
            // Critically damped spring: smooth, no overshoot, never lags far behind a sprint.
            const float omega = 5.5f;
            Vector3 x = GlobalPosition - desired;
            Vector3 acc = -omega * omega * x - 2f * omega * _vel;
            _vel += acc * dt;
            Vector3 next = GlobalPosition + _vel * dt;
            if (next.DistanceTo(desired) > 9f) { next = desired; _vel = Vector3.Zero; }
            GlobalPosition = next;
            // Look where the commander looks, banking a little into turns.
            float yaw = player.Yaw + Mathf.Pi;
            float bank = Mathf.Clamp(-_vel.Dot(right) * 0.12f, -0.35f, 0.35f);
            float pitch = Mathf.Clamp(_vel.Dot(-back) * 0.05f, -0.25f, 0.25f);
            var target = new Vector3(pitch, yaw, bank);
            _body.Rotation = new Vector3(
                Mathf.Lerp(_body.Rotation.X, target.X, dt * 6f),
                Mathf.LerpAngle(_body.Rotation.Y, target.Y, dt * 5f),
                Mathf.Lerp(_body.Rotation.Z, target.Z, dt * 6f));
            _body.Position = new Vector3(0, (float)Math.Sin(_t * 2.1) * 0.05f, 0);
        }

        public override void _Process(double delta)
        {
            var main = Main.Instance;
            if (main == null) return;
            // Blink every few seconds.
            _blinkTimer -= delta;
            if (_blinkTimer <= 0) { _blinkT = 0; _blinkTimer = 2.5 + GD.Randf() * 3.5; }
            float blink = 0;
            if (_blinkT >= 0)
            {
                _blinkT += delta;
                blink = (float)Math.Sin(Math.Min(_blinkT / 0.16, 1.0) * Math.PI);
                if (_blinkT > 0.16) _blinkT = -1;
            }
            var s = main.Sim.State;
            bool warn = s.O2Kg < 40 || s.BatteryKwh < 25 || s.WaterL < 250 || s.SolarParticleEvent || !s.EarthLinkUp;
            _alert = Mathf.Lerp(_alert, warn ? 1f : 0f, (float)delta * 3f);
            _face.SetShaderParameter("blink", blink);
            _face.SetShaderParameter("alert", _alert);
            _face.SetShaderParameter("mood", 1f - _alert * 0.85f);
            _glow.LightEnergy = 0.7f + (float)Math.Sin(_t * 7.0) * 0.15f;

            var st = main.Sim.Npcs.State(Profile.Id);
            _name.Text = L.Arabic ? Profile.DisplayNameAr : Profile.DisplayNameEn;
            _name.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
            _activity.Text = L.Arabic ? "عن بُعد: " + st.ActivityAr : "remote: " + st.ActivityEn;
            _activity.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
            bool firstPerson = !main.Player.ThirdPerson;
            _name.Visible = main.Started && !firstPerson;
            // Activity line only when the commander stops to look, so it never clutters a walk.
            _activity.Visible = main.Started && !firstPerson && main.Player.Velocity.Length() < 0.3f;

            if (!main.Started) return;
            _speakTimer -= delta;
            if (_speakTimer <= 0 && !main.Voice.IsPlaying)
            {
                _speakTimer = 55 + GD.Randf() * 40;
                main.Voice.Speak(Profile, false);
            }
        }
    }
}
