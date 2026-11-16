using System;
using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Sim;
using LunaQuest.Core.Terrain;

namespace LunaQuest.Godot
{
    /// <summary>
    /// Builds the whole colony procedurally from content JSON. A building is not a facade:
    /// the walls are real, the door is a gap you can walk through, and every interaction point
    /// is a physical console inside a named room.
    /// </summary>
    public partial class WorldBuilder : Node3D
    {
        public readonly Dictionary<string, Vector3> DoorWorld = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, Vector3> InsideWorld = new Dictionary<string, Vector3>();
        public readonly List<NpcAgent> Npcs = new List<NpcAgent>();
        public RobotCompanion? Robot { get; private set; }

        private Main _main = null!;
        private ColonySimulation _sim = null!;
        private DirectionalLight3D _sun = null!;
        private MeshInstance3D _sunDisc = null!;
        private MeshInstance3D _earth = null!;
        private StandardMaterial3D? _starMat;
        private StandardMaterial3D _panelMat = null!;
        private Label3D _greenhouseLive = null!;
        private Label3D _habitatLive = null!;
        private OmniLight3D _beacon = null!;
        private Node3D _npcRoot = null!;
        private Node3D _sky = null!;
        public TerrainStreamer Streamer = null!;
        public LunarTerrain Terrain = null!;
        private double _t;
        private double _liveTimer;
        private readonly Dictionary<string, StandardMaterial3D> _mats = new Dictionary<string, StandardMaterial3D>();
        private readonly List<(Label3D label, string en, string ar)> _bilingual = new List<(Label3D, string, string)>();

        public void Build(Main main, ColonySimulation sim)
        {
            _main = main;
            _sim = sim;
            Name = "Colony";
            _sky = new Node3D { Name = "Sky" };
            AddChild(_sky);
            BuildEnvironment();
            BuildTerrain();
            BuildStars();
            BuildEarth();
            BuildLandingPad();
            BuildPlaza();

            foreach (var b in sim.Content.Buildings) BuildBuilding(b);

            BuildSolarFarm(sim.Content.Building("power_farm"));
            BuildCommsMast(sim.Content.Building("comms_tower"));
            BuildRover(sim.Content.Building("garage"));
            BuildKitDressing();
            BuildNavigation();
            SpawnNpcs(sim);
        }

        // ---- environment -----------------------------------------------------

        private void BuildEnvironment()
        {
            var env = new global::Godot.Environment
            {
                BackgroundMode = global::Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0f, 0f, 0f),
                // There is no atmosphere on the Moon, so there is no sky bounce: the only
                // ambient light is earthshine, and it is weak. The old flat 0.82 energy filled
                // every shadow and made the whole colony read as one washed-out white box.
                AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.30f, 0.40f, 0.62f),
                AmbientLightEnergy = 0.17f,
                TonemapMode = global::Godot.Environment.ToneMapper.Filmic,
                TonemapExposure = 1.05f,
                TonemapWhite = 6.0f,
                GlowEnabled = true,
                GlowIntensity = 0.28f,
                GlowBloom = 0.12f,
            };
            var we = new WorldEnvironment { Environment = env, Name = "SpaceEnvironment" };
            AddChild(we);

            _sun = new DirectionalLight3D
            {
                Name = "LowSun",
                LightColor = new Color(1f, 0.97f, 0.93f),
                // Direct sunlight is the only strong light source out here, so it carries the
                // scene: brighter than the old value now that ambient no longer washes it out.
                LightEnergy = 2.6f,
                ShadowEnabled = true,
                // A grazing polar Sun is the worst case for shadow acne: tighter cascades,
                // more normal bias and a little blur keep the regolith clean.
                DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
                DirectionalShadowMaxDistance = 140f,
                DirectionalShadowSplit1 = 0.06f,
                DirectionalShadowSplit2 = 0.18f,
                DirectionalShadowSplit3 = 0.45f,
                DirectionalShadowBlendSplits = true,
                DirectionalShadowFadeStart = 0.8f,
                ShadowBias = 0.12f,
                ShadowNormalBias = 2.2f,
                ShadowBlur = 1.4f,
            };
            AddChild(_sun);
            UpdateSun(0);

            // A cold fill from the sky keeps the shadow side readable without pretending
            // there is an atmosphere.
            var fill = new DirectionalLight3D
            {
                Name = "EarthShine",
                LightColor = new Color(0.42f, 0.58f, 0.88f),
                LightEnergy = 0.22f,
                ShadowEnabled = false,
                RotationDegrees = new Vector3(-35, 145, 0),
            };
            AddChild(fill);

