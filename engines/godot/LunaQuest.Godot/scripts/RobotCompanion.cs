using System;
using System.Collections.Generic;
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

        private Node3D? _pointer;
        private OmniLight3D? _thrusterGlow;

        private static T? FindChild<T>(Node root) where T : Node
        {
            if (root is T match) return match;
            foreach (Node c in root.GetChildren())
            {
                var nested = FindChild<T>(c);
                if (nested != null) return nested;
            }
            return null;
        }

        private void BuildModel()
        {
            _body = new Node3D { Name = "Body" };
            AddChild(_body);
            // The CC0 Quaternius Animated LowPoly Robot body (nexo_robot.glb, ~1.05 m once
            // scaled); the procedural v2 body is the fresh-clone fallback.
            const string glbPath = "res://assets/models/nexo_robot/nexo_robot.glb";
            if (ResourceLoader.Exists(glbPath) && GD.Load<PackedScene>(glbPath) is { } scene)
            {
                var model = scene.Instantiate<Node3D>();
                var raw = MeasureRawBounds(model);
                if (raw.Size.Y > 0.001f)
                {
                    float s = 1.05f / raw.Size.Y;
                    model.Scale = new Vector3(s, s, s);
                    _body.AddChild(model);
                    var fitted = MeasureRawBounds(model);
                    model.Position = new Vector3(0, -fitted.Position.Y, 0);
                }
                else
                {
                    _body.AddChild(model);
                }
                var anim = FindChild<AnimationPlayer>(model);
                if (anim != null)
                {
                    string[] names = anim.GetAnimationList();
                    string want = "";
                    foreach (string n in names)
                        if (n.ToLowerInvariant().Contains("idle")) { want = n; break; }
                    if (want != "")
                    {
                        var clip = anim.GetAnimation(want);
                        if (clip != null)
                        {
                            clip.LoopMode = Animation.LoopModeEnum.Linear;
                            anim.Play(want);
                        }
                    }
                }
                BuildGLBFace();
                BuildNexoExtras();
                return;
            }
            BuildPrimitiveBody();
            BuildNexoExtras();
        }

        /// <summary>A visor with two eyes afloat in front of the low-poly face — the warning
        /// pair that turns orange in incidents. Parented to the body, not the skeleton.</summary>
        private void BuildGLBFace()
        {
            _face = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/nexo_face.gdshader") };
            var visor = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.115f, Height = 0.23f, RadialSegments = 24, Rings = 12 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.02f, 0.025f, 0.035f),
                    Roughness = 0.08f,
                    Metallic = 0.4f,
                },
                Scale = new Vector3(1.0f, 0.60f, 0.30f),
                Position = new Vector3(0, 0.86f, 0.335f),
            };
            _body.AddChild(visor);
            var eyes = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(0.11f, 0.05f) },
                MaterialOverride = _face,
                Position = new Vector3(0, 0.90f, 0.372f),
            };
            _body.AddChild(eyes);
        }

        /// <summary>Bounds helper that includes skinned meshes — only safe on this one asset.</summary>
        private static Aabb MeasureRawBounds(Node3D node)
        {
            Aabb? aabb = null;
            void Walk(Node3D n3, Transform3D xform)
            {
                var next = xform * n3.Transform;
                if (n3 is MeshInstance3D mi && mi.Mesh != null)
                {
                    var local = next * mi.Mesh.GetAabb();
                    aabb = aabb == null ? local : aabb.Value.Merge(local);
                }
                foreach (Node child in n3.GetChildren())
                    if (child is Node3D c3) Walk(c3, next);
            }
            Walk(node, Transform3D.Identity);
            return aabb ?? new Aabb(Vector3.Zero, Vector3.One);
        }

        /// <summary>The upgraded procedural body — the fresh-clone fallback, full hardware:
        /// panel seams, curved dark visor, cyan accent ring, antenna with orange tip, solar fins,
        /// hover skirt. NEXO is a robot, never a human suit.</summary>
        private void BuildPrimitiveBody()
        {
            var white = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.90f, 0.92f), Roughness = 0.3f, Metallic = 0.15f };
            var darkTrim = new StandardMaterial3D { AlbedoColor = new Color(0.28f, 0.30f, 0.33f), Roughness = 0.55f };
            var visorMat = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.025f, 0.035f), Roughness = 0.08f, Metallic = 0.4f, EmissionEnabled = true, Emission = new Color(0.03f, 0.04f, 0.06f), EmissionEnergyMultiplier = 0.4f };
            var cyan = new StandardMaterial3D { AlbedoColor = new Color(0.10f, 0.55f, 0.70f), EmissionEnabled = true, Emission = new Color(0.25f, 0.85f, 1.0f), EmissionEnergyMultiplier = 1.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            var eye = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.30f, 0.38f), EmissionEnabled = true, Emission = new Color(0.30f, 0.85f, 1.0f), EmissionEnergyMultiplier = 2.2f };
            var fin = new StandardMaterial3D { AlbedoColor = new Color(0.10f, 0.16f, 0.30f), Metallic = 0.55f, Roughness = 0.25f, EmissionEnabled = true, Emission = new Color(0.07f, 0.16f, 0.38f), EmissionEnergyMultiplier = 0.5f };

            var shell = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.42f, Height = 0.84f, RadialSegments = 32, Rings = 16 }, MaterialOverride = white, Position = new Vector3(0, 1.12f, 0) };
            _body.AddChild(shell);
            foreach (float frac in new[] { -0.18f, 0.18f })
            {
                var seam = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.422f, BottomRadius = 0.422f, Height = 0.018f, RadialSegments = 32 }, MaterialOverride = darkTrim, Position = new Vector3(0, 1.12f + frac, 0) };
                _body.AddChild(seam);
            }
            var visor = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.34f, Height = 0.55f, RadialSegments = 28, Rings = 12 }, MaterialOverride = visorMat, Scale = new Vector3(1.0f, 0.62f, 0.32f), Position = new Vector3(0, 1.14f, 0.30f) };
            _body.AddChild(visor);
            var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.445f, Rings = 36, RingSegments = 10 }, MaterialOverride = cyan, Position = new Vector3(0, 1.02f, 0), Rotation = new Vector3(Mathf.Pi / 2f, 0, 0) };
            _body.AddChild(ring);
            foreach (float side in new[] { -1f, 1f })
            {
                var eyeNode = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 12, Rings = 8 }, MaterialOverride = eye, Position = new Vector3(side * 0.13f, 1.18f, 0.42f) };
                _body.AddChild(eyeNode);
                var solar = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.22f, 0.34f) }, MaterialOverride = fin, Position = new Vector3(side * 0.50f, 1.36f, 0), Rotation = new Vector3(0, 0, side * 0.22f) };
                _body.AddChild(solar);
            }
            var stalk = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.018f, Height = 0.24f, RadialSegments = 8 }, MaterialOverride = white, Position = new Vector3(0, 1.68f, 0) };
            _body.AddChild(stalk);
            var tip = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.032f, Height = 0.064f, RadialSegments = 10, Rings = 6 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.45f, 0.08f), EmissionEnabled = true, Emission = new Color(1.0f, 0.6f, 0.2f), EmissionEnergyMultiplier = 1.8f }, Position = new Vector3(0, 1.82f, 0) };
            _body.AddChild(tip);
            var skirt = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.12f, Height = 0.42f, RadialSegments = 20 }, MaterialOverride = white, Position = new Vector3(0, 0.72f, 0) };
            _body.AddChild(skirt);
        }

        /// <summary>Hover thruster glow + objective pointer — always present, GLB or primitive.</summary>
        private void BuildNexoExtras()
        {
            _thrusterGlow = new OmniLight3D
            {
                Name = "ThrusterGlow",
                LightColor = new Color(0.35f, 0.78f, 0.91f),
                LightEnergy = 1.6f,
                OmniRange = 2.0f,
                Position = new Vector3(0, 0.16f, 0),
                ShadowEnabled = false,
            };
            _body.AddChild(_thrusterGlow);

            _pointer = new Node3D { Name = "ObjectivePointer", Position = new Vector3(0, 0.28f, 0) };
            var penMat = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.55f, 0.62f), EmissionEnabled = true, Emission = new Color(0.25f, 0.85f, 1.0f), EmissionEnergyMultiplier = 1.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            var tip = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.065f, Height = 0.19f, RadialSegments = 8 },
                MaterialOverride = penMat,
                Position = new Vector3(0, 0.1f, 0f),
                Rotation = new Vector3(Mathf.Pi * 0.4f, 0f, 0f),
            };
            _pointer.AddChild(tip);
            foreach (float side in new[] { -1f, 1f })
            {
                var blade = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.11f, 0.04f, 0.05f) },
                    MaterialOverride = penMat,
                    Position = new Vector3(side * 0.115f, 0.16f, 0.07f),
                    Rotation = new Vector3(0, side * 0.72f, 0),
                };
                _pointer.AddChild(blade);
            }
            _body.AddChild(_pointer);
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
            // F10 — the polite distance: trail 3.2 m behind-and-beside, never crowd the commander.
            Vector3 desired = player.GlobalPosition + right * 1.3f + back * 2.0f + Vector3.Up * FollowHeight;
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
            // F10: objective pointer — yaws toward the first unfinished objective's building.
            if (_pointer != null && main?.Sim != null)
            {
                var objectiveTarget = NextObjectiveBuilding(main);
                if (objectiveTarget != default)
                {
                    float worldYaw = Mathf.Atan2(objectiveTarget.X - GlobalPosition.X, objectiveTarget.Z - GlobalPosition.Z);
                    float rel = worldYaw - Mathf.Tau * Mathf.Round((worldYaw - _body.Rotation.Y) / Mathf.Tau);
                    rel = Mathf.LerpAngle(_pointer.Rotation.Y, rel, dt * 6f);
                    _pointer.Rotation = new Vector3(0, rel, 0);
                }
            }

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

        private static readonly Dictionary<string, string> ObjectiveTargets = new()
        {
            ["obj_solar_dust"] = "power_farm",
            ["obj_sensor_dust"] = "comms_tower",
            ["obj_airlock_dust"] = "airlock",
            ["obj_water_filter"] = "life_support",
            ["obj_air_scrubber"] = "life_support",
            ["obj_plant_care"] = "greenhouse",
            ["obj_radiation"] = "storm_shelter",
            ["obj_comms_relay"] = "comms_tower",
            ["obj_crew_rest"] = "habitat",
            ["obj_sample"] = "ice_site",
            ["obj_power_budget"] = "power_farm",
            ["obj_rover_take"] = "garage",
            ["obj_rover_ice"] = "ice_site",
        };

        /// <summary>The building the next unfinished objective points at.</summary>
        private static Vector3 NextObjectiveBuilding(LunaQuest.Godot.Main main)
        {
            var sim = main.Sim;
            var content = sim.Content;
            foreach (var objective in content.Learning)
            {
                if (sim.Learning.HasEntry(objective.Id)) continue;
                var targetId = ObjectiveTargets.GetValueOrDefault(objective.Id, "habitat");
                var building = content.Building(targetId);
                if (building == null) continue;
                return new Vector3(building.PositionX, 0f, building.PositionZ);
            }
            var mc = content.Building("mission_control");
            return mc != null ? new Vector3(mc.PositionX, 0f, mc.PositionZ) : Vector3.Zero;
        }

        public override void _Process(double delta)
        {
            var main = Main.Instance;
            if (main == null || _name == null) return;
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
            if (_face != null)
            {
                _face.SetShaderParameter("blink", blink);
                _face.SetShaderParameter("alert", _alert);
                _face.SetShaderParameter("mood", 1f - _alert * 0.85f);
            }
            // The thruster pulses orange during incidents regardless of body path.
            if (_thrusterGlow != null)
            {
                if (_alert > 0.12f)
                {
                    _thrusterGlow.LightColor = new Color(1.0f, 0.55f + 0.25f * Mathf.Abs(Mathf.Sin((float)_t * 7.0f)), 0.15f);
                    _thrusterGlow.LightEnergy = 1.1f + 0.8f * Mathf.Abs(Mathf.Sin((float)_t * 7.0f));
                }
                else
                {
                    _thrusterGlow.LightColor = new Color(0.35f, 0.78f, 0.91f);
                    _thrusterGlow.LightEnergy = 1.25f + 0.2f * Mathf.Abs(Mathf.Sin((float)_t * 2.4f));
                }
            }
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