            // The sun itself, a small unshaded disc near the horizon. At the lunar south
            // pole it never rises high, so it is a constant low glare rather than a noon sun.
            Material sunMat = ResourceLoader.Exists("res://shaders/sun.gdshader")
                ? new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sun.gdshader") }
                : new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    AlbedoColor = new Color(1f, 0.95f, 0.85f),
                };
            _sunDisc = new MeshInstance3D
            {
                Name = "SunDisc",
                Mesh = new QuadMesh { Size = new Vector2(9f, 9f) },
                MaterialOverride = sunMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _sky.AddChild(_sunDisc);
        }

        /// <summary>
        /// The approach apron: a paved path from the player's spawn to the airlock, with
        /// low bollards. It gives the spawn a readable axis instead of an empty regolith plane.
        /// </summary>
        private void BuildPlaza()
        {
            var root = new Node3D { Name = "Plaza", Position = new Vector3(0, 0, 23) };
            AddChild(root);
            var pave = Mat(new Color(0.205f, 0.215f, 0.235f), 0.12f, 0.88f);
            var edge = Mat(new Color(0.44f, 0.46f, 0.49f), 0.2f, 0.62f);
            var dark = Mat(new Color(0.075f, 0.082f, 0.1f), 0.4f, 0.55f);
            var lamp = Mat(new Color(1f, 0.88f, 0.64f), 0f, 0.4f, 2.4f);

            Solid(root, new Vector3(0, 0.055f, 0), new Vector3(7.4f, 0.11f, 15f), pave);
            for (int i = -3; i <= 3; i++)
            {
                var line = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(7.2f, 0.012f, 0.05f) },
                    MaterialOverride = dark,
                    Position = new Vector3(0, 0.116f, i * 2.25f),
                };
                root.AddChild(line);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Solid(root, new Vector3(s * 3.8f, 0.10f, 0), new Vector3(0.22f, 0.2f, 15f), edge);
                for (int i = -2; i <= 2; i++)
                {
                    var post = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.09f, Height = 1.0f },
                        MaterialOverride = dark,
                        Position = new Vector3(s * 3.35f, 0.58f, i * 3.0f),
                    };
                    root.AddChild(post);
                    var cap = new MeshInstance3D
                    {
                        Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) },
                        MaterialOverride = lamp,
                        Position = new Vector3(s * 3.35f, 1.12f, i * 3.0f),
                    };
                    root.AddChild(cap);
                }
            }
            for (int s = -1; s <= 1; s += 2)
            {
                var glow = new OmniLight3D
                {
                    LightColor = new Color(1f, 0.84f, 0.58f), LightEnergy = 0.75f,
                    OmniRange = 6.0f, Position = new Vector3(s * 3.2f, 1.2f, 3.0f),
                };
                root.AddChild(glow);
            }
        }

        /// <summary>
        /// Distant crater rims. The Moon's horizon is never a clean line: it is a chain of
        /// low, soft ridges. These are deliberately far and unlit so they read as landscape,
        /// not as props around the base.
        /// </summary>
        private void BuildHorizon()
        {
            var a = Mat(new Color(0.245f, 0.24f, 0.235f), 0f, 1f);
            var b = Mat(new Color(0.19f, 0.187f, 0.182f), 0f, 1f);
            var ridges = new (float x, float z, float rx, float ry, float rz)[]
            {
                (-250, -210, 95f, 26f, 44f), (10, -300, 130f, 34f, 58f),
                (245, -200, 82f, 22f, 48f), (-330, -30, 70f, 24f, 66f),
                (330, 35, 92f, 25f, 76f), (-170, -320, 120f, 30f, 66f),
                (185, -330, 150f, 36f, 78f), (0, -375, 200f, 32f, 100f),
                (-300, 220, 100f, 22f, 60f), (280, 240, 110f, 26f, 70f),
            };
            for (int i = 0; i < ridges.Length; i++)
            {
                var r = ridges[i];
                var hill = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 28, Rings = 12 },
                    MaterialOverride = i % 2 == 0 ? a : b,
                    Position = new Vector3(r.x, -r.ry * 0.42f, r.z),
                    Scale = new Vector3(r.rx, r.ry, r.rz),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(hill);
            }
        }

        /// <summary>Trim around a doorway: two posts, a lintel and a warm threshold strip.</summary>
        private void BuildDoorFrame(Node3D root, float d, float h, float gap, Material trim, bool back)
        {
            float z = (back ? -1f : 1f) * (d * 0.5f + 0.20f);
            float doorTop = h - 1.0f;
            for (int i = -1; i <= 1; i += 2)
                Solid(root, new Vector3(i * (gap * 0.5f + 0.14f), doorTop * 0.5f, z),
                    new Vector3(0.24f, doorTop, 0.28f), trim);
            Solid(root, new Vector3(0, doorTop + 0.06f, z),
                new Vector3(gap + 0.6f, 0.22f, 0.28f), trim);

            var glow = Mat(new Color(0.98f, 0.86f, 0.64f), 0f, 0.4f, 1.5f);
            var strip = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(gap * 0.56f, 0.07f, 0.06f) },
                MaterialOverride = glow,
                Position = new Vector3(0, doorTop + 0.22f, z + (back ? -0.06f : 0.06f)),
            };
            root.AddChild(strip);
        }

        /// <summary>Vertical hull ribs and seams so a pressurized module is not a plain box.</summary>
        private void BuildFacadeRibs(Node3D root, float w, float d, float h, float gap, Material trim)
        {
            float front = d * 0.5f + 0.21f;
            int ribs = Mathf.Max(2, Mathf.RoundToInt(w / 2.2f));
            for (int i = 0; i <= ribs; i++)
            {
                float x = -w * 0.5f + 0.35f + i * (w - 0.7f) / ribs;
                if (Mathf.Abs(x) < gap * 0.5f + 0.5f) continue;
                Solid(root, new Vector3(x, (h - 1.35f) * 0.5f + 0.18f, front),
                    new Vector3(0.12f, h - 1.45f, 0.10f), trim, hideMesh: true);
            }
            float side = (w - gap) * 0.5f;
            if (side > 1.4f)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float cx = s * (gap * 0.5f + side * 0.5f);
                    Solid(root, new Vector3(cx, h * 0.55f, front), new Vector3(side * 0.86f, 0.07f, 0.10f), trim, hideMesh: true);
                    Solid(root, new Vector3(cx, h * 0.31f, front), new Vector3(side * 0.86f, 0.045f, 0.10f), trim, hideMesh: true);
                }
            }
            if (w > 8.5f)
            {
                var dark = Mat(new Color(0.15f, 0.16f, 0.18f), 0.45f, 0.5f);
                Solid(root, new Vector3(w * 0.31f, 0.78f, front + 0.1f), new Vector3(0.95f, 1.15f, 0.32f), dark);
                var pipe = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = h * 0.55f },
                    MaterialOverride = trim,
                    Position = new Vector3(-w * 0.315f, h * 0.31f, front + 0.13f),
                };
                root.AddChild(pipe);
            }
        }

        private void BuildTerrain()
        {
            // The same procedural ground is drawn by Unity; the colony sits on a flat pad that
            // fades into craters, highland swells and boulder fields in every direction.
            Terrain = new LunarTerrain(2026, 0, 8, 88, 90, 1.0);
            LoadRealRocks();
            Material groundMaterial;
            if (ResourceLoader.Exists("res://shaders/regolith.gdshader"))
            {
                var sm = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/regolith.gdshader") };
                // Photoscanned regolith detail (Poly Haven Moon Dusted 03, CC0).
                var alb = TryLoadTexture("res://assets/real/regolith/moon_dusted_03_albedo.jpg");
                var nrm = TryLoadTexture("res://assets/real/regolith/moon_dusted_03_normal.jpg");
                var rgh = TryLoadTexture("res://assets/real/regolith/moon_dusted_03_rough.jpg");
                if (alb != null && nrm != null)
                {
                    sm.SetShaderParameter("use_detail", true);
                    sm.SetShaderParameter("detail_albedo", alb);
                    sm.SetShaderParameter("detail_normal", nrm);
                    if (rgh != null) sm.SetShaderParameter("detail_rough", rgh);
                }
                groundMaterial = sm;
            }
            else
                groundMaterial = Mat(new Color(0.36f, 0.35f, 0.33f), 0f, 0.98f);
            Streamer = new TerrainStreamer
            {
                Name = "Regolith",
                Terrain = Terrain,
                GroundMaterial = groundMaterial,
                RockMaterial = Mat(new Color(0.30f, 0.29f, 0.28f), 0f, 0.98f),
                RockMesh = new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 7, Rings = 4 },
            };
            AddChild(Streamer);
            Streamer.Prime(new Vector3(0, 0, 23));
            // A safety floor far below the deepest crater, so a bug can never drop the player forever.
            var floor = new StaticBody3D { Name = "SafetyFloor", CollisionLayer = 1, CollisionMask = 0 };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(20000, 1, 20000) }, Position = new Vector3(0, -140, 0) });
            AddChild(floor);
        }

        private void BuildStars()
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = new QuadMesh { Size = new Vector2(0.30f, 0.30f) },
            };
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = Colors.White,
            };
            mm.Mesh.SurfaceSetMaterial(0, mat);
            _starMat = mat;

            const int stars = 1700;      // real points: small, magnitude-spread
            const int band = 500;        // a faint Milky Way band of haze points
            var rng = new Random(42);
            // The band is a rotated great circle: its poles are the sky's "milky way poles".
            var bandNormal = new Vector3(0.42f, 0.9f, 0.25f).Normalized();
            var bin1 = bandNormal.Cross(Vector3.Right);
            if (bin1.LengthSquared() < 1e-4f) bin1 = bandNormal.Cross(Vector3.Forward);
            bin1 = bin1.Normalized();
            var bin2 = bandNormal.Cross(bin1);

            mm.InstanceCount = stars + band;
            for (int i = 0; i < mm.InstanceCount; i++)
            {
                bool isBand = i >= stars;
                Vector3 dir;
                Color color;
                float size;
                if (isBand)
                {
                    double t = rng.NextDouble() * Math.Tau;
                    double spread = (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * 0.34;
                    dir = (bin1 * (float)Math.Cos(t) + bin2 * (float)Math.Sin(t) + bandNormal * (float)spread).Normalized();
                    if (dir.Y < 0.03f) dir = new Vector3(dir.X, 0.03f, dir.Z).Normalized();
                    size = 0.15f + (float)Math.Pow(rng.NextDouble(), 3.0) * 0.15f;
                    float b = 0.16f + (float)rng.NextDouble() * 0.18f;
                    color = new Color(b * 0.75f, b * 0.82f, b);   // dim blue haze
                }
                else
                {
                    // Cosine-weighted across the sky, above the horizon.
                    double az = rng.NextDouble() * Math.Tau;
                    double el = Math.Asin(0.02 + rng.NextDouble() * 0.95);
                    dir = new Vector3(
                        (float)(Math.Cos(el) * Math.Cos(az)),
                        (float)Math.Sin(el),
                        (float)(Math.Cos(el) * Math.Sin(az)));
                    // Magnitude distribution: many faint points, few bright ones.
                    float m = (float)Math.Pow(rng.NextDouble(), 2.6);
                    size = 0.14f + m * 0.46f;
                    float b = 0.35f + (float)Math.Pow(rng.NextDouble(), 2.2) * 0.75f;
                    float w = (float)rng.NextDouble();
                    if (w < 0.78f) color = new Color(b, b, b);                    // white
                    else if (w < 0.89f) color = new Color(b, b * 0.82f, b * 0.65f); // warm
                    else color = new Color(b * 0.78f, b * 0.88f, b);                // cool
                }
                var pos = dir * 1500f;
                var xf = new Transform3D(Basis.Identity, pos).Scaled(new Vector3(size, size, size));
                mm.SetInstanceTransform(i, xf);
                mm.SetInstanceColor(i, color);
            }
            var mmi = new MultiMeshInstance3D { Multimesh = mm, Name = "Stars" };
            mmi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _sky.AddChild(mmi);
        }

        private void BuildEarth()
        {
            var sphere = new SphereMesh { Radius = 6f, Height = 12f, RadialSegments = 64, Rings = 32 };
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = new Color(0.35f, 0.6f, 1f),
                EmissionEnergyMultiplier = 0.6f,
                AlbedoColor = new Color(0.25f, 0.48f, 0.9f),
            };
            Material earthMat = mat;
            // NASA Blue Marble (public domain) with a lit day side and atmospheric limb.
            var tex = TryLoadTexture("res://assets/real/earth/blue_marble_2048.jpg")
                   ?? TryLoadTexture("res://assets/textures/earth.jpg");
            if (tex != null && ResourceLoader.Exists("res://shaders/earth.gdshader"))
            {
                var em = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/earth.gdshader") };
                em.SetShaderParameter("surface", tex);
                // The initial value is replaced each frame in UpdateSun(); this is its seed.
                em.SetShaderParameter("sun_dir", new Vector3(0.75f, 0.25f, 0.6f));
                earthMat = em;
            }
            else if (tex != null)
            {
                // Fresh clone without the shader: still a lit rock in the sun, never a glowing ball.
                var lit = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                    AlbedoTexture = tex,
                    Roughness = 1.0f,
                };
                earthMat = lit;
            }
            _earth = new MeshInstance3D
            {
                Mesh = sphere,
                MaterialOverride = earthMat,
                Position = new Vector3(95, 118, -305),
                // Tilt so Africa and the Atlantic face the Moon, like the cartoon intro.
                Rotation = new Vector3(0.35f, -1.9f, 0.41f),
                Name = "Earth",
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _sky.AddChild(_earth);
        }

        private void BuildLandingPad()
        {
            var pad = new Node3D { Name = "LandingPad", Position = new Vector3(0, 0, 58) };
            AddChild(pad);
            var mat = Mat(new Color(0.3f, 0.31f, 0.33f), 0.2f, 0.8f);
            var disc = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 13, BottomRadius = 13.4f, Height = 0.22f }, MaterialOverride = mat };
            disc.Position = new Vector3(0, 0.11f, 0);
            pad.AddChild(disc);
            var ringMat = Mat(new Color(0.85f, 0.75f, 0.3f), 0.1f, 0.6f, 0.4f);
            var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 9.6f, OuterRadius = 10.2f, Rings = 64, RingSegments = 8 }, MaterialOverride = ringMat };
            ring.Scale = new Vector3(1, 0.06f, 1);
            ring.Position = new Vector3(0, 0.24f, 0);
            pad.AddChild(ring);

            // A small crew lander, gold foil and white ascent stage.
            // F6: the supply crate — a hold-E cargo delivery for the workshop spares desk.
            var crateMat = Mat(new Color(0.42f, 0.35f, 0.27f), 0.75f, 0.05f);
            var crate = new StaticBody3D { Position = new Vector3(6.8f, 0.62f, 3.0f), CollisionLayer = 1, CollisionMask = 0 };
            pad.AddChild(crate);
            var crateMesh = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.5f, 1.25f, 1.15f) }, MaterialOverride = crateMat };
            crate.AddChild(crateMesh);
            var crateStripe = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.52f, 0.18f, 1.17f) }, MaterialOverride = Mat(new Color(0.84f, 0.68f, 0.14f), 0.4f, 0.3f, 0.5f), Position = new Vector3(0, -0.18f, 0) };
            crate.AddChild(crateStripe);
            var crateCol = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.5f, 1.25f, 1.15f) } };
            crate.AddChild(crateCol);
            var crateInteract = new Interactable
            {
                BuildingId = "",
                InteractionId = "cargo_crate",
                PromptEn = "Supply Crate",
                PromptAr = "صندوق الإمداد",
                Kind = "job",
                Radius = 2.4f,
                Position = new Vector3(6.8f, 0.8f, 3.0f),
            };
            pad.AddChild(crateInteract);

            var foil = Mat(new Color(0.85f, 0.62f, 0.18f), 0.85f, 0.35f);
            var white = Mat(new Color(0.86f, 0.87f, 0.88f), 0.3f, 0.45f);
            var body = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 2.0f, BottomRadius = 2.6f, Height = 3.4f }, MaterialOverride = foil };
            body.Position = new Vector3(0, 2.1f, 12.5f);
            pad.AddChild(body);
            var cabin = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 2.0f, Height = 1.6f }, MaterialOverride = white };
            cabin.Position = new Vector3(0, 4.6f, 12.5f);
            pad.AddChild(cabin);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.Pi / 4 + i * Mathf.Pi / 2;
                var leg = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.1f, Height = 2.6f }, MaterialOverride = white };
                leg.Position = new Vector3(Mathf.Cos(a) * 2.2f, 1.2f, 12.5f + Mathf.Sin(a) * 2.2f);
                leg.Rotation = new Vector3(Mathf.Sin(a) * 0.35f, 0, Mathf.Cos(a) * -0.35f);
                pad.AddChild(leg);
            }
        }

        // ---- buildings -------------------------------------------------------

        private static readonly Dictionary<string, string> ShellAsset = new()
        {
            ["habitat"] = "rigid-cylinder-habitat-9b2fdbb4.glb",
            ["mission_control"] = "rigid-cylinder-habitat-2dec4448.glb",
            ["greenhouse"] = "greenhouse-module-b35be615.glb",
            ["workshop"] = "workshop-module-7729780d.glb",
            ["life_support"] = "oxygen-extraction-plan-002fb0ae.glb",
            ["lab"] = "medical-module-6d266b02.glb",
            ["medical"] = "medical-module-6d266b02.glb",
            ["garage"] = "inflatable-dome-habita-f86f5d7b.glb",
            ["storage"] = "battery-container-9755ed67.glb",
            ["comms_tower"] = "comms-relay-mast-4cdfe54f.glb",
            ["regolith_plant"] = "sintering-print-gantry-64cc192a.glb",
            ["storm_shelter"] = "inflatable-dome-habita-514822a4.glb",
            ["power_farm"] = "solar-array-row-e52454d9.glb",
            ["ice_site"] = "regolith-excavator-64b4560f.glb",
            ["airlock"] = "airlock-with-dust-lock-fe27e26c.glb",
        };
        private readonly List<Node3D> _wallMeshes = new();
        private readonly Dictionary<string, int> _attachedProps = new();

        public int ShellCount { get; private set; }
        public int PorchCount { get; private set; }

        /// <summary>F2: wrap the module in a CC0 kit shell scaled to its footprint, and hide the
        /// plain box wall meshes (their blockers stay put).</summary>
        private void BuildShell(Node3D root, BuildingDefinition b, float w, float d, float h)
        {
            // Hide the window strips and the foil blanket — the shell carries the exterior.
            foreach (Node mesh in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                var nm = mesh.Name.ToString();
                if (nm == "WallMesh" || nm == "CableTray") continue;
                if (nm.StartsWith("Window", StringComparison.Ordinal) || nm == "GoldBlanket")
                    ((MeshInstance3D)mesh).Visible = false;
            }
            string key = ShellAsset.TryGetValue(b.Id, out var k) ? k : "";
            if (key.Length == 0) return;
            string path = "res://assets/models/lunar_colony/" + key;
            if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path) is not { } scene) return;
            var node = scene.Instantiate<Node3D>();
            if (node == null) return;
            node.Name = "Shell_" + b.Id;
            var aabb = MeasureAabb(node);
            if (aabb.Size.LengthSquared() > 1e-6f)
            {
                float sx = Mathf.Clamp(w / Mathf.Max(aabb.Size.X, 1e-3f), 0.5f, 2.5f);
                float sy = Mathf.Clamp(h / Mathf.Max(aabb.Size.Y, 1e-3f), 0.5f, 2.5f);
                float sz = Mathf.Clamp(d / Mathf.Max(aabb.Size.Z, 1e-3f), 0.5f, 2.5f);
                node.Scale = new Vector3(sx, sy, sz);
                var min = new Vector3(aabb.Position.X * sx, aabb.Position.Y * sy, aabb.Position.Z * sz);
                var centre = min + new Vector3(aabb.Size.X * sx * 0.5f, 0f, aabb.Size.Z * sz * 0.5f);
                node.Position = new Vector3(-centre.X, -min.Y, -centre.Z);
            }
            root.AddChild(node);
            var ribMat = Mat(new Color(0.42f, 0.45f, 0.50f), 0.55f, 0.35f);
            foreach (float f in new[] { 0.34f, 0.67f })
            {
                var ring = new MeshInstance3D
                {
                    Name = "RibRing",
                    Mesh = new TorusMesh { InnerRadius = Mathf.Min(w, d) * 0.42f, OuterRadius = Mathf.Min(w, d) * 0.42f + 0.18f, Rings = 32, RingSegments = 8 },
                    MaterialOverride = ribMat,
                    Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                    Position = new Vector3(0, h * f, 0),
                };
                root.AddChild(ring);
            }
            var radiator = new MeshInstance3D
            {
                Name = "Radiator",
                Mesh = new BoxMesh { Size = new Vector3(w * 0.7f, 1.9f, 0.14f) },
                MaterialOverride = Mat(new Color(0.28f, 0.30f, 0.34f), 0.7f, 0.05f),
                Position = new Vector3(0, h * 0.52f, -d * 0.5f - 0.2f),
            };
            root.AddChild(radiator);
            ShellCount++;
        }

        private static Aabb MeasureAabb(Node3D node)
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

        /// <summary>An airlock-style porch at each door: gold MLI collar, gold foil side panels,
        /// a hazard-stripe sill and two handrails.</summary>
        private void BuildPorch(Node3D root, float w, float d, float h)
        {
            // The door faces the colony centre, so it is local +Z of the root by construction.
            var porch = new Node3D { Name = "Porch", Position = new Vector3(0, 0, d * 0.5f) };
            root.AddChild(porch);
            var goldGlow = Mat(new Color(0.98f, 0.70f, 0.22f), 0.85f, 0.3f, 0.9f);
            var goldMat = Mat(new Color(0.78f, 0.55f, 0.14f), 0.35f, 0.6f);
            var alloy = Mat(new Color(0.55f, 0.58f, 0.62f), 0.4f, 0.55f);

            var collar = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.4f, 0.6f, 0.55f) }, MaterialOverride = goldGlow };
            collar.Position = new Vector3(0, 0.3f, 0.35f);
            porch.AddChild(collar);
            foreach (int side in new[] { -1, 1 })
            {
                var mlishield = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 2.7f, 0.62f) }, MaterialOverride = goldMat };
                mlishield.Position = new Vector3(side * 1.35f, 1.6f, 0.15f);
                porch.AddChild(mlishield);
                var rib = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.09f, 0.66f) }, MaterialOverride = alloy };
                rib.Position = new Vector3(side * 1.35f, 1.05f, 0.16f);
                porch.AddChild(rib);
                var rail = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.055f, Height = 1.15f }, MaterialOverride = alloy };
                rail.Position = new Vector3(side * 1.05f, 0.58f, 0.62f);
                porch.AddChild(rail);
            }
            var hazard = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.4f, 0.06f, 0.34f) }, MaterialOverride = Mat(new Color(0.82f, 0.66f, 0.12f), 0.7f, 0.02f) };
            hazard.Position = new Vector3(0, 0.03f, 0.52f);
            porch.AddChild(hazard);
            var hazardBlack = Mat(new Color(0.12f, 0.11f, 0.10f), 0.8f, 0.02f);
            foreach (int side in new[] { -1, 1 })
            {
                var end = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.07f, 0.35f) }, MaterialOverride = hazardBlack };
                end.Position = new Vector3(side * 1.05f, 0.035f, 0.52f);
                porch.AddChild(end);
            }
            PorchCount++;
        }

        /// <summary>A cable tray along each building, below the roof line.</summary>
        private void BuildCableTray(Node3D root, float w, float d, float h)
        {
            var tray = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.30f, 0.16f, d * 0.8f) },
                MaterialOverride = Mat(new Color(0.42f, 0.45f, 0.50f), 0.55f, 0.35f),
                Position = new Vector3(-w * 0.5f - 0.15f, h * 0.78f, 0),
                Name = "CableTray",
            };
            root.AddChild(tray);
        }

        private void BuildBuilding(BuildingDefinition b)
        {
            var root = new Node3D { Name = b.Id, Position = new Vector3(b.PositionX, 0, b.PositionZ) };
            AddChild(root);

            // Most modules face the colony centre, but the airlock is the player's
            // vestibule: its front door must face the approach path to the south.
            if (b.Id != "airlock")
            {
                Vector3 toCenter = new Vector3(-b.PositionX, 0, -b.PositionZ);
                if (toCenter.LengthSquared() > 0.01f)
                    root.Rotation = new Vector3(0, Mathf.Atan2(toCenter.X, toCenter.Z), 0);
            }

            float w = b.FootprintW, d = b.FootprintD, h = b.Height;
            Color wall = b.ColorHex.StartsWith("#") ? Color.FromHtml(b.ColorHex) : new Color(0.8f, 0.83f, 0.86f);
            var wallMat = Mat(wall, 0.15f, 0.62f);
            var trimMat = Mat(wall.Darkened(0.32f), 0.25f, 0.5f);
            var floorMat = Mat(wall.Darkened(0.55f), 0.2f, 0.75f);

            if (b.Category == BuildingCategory.Decorative)
            {
                var platform = Solid(root, new Vector3(0, 0.15f, 0), new Vector3(w, 0.3f, d), trimMat);
                platform.Name = "Platform";
                return;
            }

            Solid(root, new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), floorMat);
            Solid(root, new Vector3(0, h - 0.12f, 0), new Vector3(w, 0.24f, d), trimMat);

            const float wallH = 0.34f;
            float gap = 2.8f;
            float side = (w - gap) * 0.5f;
            // Front wall (door side, local +Z)
            Solid(root, new Vector3(-(gap * 0.5f + side * 0.5f), (h - 0.3f) * 0.5f + 0.1f, d * 0.5f), new Vector3(side, h - 0.3f, wallH), wallMat, hideMesh: true);
            Solid(root, new Vector3(gap * 0.5f + side * 0.5f, (h - 0.3f) * 0.5f + 0.1f, d * 0.5f), new Vector3(side, h - 0.3f, wallH), wallMat, hideMesh: true);
            Solid(root, new Vector3(0, h - 0.55f, d * 0.5f), new Vector3(gap, 0.9f, wallH), wallMat, hideMesh: true);
            // Back and sides. The airlock is a passage, so its back wall opens too.
            if (b.Id == "airlock")
            {
                Solid(root, new Vector3(-(gap * 0.5f + side * 0.5f), (h - 0.3f) * 0.5f + 0.1f, -d * 0.5f), new Vector3(side, h - 0.3f, wallH), wallMat, hideMesh: true);
                Solid(root, new Vector3(gap * 0.5f + side * 0.5f, (h - 0.3f) * 0.5f + 0.1f, -d * 0.5f), new Vector3(side, h - 0.3f, wallH), wallMat, hideMesh: true);
                Solid(root, new Vector3(0, h - 0.55f, -d * 0.5f), new Vector3(gap, 0.9f, wallH), wallMat, hideMesh: true);
                BuildDoorFrame(root, d, h, gap, trimMat, true);
            }
            else
            {
                Solid(root, new Vector3(0, (h - 0.3f) * 0.5f + 0.1f, -d * 0.5f), new Vector3(w, h - 0.3f, wallH), wallMat, hideMesh: true);
            }
            Solid(root, new Vector3(-w * 0.5f, (h - 0.3f) * 0.5f + 0.1f, 0), new Vector3(wallH, h - 0.3f, d), wallMat, hideMesh: true);
            Solid(root, new Vector3(w * 0.5f, (h - 0.3f) * 0.5f + 0.1f, 0), new Vector3(wallH, h - 0.3f, d), wallMat, hideMesh: true);

            BuildDoorFrame(root, d, h, gap, trimMat, false);
            BuildFacadeRibs(root, w, d, h, gap, trimMat);
            BuildShell(root, b, w, d, h);
            BuildPorch(root, w, d, h);
            BuildCableTray(root, w, d, h);

            // Window strips and gold-foil thermal blanket on the shaded side. The greenhouse
            // has no transparent aperture: plants are lit by colony power inside a sealed pod,
            // because direct lunar sunlight carries unattenuated UV and particle radiation.
            if (b.Id != "greenhouse" && b.Id != "airlock")
            {
                var glass = Mat(new Color(0.28f, 0.55f, 0.75f), 0.6f, 0.15f, 0.25f);
                for (int i = -1; i <= 1; i++)
                {
                    var win = new MeshInstance3D { Name = "Window", Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.7f, 0.06f) }, MaterialOverride = glass };
                    win.Position = new Vector3(i * (w * 0.24f), h * 0.62f, -d * 0.5f - 0.19f);
                    root.AddChild(win);
                }
            }
            if (b.Id != "airlock")
            {
                var foil = Mat(new Color(0.92f, 0.68f, 0.22f), 0.85f, 0.3f);
                var blanket = new MeshInstance3D { Name = "GoldBlanket", Mesh = new BoxMesh { Size = new Vector3(w * 0.94f, 0.7f, 0.05f) }, MaterialOverride = foil };
                blanket.Position = new Vector3(0, 0.55f, -d * 0.5f - 0.2f);
                root.AddChild(blanket);
            }

            // Interior light and ceiling strip.
            var lamp = new OmniLight3D
            {
                LightColor = new Color(1f, 0.94f, 0.82f), LightEnergy = 1.15f,
                OmniRange = Math.Max(w, d) * 0.62f, ShadowEnabled = true,
                Position = new Vector3(0, h - 0.8f, 0),
            };
            root.AddChild(lamp);
            var strip = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(w * 0.55f, 0.06f, 0.18f) },
                MaterialOverride = Mat(new Color(1f, 0.95f, 0.82f), 0f, 0.4f, 2.2f),
                Position = new Vector3(0, h - 0.28f, 0),
            };
            root.AddChild(strip);

            // Outside nameplate on the facade, readable from the approach path.
            var plateMat = Mat(new Color(0.055f, 0.065f, 0.08f), 0.4f, 0.5f);
            var plate = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(Mathf.Min(w * 0.44f, 4.6f), 0.5f, 0.08f) },
                MaterialOverride = plateMat,
                Position = new Vector3(0, h - 0.52f, d * 0.5f + 0.22f),
            };
            root.AddChild(plate);
            var sign = BiLabel(b.NameEn, b.NameAr,
                new Vector3(0, h - 0.52f, d * 0.5f + 0.30f), 36, new Color(0.88f, 0.94f, 1f));
            sign.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            root.AddChild(sign);

            // A warm porch light above each entrance, so doors read at night.
            if (b.Id is "habitat" or "airlock" or "mission_control" or "life_support")
            {
                var porch = new OmniLight3D
                {
                    LightColor = new Color(1f, 0.88f, 0.68f), LightEnergy = 1.3f,
                    OmniRange = 7f, Position = new Vector3(0, h - 1.1f, d * 0.5f + 1.3f),
                };
                root.AddChild(porch);
            }

            DoorWorld[b.Id] = root.ToGlobal(new Vector3(0, 0.2f, d * 0.5f + 1.6f));
            InsideWorld[b.Id] = root.ToGlobal(new Vector3(0, 0.2f, 0));

            BuildRooms(root, b, wallMat, floorMat);
            BuildInteractions(root, b);
        }

        private void BuildRooms(Node3D root, BuildingDefinition b, Material wallMat, Material floorMat)
        {
            int index = 0;
            foreach (var room in b.Rooms)
            {
                var c = new Vector3(room.OffsetX, 0.12f, room.OffsetZ);
                var patch = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(room.SizeX * 0.92f, 0.02f, room.SizeZ * 0.92f) },
                    MaterialOverride = Mat(new Color(0.16f, 0.19f, 0.23f).Lerp(new Color(0.34f, 0.3f, 0.22f), index * 0.18f), 0f, 0.9f),
                    Position = c,
                };
                root.AddChild(patch);

                // Partitions: two stub walls give each room a real boundary without
                // sealing it, so the interior stays readable from the door.
                float px = room.OffsetX - room.SizeX * 0.5f;
                float pz = room.OffsetZ + room.SizeZ * 0.5f;
                Solid(root, new Vector3(px, 0.65f, room.OffsetZ), new Vector3(0.14f, 1.2f, room.SizeZ * 0.55f), wallMat);
                Solid(root, new Vector3(room.OffsetX, 0.65f, pz), new Vector3(room.SizeX * 0.45f, 1.2f, 0.14f), wallMat);

                var label = BiLabel(room.NameEn, room.NameAr,
                    c + new Vector3(0, 2.25f, 0), 34, new Color(0.75f, 0.85f, 0.95f));
                root.AddChild(label);

                Furnish(root, room, c);
                index++;
            }
            // Door mat.
            var mat = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.4f, 0.02f, 1.2f) },
                MaterialOverride = Mat(new Color(0.15f, 0.15f, 0.16f), 0f, 0.95f),
                Position = new Vector3(0, 0.115f, b.FootprintD * 0.5f - 0.8f),
            };
            root.AddChild(mat);
        }

        private void Furnish(Node3D root, InteriorRoom room, Vector3 c)
        {
            string p = room.Purpose.ToLowerInvariant() + " " + room.NameEn.ToLowerInvariant();
            var metal = Mat(new Color(0.42f, 0.45f, 0.5f), 0.55f, 0.4f);
            var dark = Mat(new Color(0.12f, 0.13f, 0.15f), 0.2f, 0.7f);
            var white = Mat(new Color(0.85f, 0.86f, 0.88f), 0.1f, 0.55f);
            var screen = Mat(new Color(0.2f, 0.7f, 0.9f), 0f, 0.3f, 1.4f);

            if (p.Contains("grow") || p.Contains("seed") || p.Contains("harvest") || p.Contains("plant"))
            {
                var pink = Mat(new Color(1f, 0.35f, 0.65f), 0f, 0.4f, 2.2f);
                for (int i = -1; i <= 1; i++)
                {
                    var rack = Solid(root, c + new Vector3(i * 1.5f, 0.75f, -0.7f), new Vector3(1.1f, 1.3f, 0.7f), metal);
                    var leaf = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.95f, 0.16f, 0.55f) }, MaterialOverride = Mat(new Color(0.2f, 0.55f, 0.24f), 0f, 0.85f) };
                    leaf.Position = c + new Vector3(i * 1.5f, 1.45f, -0.7f);
                    root.AddChild(leaf);
                    var light = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.05f, 0.12f) }, MaterialOverride = pink };
                    light.Position = c + new Vector3(i * 1.5f, 1.85f, -0.7f);
                    root.AddChild(light);
                }
            }
            else if (p.Contains("medical") || p.Contains("check") || p.Contains("quarter") || p.Contains("rest"))
            {
                var bed = Solid(root, c + new Vector3(-1.1f, 0.35f, 0f), new Vector3(1.9f, 0.45f, 0.95f), white);
                var pillow = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.16f, 0.7f) }, MaterialOverride = Mat(new Color(0.65f, 0.78f, 0.9f), 0f, 0.9f) };
                pillow.Position = c + new Vector3(-1.8f, 0.62f, 0f);
                root.AddChild(pillow);
                Solid(root, c + new Vector3(1.0f, 0.28f, 0.2f), new Vector3(0.7f, 0.4f, 0.7f), metal);
            }
            else if (p.Contains("repair") || p.Contains("fabric") || p.Contains("part"))
            {
                Solid(root, c + new Vector3(0, 0.55f, 0), new Vector3(2.2f, 0.9f, 0.9f), metal);
                for (int i = 0; i < 3; i++)
                {
                    var tool = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.3f, 0.18f) }, MaterialOverride = dark };
                    tool.Position = c + new Vector3(-0.6f + i * 0.6f, 1.08f, 0.2f);
                    root.AddChild(tool);
                }
                Solid(root, c + new Vector3(0, 1.5f, -0.4f), new Vector3(1.4f, 0.7f, 0.08f), screen);
            }
            else if (p.Contains("battery") || p.Contains("inverter") || p.Contains("power") || p.Contains("array"))
            {
                for (int i = -1; i <= 1; i++)
                    Solid(root, c + new Vector3(i * 0.9f, 1.0f, -0.6f), new Vector3(0.7f, 1.8f, 0.55f), dark);
                var meter = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.6f, 0.06f) }, MaterialOverride = screen };
                meter.Position = c + new Vector3(0, 1.7f, 0.5f);
                root.AddChild(meter);
            }
            else if (p.Contains("comms") || p.Contains("relay") || p.Contains("network") || p.Contains("antenna") || p.Contains("ops") || p.Contains("control"))
            {
                Solid(root, c + new Vector3(0, 0.5f, 0), new Vector3(2.4f, 0.9f, 0.8f), metal);
                var wall = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.8f, 1.2f, 0.08f) }, MaterialOverride = screen };
                wall.Position = c + new Vector3(0, 1.7f, -0.9f);
                root.AddChild(wall);
            }
            else if (p.Contains("lab") || p.Contains("sample") || p.Contains("microscop") || p.Contains("bench") || p.Contains("filter") || p.Contains("air"))
            {
                Solid(root, c + new Vector3(0, 0.55f, 0), new Vector3(2.0f, 0.9f, 0.8f), white);
                var scope = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 0.5f }, MaterialOverride = metal };
                scope.Position = c + new Vector3(0.4f, 1.2f, 0f);
                root.AddChild(scope);
                for (int i = -1; i <= 1; i += 2)
                {
                    var tank = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.28f, Height = 1.3f }, MaterialOverride = Mat(new Color(0.55f, 0.65f, 0.72f), 0.7f, 0.3f) };
                    tank.Position = c + new Vector3(i * 1.1f, 0.75f, -0.6f);
                    root.AddChild(tank);
                }
            }
            else
            {
                Solid(root, c + new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.75f, 0.8f), metal);
                var seat = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.5f) }, MaterialOverride = white };
                seat.Position = c + new Vector3(0.9f, 0.3f, 0.3f);
                root.AddChild(seat);
            }
        }

        private void BuildInteractions(Node3D root, BuildingDefinition b)
        {
            var palette = new Dictionary<string, Color>
            {
                ["clean"] = new Color(0.25f, 0.8f, 0.95f),
                ["repair"] = new Color(1f, 0.62f, 0.2f),
                ["harvest"] = new Color(0.35f, 0.95f, 0.45f),
                ["read"] = new Color(0.45f, 0.68f, 1f),
                ["use"] = new Color(1f, 0.85f, 0.35f),
                ["inspect"] = new Color(0.8f, 0.55f, 1f),
            };

            int idx = 0;
            foreach (var i in b.Interactions)
            {
                var room = b.Rooms.Find(r => r.Id == i.RoomId);
                Vector3 basePos = room != null
                    ? new Vector3(room.OffsetX, 0.1f, room.OffsetZ)
                    : new Vector3(0, 0.1f, 0);
                basePos += new Vector3(0.9f * ((idx % 2 == 0) ? -1 : 1), 0, b.FootprintD * 0.22f - 0.4f * (idx % 3));

                var station = new Node3D { Position = basePos };
                root.AddChild(station);

                var pedestal = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.55f, 1.0f, 0.45f) },
                    MaterialOverride = Mat(new Color(0.18f, 0.2f, 0.24f), 0.5f, 0.45f),
                    Position = new Vector3(0, 0.5f, 0),
                };
                station.AddChild(pedestal);

                Color col = palette.TryGetValue(i.Kind, out var pc) ? pc : new Color(0.6f, 0.8f, 1f);
                var screenMat = Mat(col, 0.1f, 0.3f, 1.6f);
                var screen = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.62f, 0.42f, 0.06f) },
                    MaterialOverride = screenMat,
                    Position = new Vector3(0, 1.18f, 0.18f),
                    Rotation = new Vector3(Mathf.DegToRad(-18), 0, 0),
                };
                station.AddChild(screen);

                var label = BiLabel(i.NameEn, i.NameAr,
                    new Vector3(0, 1.75f, 0), 30, col);
                label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                station.AddChild(label);

                // Physical text for the plant and habitat rules, updated live.
                if (i.Id == "crop_monitor")
                {
                    _greenhouseLive = new Label3D { Text = "", FontSize = 26, PixelSize = 0.0045f, Modulate = new Color(0.85f, 1f, 0.9f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 2.15f, 0) };
                    _greenhouseLive.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
                    station.AddChild(_greenhouseLive);
                }

                var area = new Interactable
                {
                    BuildingId = b.Id,
                    InteractionId = i.Id,
                    PromptEn = i.NameEn,
                    PromptAr = i.NameAr,
                    Kind = i.Kind,
                    Position = new Vector3(0, 0.6f, 0.9f),
                };
                area.Label = label;
                area.Screen = screen;
                station.AddChild(area);
                idx++;
            }
        }

        // ---- exterior props --------------------------------------------------

        private void BuildSolarFarm(BuildingDefinition? farm)
        {
            if (farm == null) return;
            var root = new Node3D { Name = "SolarFarm" };
            AddChild(root);
            float baseX = farm.PositionX - 6f;
            float baseZ = farm.PositionZ - 26f;

            _panelMat = Mat(new Color(0.08f, 0.14f, 0.32f), 0.75f, 0.18f, 0.12f);
            var frame = Mat(new Color(0.35f, 0.37f, 0.4f), 0.7f, 0.4f);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var panel = new MeshInstance3D
                    {
                        Mesh = new BoxMesh { Size = new Vector3(2.6f, 4.2f, 0.1f) },
                        MaterialOverride = _panelMat,
                        Position = new Vector3(baseX + col * 3.0f, 2.6f, baseZ + row * 4.2f),
                        Rotation = new Vector3(Mathf.DegToRad(18), Mathf.DegToRad(4), 0),
                    };
                    root.AddChild(panel);
                    var post = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = 1.2f },
                        MaterialOverride = frame,
                        Position = new Vector3(baseX + col * 3.0f, 0.6f, baseZ + row * 4.2f),
                    };
                    root.AddChild(post);
                }
            }

            var sign = BiLabel("SOLAR FARM  ·  30 kW", "مزرعة الطاقة  ·  30 كيلوواط",
                new Vector3(baseX + 8.5f, 3.2f, baseZ - 3f), 54, new Color(0.9f, 0.92f, 1f));
            sign.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            root.AddChild(sign);
        }

        private void BuildCommsMast(BuildingDefinition? tower)
        {
            if (tower == null) return;
            var root = new Node3D { Name = "CommsMast", Position = new Vector3(tower.PositionX + 7f, 0, tower.PositionZ + 6f) };
            AddChild(root);
            var metal = Mat(new Color(0.62f, 0.66f, 0.7f), 0.8f, 0.35f);
            var mast = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.35f, Height = 16f }, MaterialOverride = metal };
            mast.Position = new Vector3(0, 8f, 0);
            root.AddChild(mast);

            var dish = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 3.4f, BottomRadius = 0.3f, Height = 0.7f }, MaterialOverride = metal };
            dish.Position = new Vector3(0, 14.5f, 0);
            dish.Rotation = new Vector3(Mathf.DegToRad(-55), 0, 0);
            root.AddChild(dish);
            var feed = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 1.4f }, MaterialOverride = metal };
            feed.Position = new Vector3(0, 15.4f, 0.8f);
            feed.Rotation = new Vector3(Mathf.DegToRad(-35), 0, 0);
            root.AddChild(feed);

            _beacon = new OmniLight3D
            {
                LightColor = new Color(1f, 0.15f, 0.1f), LightEnergy = 1.5f,
                OmniRange = 8f, Position = new Vector3(0, 16.2f, 0),
            };
            root.AddChild(_beacon);
            var bulb = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f }, MaterialOverride = Mat(new Color(1f, 0.2f, 0.1f), 0f, 0.4f, 3f) };
            bulb.Position = new Vector3(0, 16.2f, 0);
            root.AddChild(bulb);
        }

        private void BuildRover(BuildingDefinition? garage)
        {
            if (garage == null) return;
            var root = new Node3D { Name = "Rover", Position = new Vector3(garage.PositionX + 12f, 0, garage.PositionZ + 4f) };
            AddChild(root);
            var body = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.4f, 0.8f, 1.9f) }, MaterialOverride = Mat(new Color(0.85f, 0.87f, 0.9f), 0.4f, 0.45f) };
            body.Position = new Vector3(0, 0.95f, 0);
            root.AddChild(body);
            var panel = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.6f, 0.06f, 1.5f) }, MaterialOverride = Mat(new Color(0.1f, 0.18f, 0.4f), 0.7f, 0.2f, 0.15f) };
            panel.Position = new Vector3(0, 1.4f, 0);
            root.AddChild(panel);
            var wheelMat = Mat(new Color(0.12f, 0.12f, 0.13f), 0.2f, 0.85f);
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    var wheel = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.3f }, MaterialOverride = wheelMat };
                    wheel.Position = new Vector3(ix * 1.35f, 0.42f, iz * 0.85f);
                    wheel.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
                    root.AddChild(wheel);
                }
        }

        private void BuildCraters()
        {
            var rim = Mat(new Color(0.28f, 0.27f, 0.26f), 0f, 0.98f);
            var floor = Mat(new Color(0.12f, 0.12f, 0.13f), 0f, 1f);
            var spots = new (float x, float z, float r)[]
            {
                (-90, 70, 10), (120, 80, 16), (-150, -90, 22),
                (160, -110, 13), (60, 160, 18), (-70, -170, 14),
            };
            foreach (var s in spots)
            {
                var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = s.r * 0.82f, OuterRadius = s.r, Rings = 48, RingSegments = 6 }, MaterialOverride = rim };
                ring.Scale = new Vector3(1, 0.10f, 1);
                ring.Position = new Vector3(s.x, 0.06f, s.z);
                AddChild(ring);
                var disc = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = s.r * 0.84f, BottomRadius = s.r * 0.84f, Height = 0.02f }, MaterialOverride = floor };
                disc.Position = new Vector3(s.x, 0.02f, s.z);
                AddChild(disc);
            }
        }

        private void BuildRocks()
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f, RadialSegments = 6, Rings = 4 },
            };
            mm.Mesh.SurfaceSetMaterial(0, Mat(new Color(0.32f, 0.31f, 0.3f), 0f, 0.98f));
            var rng = new Random(11);
            int count = 160;
            mm.InstanceCount = count;
            for (int i = 0; i < count; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2.0;
                double r = 40 + rng.NextDouble() * 190.0;
                var pos = new Vector3((float)(Math.Cos(a) * r), 0.1f, (float)(Math.Sin(a) * r));
                var basis = Basis.Identity.Rotated(Vector3.Up, (float)(rng.NextDouble() * Math.PI));
                float sc = 0.3f + (float)rng.NextDouble() * 1.6f;
                basis = basis.Scaled(new Vector3(sc, sc * 0.55f, sc));
                mm.SetInstanceTransform(i, new Transform3D(basis, pos));
            }
            var mmi = new MultiMeshInstance3D { Multimesh = mm, Name = "Rocks" };
            AddChild(mmi);
        }

        private void SpawnNpcs(ColonySimulation sim)
        {
            _npcRoot = new Node3D { Name = "Crew" };
            AddChild(_npcRoot);
            foreach (var profile in sim.Content.Npcs)
            {
                if (profile.IsRobot)
                {
                    Robot = new RobotCompanion { Profile = profile, World = this };
                    _npcRoot.AddChild(Robot);
                    continue;
                }
                var agent = new NpcAgent { Profile = profile, World = this };
                _npcRoot.AddChild(agent);
                Npcs.Add(agent);
            }
        }

        // ---- live world ------------------------------------------------------

        public override void _Process(double delta)
        {
            if (_sim == null) return;
            _t += delta;
            var cam = GetViewport().GetCamera3D();
            if (cam != null)
            {
                _sky.GlobalPosition = cam.GlobalPosition;
                Streamer.SetFocus(cam.GlobalPosition);
            }
            UpdateSun((float)_sim.Clock.LunarPhase);
            if (_beacon != null) _beacon.LightEnergy = (Math.Sin(_t * 2.2) > 0.4) ? 1.4f : 0.05f;
            if (_earth != null) _earth.RotateY((float)delta * 0.01f);

            float dust = (float)_sim.State.ArrayDust;
            if (_panelMat != null)
                _panelMat.AlbedoColor = new Color(0.08f, 0.14f, 0.32f).Lerp(new Color(0.47f, 0.44f, 0.4f), Mathf.Clamp(dust * 1.1f, 0, 1));

            _liveTimer += delta;
            if (_liveTimer > 1.0)
            {
                _liveTimer = 0;
                UpdateLiveLabels();
            }
        }

        private void UpdateSun(float phase)
        {
            float az = phase * 360f + 180f;
            float el = 0.8f + Mathf.Sin(phase * Mathf.Tau) * 0.7f;
            _sun.RotationDegrees = new Vector3(-el, az, 0);
            if (_sunDisc != null)
            {
                var rot = new Basis(Vector3.Up, Mathf.DegToRad(az)) * new Basis(Vector3.Right, Mathf.DegToRad(-el));
                // The visible disc sits at the opposite end of the light ray, so shadows always
                // point away from the Sun you can see.
                _sunDisc.Position = -(rot * new Vector3(0, 0, -1)) * 420f;
            }
            // Stars are points of light; the sun on the horizon washes them out.
            if (_starMat != null)
            {
                _starMat.AlbedoColor = new Color(1f, 1f, 1f,
                    Mathf.Clamp(1.15f - (0.8f + Mathf.Sin(phase * Mathf.Tau) * 0.7f) * 0.45f, 0.18f, 1f));
            }
            // Keep the Earth's phase in step with the colony's sun (same light, same direction).
            if (_earth?.MaterialOverride is ShaderMaterial em && em.Shader?.GetPath() == "res://shaders/earth.gdshader")
                em.SetShaderParameter("sun_dir", _sunDisc != null
                    ? _sunDisc.Position.Normalized()
                    : new Vector3(0.75f, 0.25f, 0.6f));
        }

        private void UpdateLiveLabels()
        {
            var s = _sim.State;
            if (_greenhouseLive != null)
            {
                double waterDay = CropModel.PodAreaM2 * CropModel.WaterLPerM2Day;
                double co2Day = CropModel.PodAreaM2 * CropModel.Co2KgPerM2Day;
                _greenhouseLive.Text = L.T(
                    $"SEALED POD  ·  light {(_sim.State.GrowLightsOn ? "2.0" : "0.0")} kW from colony power\n" +
                    $"CO2 {co2Day:0.0} kg/day from cabin  ·  water {waterDay:0} L/day (90% recovered)\n" +
                    $"crop {CropModel.Stage(s)}  ·  health {s.CropHealth * 100:0}%",
                    $"وحدة مغلقة  ·  الضوء {(_sim.State.GrowLightsOn ? "2.0" : "0.0")} كيلوواط من طاقة المستعمرة\n" +
                    $"CO2 {co2Day:0.0} كغ/يوم من المقصورة  ·  ماء {waterDay:0} لتر/يوم (استعادة 90%)\n" +
                    $"المحصول {CropModel.Stage(s)}  ·  الصحة {s.CropHealth * 100:0}%");
            }
        }

        // ---- helpers ---------------------------------------------------------

        public Node3D CreateAstronaut(float targetHeight = 1.75f)
        {
            var packed = GD.Load<PackedScene>("res://assets/astronaut/cute_astronaut.glb");
            if (packed == null)
            {
                // There is exactly one human character model in Luna-Quest. A missing GLB is a
                // broken checkout, not an invitation to draw a different astronaut.
                GD.PushError("Luna-Quest: the Cute Astronaut GLB is missing. Run tools/sync-content.sh before launching. No substitute astronaut will be drawn.");
                return new Node3D { Name = "SuitHolder" };
            }
            var holder = new Node3D { Name = "SuitHolder" };
            // The imported Sketchfab node faces +Z once the action is posed, which is
            // also the direction the player/crew yaw math uses. Keep the model upright
            // and unturned so the visor leads when the holder walks forward.
            var modelRoot = new Node3D { Name = "ModelRoot" };
            holder.AddChild(modelRoot);
            var inst = packed.Instantiate<Node3D>();
            modelRoot.AddChild(inst);
            AddChild(holder);

            // The Sketchfab rig ships one long action: frame 0 is a seated bind pose,
            // the early frames hold a standing idle, the middle contains a complete
            // walk cycle and the late frames drift away from the origin. The animator
            // turns the idle and walk sections into looping clips, so the holder can
            // move through the world while the suit animates in place.
            var anim = FindChild<AnimationPlayer>(holder);
            var animator = new AstronautAnimator { Name = "Animator" };
            holder.AddChild(animator);
            if (anim != null)
                animator.Setup(anim);

            DressAstronaut(holder, modelRoot);

            // The raw mesh AABB is the bind pose (14 units tall) and has nothing to do
            // with the standing silhouette. The posed model measures 2.311 units from
            // sole to crown, so normalize from that instead.
            const float posedHeight = 2.311f;
            float sc = targetHeight / posedHeight;
            holder.Scale = new Vector3(sc, sc, sc);
            RemoveChild(holder);
            return holder;
        }

        /// <summary>
        /// The Sketchfab download kept the visor rim but lost the black glass inside it,
        /// and its suit material is untextured. Restore the readable silhouette from the
        /// model thumbnail: dark glossy visor, a chest control module and a small patch.
        /// The pieces are children of the holder, so they scale and turn with the suit.
        /// </summary>
        private void DressAstronaut(Node3D holder, Node3D modelRoot)
        {
            var blackGlass = Mat(new Color(0.012f, 0.018f, 0.032f), 0.45f, 0.06f);
            var dark = Mat(new Color(0.045f, 0.05f, 0.06f), 0.2f, 0.45f);
            // The suit itself: a slightly warm off-white with a rim highlight. Untextured
            // white against black space has no silhouette without the rim, which is exactly
            // why the astronaut read as a washed-out blob in the first place.
            var white = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.80f, 0.82f, 0.85f),
                Metallic = 0.04f,
                Roughness = 0.62f,
                RimEnabled = true,
                Rim = 0.55f,
                RimTint = 0.35f,
            };
            var packMat = Mat(new Color(0.40f, 0.44f, 0.50f), 0.18f, 0.45f);
            var accent = Mat(new Color(0.92f, 0.52f, 0.12f), 0.25f, 0.4f);
            var screen = Mat(new Color(0.05f, 0.5f, 0.85f), 0.1f, 0.2f, 1.6f);

            foreach (var node in holder.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mi || mi.Mesh == null) continue;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var m = mi.Mesh.SurfaceGetMaterial(s);
                    string surface = m?.ResourceName.ToLowerInvariant() ?? "";
                    if (surface.Contains("visier"))
                        mi.SetSurfaceOverrideMaterial(s, blackGlass);
                    else
                        mi.SetSurfaceOverrideMaterial(s, white);
                }
            }

            // Glass dome inside the visor rim (rim spans z 0.17..0.82, x -0.53..0.27,
            // y 1.29..2.27 in the posed model's local units).
            var visor = new MeshInstance3D
            {
                Name = "VisorGlass",
                Mesh = new SphereMesh { Radius = 0.33f, Height = 0.86f, RadialSegments = 32, Rings = 16 },
                MaterialOverride = blackGlass,
                Position = new Vector3(-0.126f, 1.76f, 0.56f),
                Scale = new Vector3(1f, 1f, 0.95f),
            };
            modelRoot.AddChild(visor);

            // Chest control module, the shape that reads as a suit rather than a mannequin.
            var pack = new MeshInstance3D
            {
                Name = "ChestPack",
                Mesh = new BoxMesh { Size = new Vector3(0.46f, 0.30f, 0.14f) },
                MaterialOverride = white,
                Position = new Vector3(-0.02f, 1.20f, 0.36f),
            };
            modelRoot.AddChild(pack);
            var panel = new MeshInstance3D
            {
                Name = "ChestScreen",
                Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.10f, 0.02f) },
                MaterialOverride = screen,
                Position = new Vector3(-0.02f, 1.22f, 0.44f),
            };
            modelRoot.AddChild(panel);

            // Two small orange tabs on the pack, echoing the thumbnail's accent bands.
            for (int i = -1; i <= 1; i += 2)
            {
                var tab = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.14f, 0.03f) },
                    MaterialOverride = accent,
                    Position = new Vector3(-0.02f + i * 0.28f, 1.20f, 0.43f),
                };
                modelRoot.AddChild(tab);
            }

            // Life-support backpack. The default camera trails the player, so the back
            // needs as much silhouette as the front: a PLSS box, two gas tanks and the
            // orange shoulder straps that read at a distance.
            var back = new MeshInstance3D
            {
                Name = "Backpack",
                Mesh = new BoxMesh { Size = new Vector3(0.44f, 0.58f, 0.22f) },
                MaterialOverride = packMat,
                Position = new Vector3(-0.02f, 1.18f, -0.36f),
            };
            modelRoot.AddChild(back);
            var backPanel = new MeshInstance3D
            {
                Name = "BackpackPanel",
                Mesh = new BoxMesh { Size = new Vector3(0.30f, 0.36f, 0.03f) },
                MaterialOverride = dark,
                Position = new Vector3(-0.02f, 1.18f, -0.485f),
            };
            modelRoot.AddChild(backPanel);

            for (int i = -1; i <= 1; i += 2)
            {
                var tank = new MeshInstance3D
                {
                    Name = i < 0 ? "TankL" : "TankR",
                    Mesh = new CylinderMesh
                    {
                        TopRadius = 0.08f,
                        BottomRadius = 0.08f,
                        Height = 0.40f,
                        RadialSegments = 20,
                    },
                    MaterialOverride = dark,
                    Position = new Vector3(-0.02f + i * 0.27f, 1.18f, -0.50f),
                };
                modelRoot.AddChild(tank);
            }

            // Orange bands top and bottom frame the grey PLSS and stay visible from the
            // trailing camera even when the tanks are in shadow.
            for (int i = -1; i <= 1; i += 2)
            {
                var band = new MeshInstance3D
                {
                    Name = i < 0 ? "PackBandLow" : "PackBandHigh",
                    Mesh = new BoxMesh { Size = new Vector3(0.38f, 0.035f, 0.02f) },
                    MaterialOverride = accent,
                    Position = new Vector3(-0.02f, i < 0 ? 0.93f : 1.43f, -0.49f),
                };
                modelRoot.AddChild(band);
            }
        }

        private static Aabb? ComputeBounds(Node node, Transform3D xform)
        {
            Aabb? result = null;
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var a = xform * mi.GetAabb();
                result = a;
            }
            foreach (var child in node.GetChildren())
            {
                if (child is Node3D n3)
                {
                    var childA = ComputeBounds(n3, xform * n3.Transform);
                    if (childA.HasValue) result = result.HasValue ? result.Value.Merge(childA.Value) : childA;
                }
            }
            return result;
        }

        private static T? FindChild<T>(Node root) where T : Node
        {
            foreach (var c in root.GetChildren())
            {
                if (c is T t) return t;
                var nested = FindChild<T>(c);
                if (nested != null) return nested;
            }
            return null;
        }

        private StandardMaterial3D Mat(Color c, float metallic = 0.05f, float roughness = 0.75f, float emission = 0f)
        {
            string key = $"{c}_{metallic}_{roughness}_{emission}";
            if (_mats.TryGetValue(key, out var m)) return m;
            m = new StandardMaterial3D { AlbedoColor = c, Metallic = metallic, Roughness = roughness };
            if (emission > 0)
            {
                m.EmissionEnabled = true;
                m.Emission = c;
                m.EmissionEnergyMultiplier = emission;
            }
            _mats[key] = m;
            return m;
        }

        private static StaticBody3D Solid(Node3D parent, Vector3 pos, Vector3 size, Material mat, bool hideMesh = false)
        {
            var body = new StaticBody3D { Position = pos, CollisionLayer = 1, CollisionMask = 0 };
            parent.AddChild(body);
            var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = mat };
            body.AddChild(mesh);
            var col = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
            body.AddChild(col);
            if (hideMesh)
            {
                mesh.Name = "WallMesh";
                mesh.Visible = false;
                WallMeshes.Add(mesh);
            }
            return body;
        }

        /// <summary>All hidden plain-wall meshes, collected for diagnostics.</summary>
        public static readonly List<Node3D> WallMeshes = new();

        private Label3D MakeLabel(string text, Vector3 pos, int size, Color color)
            => new Label3D
            {
                Text = text,
                Position = pos,
                FontSize = size,
                PixelSize = 0.0048f,
                Modulate = color,
                OutlineSize = 6,
                OutlineModulate = new Color(0, 0, 0, 0.85f),
                NoDepthTest = false,
                Font = L.Arabic ? L.ArabicFont : L.LatinFont,
            };

        /// <summary>A world label that knows both languages and can be refreshed in place.</summary>
        private Label3D BiLabel(string en, string ar, Vector3 pos, int size, Color color)
        {
            var label = MakeLabel(L.Arabic ? ar : en, pos, size, color);
            _bilingual.Add((label, en, ar));
            return label;
        }

        public void RefreshLanguage()
        {
            foreach (var (label, en, ar) in _bilingual)
            {
                if (!IsInstanceValid(label)) continue;
                label.Text = L.Arabic ? ar : en;
                label.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
            }
            if (_greenhouseLive != null)
                _greenhouseLive.Font = L.Arabic ? L.ArabicFont : L.LatinFont;
        }

        private static Texture2D? TryLoadTexture(string path)
            => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }
}
