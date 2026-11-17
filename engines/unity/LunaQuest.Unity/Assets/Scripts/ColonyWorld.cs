using System;
using System.Collections.Generic;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunaQuest.Unity
{
    /// <summary>
    /// Builds the colony procedurally from the same buildings.json the Godot adapter uses.
    /// Walls are solid, doors are openings, and every interaction is a physical console
    /// inside a named room. The Moon is a real place: low sun, sharp shadows, no sky haze.
    /// </summary>
    public sealed class ColonyWorld : MonoBehaviour
    {
        public readonly List<Station> Stations = new List<Station>();
        public readonly List<CrewAgent> Crew = new List<CrewAgent>();

        private readonly Dictionary<string, Transform> _buildings = new Dictionary<string, Transform>();
        private readonly List<WorldLabel> _labels = new List<WorldLabel>();

        private LunaQuestGame _game;
        private GameObject _astronaut;
        private Material _visor;
        private Material _pack;
        private Material _black;
        private Material _white;
        private Material _orange;
        private Material _blueGlow;
        private Material _warmGlow;
        private Material _regolith;
        private Material _rock;
        private Material _earthMat;
        private Material _suit;
        private readonly LunaQuest.Core.Terrain.LunarTerrain _terrain =
            new LunaQuest.Core.Terrain.LunarTerrain(1337);

        public void Init(LunaQuestGame game)
        {
            _game = game;
        }

        public void Build()
        {
            if (_game == null) _game = LunaQuestGame.Instance;
            _astronaut = Resources.Load<GameObject>("Art/Astronaut/cute_astronaut_baked");
            BuildMaterials();
            BuildEnvironment();
            BuildGround();
            BuildPlaza();
            BuildLandingPad();

            foreach (var building in _game.Sim.Content.Buildings)
                BuildBuilding(building);

            BuildSolarFarm();
            BuildCommsMast();
            BuildRover();
            BuildIceRig();
            BuildRegolithPlant();
            BuildCraters();
            BuildRocks();
            SpawnCrew();

            Debug.Log($"Colony built: {_game.Sim.Content.Buildings.Count} buildings, " +
                      $"{Stations.Count} stations, {Crew.Count} crew.");
        }

        // ---- materials -------------------------------------------------------

        private void BuildMaterials()
        {
            _suit = Mat(new Color(0.78f, 0.80f, 0.83f), 0.05f, 0.42f);
            _visor = Mat(new Color(0.010f, 0.016f, 0.026f), 0.35f, 0.92f);
            _pack = Mat(new Color(0.40f, 0.44f, 0.50f), 0.18f, 0.45f);
            _black = Mat(new Color(0.045f, 0.052f, 0.065f), 0.30f, 0.48f);
            _white = Mat(new Color(0.84f, 0.86f, 0.89f), 0.08f, 0.4f);
            _orange = Mat(new Color(0.95f, 0.52f, 0.10f), 0.15f, 0.35f);
            _blueGlow = Mat(new Color(0.10f, 0.45f, 0.85f), 0.1f, 0.25f,
                new Color(0.20f, 0.65f, 1.0f), 1.8f);
            _warmGlow = Mat(new Color(1.0f, 0.86f, 0.58f), 0.0f, 0.3f,
                new Color(1.0f, 0.78f, 0.45f), 2.2f);
            _regolith = Mat(new Color(0.40f, 0.35f, 0.29f), 0.0f, 0.96f);
            _rock = Mat(new Color(0.115f, 0.112f, 0.108f), 0.0f, 0.98f);
        }

        private static Material Mat(Color color, float metallic = 0f, float smoothness = 0.35f,
            Color? emission = null, float emissionEnergy = 1f)
        {
            var shader = Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value * emissionEnergy);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return material;
        }

        private static Material Unlit(Color color, Texture texture = null)
        {
            var shader = Shader.Find(texture != null ? "Unlit/Texture" : "Unlit/Color");
            var material = new Material(shader) { color = color };
            if (texture != null) material.mainTexture = texture;
            return material;
        }

        private readonly List<string> _shells = new List<string>();
        private readonly List<string> _porches = new List<string>();
        public int ShellCount => _shells.Count;
        public int PorchCount => _porches.Count;

        /// <summary>
        /// F2: wrap the module in a high-fidelity primitive shell — Unity cannot load .glb kit
        /// modules (no glTFast without a real editor), so the shell is built to the kit
        /// silhouettes: a cylinder hull, vertical ribs, gold MLI flank panels and a cable tray.
        /// Colliders stay live; only the plain box wall meshes are disabled.
        /// </summary>
        private void BuildShell(Transform root, BuildingDefinition b, float w, float d, float h)
        {
            var shell = new GameObject("Shell_" + b.Id);
            shell.transform.SetParent(root, false);

            Color baseColor;
            if (!ColorUtility.TryParseHtmlString(b.ColorHex, out baseColor))
                baseColor = new Color(0.78f, 0.81f, 0.85f);
            var hull = Mat(baseColor * 0.92f, 0.05f, 0.30f);
            var dark = Mat(new Color(0.28f, 0.30f, 0.35f), 0.30f, 0.35f);
            var gold = Mat(new Color(0.92f, 0.62f, 0.18f), 0.35f, 0.55f,
                new Color(0.95f, 0.66f, 0.2f), 0.25f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "ShellCore";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(shell.transform, false);
            body.transform.localPosition = new Vector3(0f, h * 0.5f - 0.08f, 0f);
            body.transform.localScale = new Vector3(w, h * 0.5f - 0.16f, d);
            body.GetComponent<Renderer>().sharedMaterial = hull;

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 pos = new Vector3(dir.x * w * 0.46f, h * 0.5f, dir.z * d * 0.46f);
                var rib = Box(shell.transform, "Rib_" + i, pos, new Vector3(0.28f, h * 0.86f, 0.28f), dark);
                rib.transform.SetParent(shell.transform, true);
                rib.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Box(shell.transform, "GoldPanel_" + s, new Vector3(s * w * 0.34f, h * 0.56f, -d * 0.46f),
                    new Vector3(w * 0.22f, h * 0.34f, 0.07f), gold);
            }
            Box(shell.transform, "CableTray", new Vector3(w * 0.5f + 0.14f, h * 0.74f, 0f),
                new Vector3(0.24f, 0.15f, d * 0.82f), dark);
            _shells.Add(b.Id);
        }

        /// <summary>An airlock-style porch: gold collar, gold foil panels, handrails and a
        /// hazard-stripe sill, on the door side.</summary>
        private void BuildPorch(Transform root, float w, float d, float h)
        {
            var porch = new GameObject("Porch");
            porch.transform.SetParent(root, false);
            porch.transform.localPosition = new Vector3(0f, 0f, d * 0.5f);
            var gold = Mat(new Color(0.95f, 0.68f, 0.20f), 0.45f, 0.55f,
                new Color(0.98f, 0.72f, 0.24f), 0.3f);
            var alloy = Mat(new Color(0.55f, 0.58f, 0.62f), 0.35f, 0.55f);
            var hazard = Mat(new Color(0.84f, 0.68f, 0.14f), 0.75f, 0.1f);
            var black = Mat(new Color(0.12f, 0.11f, 0.10f), 0.85f, 0.05f);

            Box(porch.transform, "GoldCollar", new Vector3(0f, 0.3f, 0.35f),
                new Vector3(3.6f, 0.6f, 0.55f), gold);
            foreach (int s in new[] { -1, 1 })
            {
                Box(porch.transform, "GoldEdge_" + s, new Vector3(s * 1.42f, 1.62f, 0.16f),
                    new Vector3(0.5f, 2.8f, 0.64f), gold);
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rail.name = "Handrail_" + s;
                Destroy(rail.GetComponent<Collider>());
                rail.transform.SetParent(porch.transform, false);
                rail.transform.localPosition = new Vector3(s * 1.08f, 0.58f, 0.62f);
                rail.transform.localScale = new Vector3(0.11f, 0.575f, 0.11f);
                rail.GetComponent<Renderer>().sharedMaterial = alloy;
            }
            Box(porch.transform, "HazardSill", new Vector3(0f, 0.028f, 0.54f),
                new Vector3(2.4f, 0.06f, 0.34f), hazard);
            foreach (int s in new[] { -1, 1 })
                Box(porch.transform, "HazardEnd_" + s, new Vector3(s * 1.05f, 0.04f, 0.54f),
                    new Vector3(0.35f, 0.07f, 0.35f), black);
            _porches.Add(root.name);
        }

        // ---- environment -----------------------------------------------------

        private void BuildEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.25f, 0.20f);
            RenderSettings.fog = false;
            RenderSettings.skybox = BuildSkybox();

            var sunGo = new GameObject("LowSun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(17f, 205f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.96f, 0.89f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;

            var fillGo = new GameObject("EarthShine");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(42f, 28f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.38f, 0.47f, 0.72f);
            fill.intensity = 0.10f;
            fill.shadows = LightShadows.None;

            // A small unshaded solar disc. At 89.9° S the Sun crawls along the horizon;
            // this is the glare the crew sees all shift, not a noon sun.
            var disc = Sphere(transform, "SunDisc", new Vector3(-300f, 52f, -360f),
                new Vector3(22f, 22f, 22f), Unlit(new Color(1f, 0.94f, 0.78f)));
            NoCollider(disc);

            // Earth: the real day map from Solar System Scope, credited in CREDITS.md.
            // Standard (lit) material: the directional LowSun lights the day side and the side
            // away from it goes dark — no glowing ball. Sized to read ~2 degrees across (the Moon
            // sees Earth about 1.9 degrees wide), essentially fixed over the ridge.
            var texture = Resources.Load<Texture2D>("Textures/earth");
            _earthMat = new Material(Shader.Find("Standard")) { mainTexture = texture };
            var earth = Sphere(transform, "Earth", new Vector3(95f, 118f, -305f),
                new Vector3(12f, 12f, 12f), _earthMat);
            earth.transform.rotation = Quaternion.Euler(8f, 160f, -12f);
            NoCollider(earth);
            var earthLight = earth.AddComponent<Light>();
            earthLight.type = LightType.Point;
            earthLight.range = 0f;
            earthLight.intensity = 0f;
        }

        private static Material BuildSkybox()
        {
            const int w = 1024;
            const int h = 512;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "LunaStars",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[w * h];
            var black = new Color32(0, 0, 0, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = black;

            var rng = new System.Random(1972);
            // A faint Milky Way band behind the sharp stars, along one great-circle axis.
            for (int i = 0; i < 8500; i++)
            {
                float x = (float)rng.NextDouble() * w;
                float centre = h * 0.52f + Mathf.Sin(x / w * Mathf.PI * 2f) * h * 0.12f;
                float y = centre + (float)(rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * h * 0.11f;
                if (y < 0 || y >= h) continue;
                int idx = (int)y * w + (int)x;
                byte v = (byte)(7 + rng.Next(6));
                pixels[idx] = new Color32(v, v, (byte)(v + 4), 255);
            }
            // Bright points, magnitude-spread: many faint, few bright, slight colour variance.
            for (int i = 0; i < 2400; i++)
            {
                double m = Math.Pow(rng.NextDouble(), 2.4);
                int x = rng.Next(w);
                int y = rng.Next(h);
                int idx = y * w + x;
                byte v = (byte)(90 + m * 160.0);
                double wtone = rng.NextDouble();
                byte vr = v, vg = v, vb = v;
                if (wtone < 0.78) { } // white
                else if (wtone < 0.89) { vg = (byte)(v * 0.82); vb = (byte)(v * 0.65); } // warm
                else { vr = (byte)(v * 0.78); vg = (byte)(v * 0.88); } // cool
                pixels[idx] = new Color32(vr, vg, vb, 255);
                if (rng.NextDouble() > 0.72 && v > 140)
                {
                    int x2 = Mathf.Min(w - 1, x + 1);
                    pixels[y * w + x2] = new Color32((byte)(vr * 0.7f), (byte)(vg * 0.7f), (byte)(vb * 0.7f), 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.SetTexture("_MainTex", texture);
            mat.SetFloat("_Exposure", 1.15f);
            mat.SetFloat("_Rotation", 12f);
            return mat;
        }

        /// <summary>
        /// The height of the shared lunar surface at a point, in metres. One Moon: this is the
        /// same deterministic field (seed 1337) the ground mesh is built from, so gameplay code
        /// never has to guess where the ground is.
        /// </summary>
        public float HeightAt(float x, float z) => (float)_terrain.Height(x, z);

        private void BuildGround()
        {
            // One Moon for every engine: the ground is generated from the shared deterministic
            // heightfield (LunaQuest.Core.Terrain.LunarTerrain) — the same surface Godot draws,
            // seed 1337, flat pad around (0, 8). Replaces the old flat 1600 m plane.
            var terrain = _terrain;
            const float size = 840f;
            const int cells = 210;
            float step = size / cells;
            float half = size * 0.5f;
            int n = cells + 1;
            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = -half + ix * step, z = -half + iz * step;
                verts[iz * n + ix] = new Vector3(x, (float)terrain.Height(x, z), z);
                uv[iz * n + ix] = new Vector2(ix / (float)cells * 240f, iz / (float)cells * 240f);
            }
            var tris = new int[cells * cells * 6];
            int t = 0;
            for (int iz = 0; iz < cells; iz++)
            for (int ix = 0; ix < cells; ix++)
            {
                int a = iz * n + ix, b = a + 1, c = a + n, d = c + 1;
                // Unity treats clockwise (seen from above) as the front face, so this order is
                // what makes the ground face the sky and the MeshCollider hittable from above.
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
            // Guarantee the surface faces up regardless of the winding convention: sum the
            // geometry normals of every triangle and flip them all only if the surface as a whole
            // points down. A single-vertex test is not enough — mesh.normals[0].y is always
            // positive on this grid, so the old guard never fired even when every triangle faced
            // down, and a MeshCollider only accepts a ray from the side its triangles face.
            double upness = 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 e1 = verts[tris[i + 1]] - verts[tris[i]];
                Vector3 e2 = verts[tris[i + 2]] - verts[tris[i]];
                upness += Vector3.Cross(e1, e2).y;
            }
            if (upness < 0)
            {
                for (int i = 0; i < tris.Length; i += 3)
                    (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
            var mesh = new Mesh { name = "Regolith", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var ground = new GameObject("Regolith");
            ground.transform.SetParent(transform, false);
            ground.isStatic = true;
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            // The CC0 Moon Dusted 03 scan, white under lunar sun, tiled every ~3.5 m.
            var reg = Resources.Load<Texture2D>("Textures/moon_dusted_03_albedo");
            if (reg != null)
            {
                var regMat = new Material(Shader.Find("Standard"))
                {
                    mainTexture = reg,
                    mainTextureScale = new Vector2(240f, 240f),
                    mainTextureOffset = new Vector2(0f, 0f),
                };
                regMat.color = Color.white;
                regMat.metallic = 0f;
                regMat.smoothness = 0.04f;
                regMat.enableInstancing = true;
                regMat.mainTexture.wrapMode = TextureWrapMode.Repeat;
                renderer.sharedMaterial = regMat;
            }
            else
            {
                renderer.sharedMaterial = _regolith;
            }
            renderer.receiveShadows = true;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;

            // A slightly paler apron around the base, so the colony sits on a place.
            var apron = GameObject.CreatePrimitive(PrimitiveType.Plane);
            apron.name = "ColonyApron";
            apron.transform.SetParent(transform, false);
            apron.transform.localPosition = new Vector3(0f, 0.012f, 6f);
            apron.transform.localScale = new Vector3(25f, 1f, 22f);
            apron.GetComponent<Renderer>().sharedMaterial =
                Mat(new Color(0.21f, 0.205f, 0.195f), 0f, 0.95f);
            NoCollider(apron);
        }

        private void BuildPlaza()
        {
            var root = new GameObject("Plaza");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 22f);

            var pave = Mat(new Color(0.205f, 0.215f, 0.235f), 0.12f, 0.86f);
            var edge = Mat(new Color(0.40f, 0.43f, 0.47f), 0.2f, 0.6f);
            var dark = Mat(new Color(0.065f, 0.073f, 0.09f), 0.35f, 0.55f);
            var lamp = Mat(new Color(1f, 0.88f, 0.64f), 0f, 0.4f, new Color(1f, 0.84f, 0.58f), 2.4f);

            Box(root.transform, "Path", new Vector3(0f, 0.055f, 0f), new Vector3(7.4f, 0.11f, 16f), pave);
            for (int i = -3; i <= 3; i++)
                Box(root.transform, "PathLine", new Vector3(0f, 0.117f, i * 2.25f),
                    new Vector3(7.2f, 0.012f, 0.05f), dark);

            for (int s = -1; s <= 1; s += 2)
            {
                Box(root.transform, "PathEdge", new Vector3(s * 3.8f, 0.10f, 0f),
                    new Vector3(0.22f, 0.20f, 16f), edge);
                for (int i = -2; i <= 2; i++)
                {
                    Cylinder(root.transform, "Bollard", new Vector3(s * 3.35f, 0.58f, i * 3f),
                        0.075f, 1.0f, dark);
                    Box(root.transform, "BollardCap", new Vector3(s * 3.35f, 1.12f, i * 3f),
                        new Vector3(0.14f, 0.14f, 0.14f), lamp);
                }
            }

            for (int s = -1; s <= 1; s += 2)
            {
                var lightGo = new GameObject("PlazaLight");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(s * 3.2f, 1.3f, 3f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.84f, 0.58f);
                light.intensity = 1.1f;
                light.range = 7f;
                light.shadows = LightShadows.None;
            }

            // The colony entrance board, facing the spawn.
            var back = Box(root.transform, "BoardBack", new Vector3(0f, 2.1f, -6.7f),
                new Vector3(5.4f, 1.5f, 0.12f), dark);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            MakeText(root.transform, "ColonyBoard", new Vector3(0f, 2.1f, -6.78f),
                0.32f, new Color(0.82f, 0.93f, 1f),
                "LUNA-QUEST · SHACKLETON RIM", "لونا-كوست · حافة شاكلتون", false);
        }

        private void BuildLandingPad()
        {
            var root = new GameObject("LandingPad");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 52f);

            var padMat = Mat(new Color(0.27f, 0.285f, 0.31f), 0.2f, 0.8f);
            var mark = Mat(new Color(0.9f, 0.76f, 0.25f), 0.15f, 0.55f,
                new Color(0.65f, 0.52f, 0.12f), 0.5f);
            Cylinder(root.transform, "Pad", new Vector3(0f, 0.10f, 0f), 13f, 0.20f, padMat);
            for (int i = 0; i < 36; i++)
            {
                float a = i / 36f * Mathf.PI * 2f;
                var marker = Box(root.transform, "PadMark",
                    new Vector3(Mathf.Cos(a) * 9.7f, 0.22f, Mathf.Sin(a) * 9.7f),
                    new Vector3(1.1f, 0.05f, 0.35f), mark);
                marker.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }

            var foil = Mat(new Color(0.86f, 0.64f, 0.18f), 0.8f, 0.30f);
            var ascent = Mat(new Color(0.86f, 0.87f, 0.88f), 0.25f, 0.4f);
            var lander = new GameObject("Lander");
            lander.transform.SetParent(root.transform, false);
            lander.transform.localPosition = new Vector3(0f, 0f, 0f);
            Cylinder(lander.transform, "DescentStage", new Vector3(0f, 1.55f, 0f), 2.7f, 2.8f, foil);
            Box(lander.transform, "AscentStage", new Vector3(0f, 3.75f, 0f),
                new Vector3(2.7f, 2.2f, 2.7f), ascent);
            Box(lander.transform, "Hatch", new Vector3(0f, 1.5f, 1.37f),
                new Vector3(0.8f, 1.0f, 0.06f), _black);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(a) * 3.9f, 0f, Mathf.Sin(a) * 3.9f);
                Beam(lander.transform, "Leg", new Vector3(Mathf.Cos(a) * 1.8f, 1.1f, Mathf.Sin(a) * 1.8f),
                    foot + new Vector3(0f, 0.2f, 0f), 0.10f, ascent);
                Cylinder(lander.transform, "Foot", foot + new Vector3(0f, 0.1f, 0f), 0.55f, 0.12f, _black);
            }
            var antenna = Cylinder(lander.transform, "Antenna", new Vector3(1.0f, 6.0f, 0f), 0.05f, 2.4f, ascent);
            antenna.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

            // F6: the supply crate — a hold-E cargo delivery for the workshop spares desk.
            var crate = Box(root.transform, "SupplyCrate", new Vector3(6.8f, 0.62f, 3.0f),
                new Vector3(1.5f, 1.25f, 1.15f), Mat(new Color(0.42f, 0.35f, 0.27f), 0.75f, 0.05f));
            Box(crate.transform, "CrateStripe", new Vector3(0f, -0.18f, 0f),
                new Vector3(1.52f, 0.18f, 1.17f), Mat(new Color(0.84f, 0.68f, 0.14f), 0.45f, 0.35f,
                    new Color(0.9f, 0.78f, 0.3f), 0.4f));
            var crateStation = crate.AddComponent<Station>();
            crateStation.BuildingId = "";
            crateStation.InteractionId = "cargo_crate";
            crateStation.PromptEn = "Supply Crate";
            crateStation.PromptAr = "صندوق الإمداد";
            crateStation.Kind = "job";
            MakeText(crate.transform, "CrateStencil", new Vector3(0f, 0.75f, 0f),
                0.16f, new Color(0.92f, 0.66f, 0.22f), "SUPPLY", "SUPPLY", true);
        }

        // ---- buildings -------------------------------------------------------

        private void BuildBuilding(BuildingDefinition b)
        {
            var root = new GameObject("B_" + b.Id);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(b.PositionX, 0f, b.PositionZ);
            if (b.Id != "airlock")
            {
                var toCentre = new Vector3(-b.PositionX, 0f, -b.PositionZ);
                if (toCentre.sqrMagnitude > 0.01f)
                    root.transform.rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up);
            }
            _buildings[b.Id] = root.transform;

            if (b.Category == BuildingCategory.Decorative)
            {
                var platform = Box(root.transform, "Platform", new Vector3(0f, 0.12f, 0f),
                    new Vector3(b.FootprintW, 0.24f, b.FootprintD),
                    Mat(new Color(0.3f, 0.31f, 0.33f), 0.2f, 0.7f));
                platform.transform.SetParent(root.transform, true);
                return;
            }

            Color baseColor;
            if (!ColorUtility.TryParseHtmlString(b.ColorHex, out baseColor))
                baseColor = new Color(0.8f, 0.83f, 0.86f);
            var wall = Mat(baseColor, 0.04f, 0.40f);
            var trim = Mat(baseColor * 0.56f, 0.30f, 0.42f);
            var floor = Mat(baseColor * 0.28f, 0.15f, 0.50f);
            var dark = Mat(new Color(0.055f, 0.065f, 0.082f), 0.35f, 0.50f);
            var glass = Mat(new Color(0.09f, 0.36f, 0.58f), 0.25f, 0.16f,
                new Color(0.20f, 0.62f, 0.95f), 0.9f);

            float w = b.FootprintW;
            float d = b.FootprintD;
            float h = b.Height;
            float t = 0.18f;
            float gap = Mathf.Clamp(w * 0.34f, 2.8f, 4.2f);
            float side = (w - gap) * 0.5f;
            float doorH = Mathf.Min(2.7f, h - 0.8f);

            Box(root.transform, "Floor", new Vector3(0f, 0.07f, 0f),
                new Vector3(w, 0.14f, d), floor);
            Box(root.transform, "Roof", new Vector3(0f, h - 0.08f, 0f),
                new Vector3(w, 0.16f, d), trim);

            float wallH = h - 0.16f;
            float wallY = wallH * 0.5f + 0.08f;
            // Front: two solid shoulders and a lintel over the door.
            Box(root.transform, "FrontL", new Vector3(-(gap * 0.5f + side * 0.5f), wallY, d * 0.5f),
                new Vector3(side, wallH, t), wall);
            Box(root.transform, "FrontR", new Vector3(gap * 0.5f + side * 0.5f, wallY, d * 0.5f),
                new Vector3(side, wallH, t), wall);
            Box(root.transform, "Lintel", new Vector3(0f, (doorH + h) * 0.5f - 0.04f, d * 0.5f),
                new Vector3(gap, h - doorH - 0.08f, t), wall);

            // Back: the airlock is a passage and opens both ways.
            if (b.Id == "airlock")
            {
                Box(root.transform, "BackL", new Vector3(-(gap * 0.5f + side * 0.5f), wallY, -d * 0.5f),
                    new Vector3(side, wallH, t), wall);
                Box(root.transform, "BackR", new Vector3(gap * 0.5f + side * 0.5f, wallY, -d * 0.5f),
                    new Vector3(side, wallH, t), wall);
                Box(root.transform, "BackLintel", new Vector3(0f, (doorH + h) * 0.5f - 0.04f, -d * 0.5f),
                    new Vector3(gap, h - doorH - 0.08f, t), wall);
            }
            else
            {
                Box(root.transform, "Back", new Vector3(0f, wallY, -d * 0.5f),
                    new Vector3(w, wallH, t), wall);
            }

            Box(root.transform, "SideL", new Vector3(-w * 0.5f, wallY, 0f),
                new Vector3(t, wallH, d), wall);
            Box(root.transform, "SideR", new Vector3(w * 0.5f, wallY, 0f),
                new Vector3(t, wallH, d), wall);

            // Door frame and warm threshold.
            for (int s = -1; s <= 1; s += 2)
                Box(root.transform, "DoorPost",
                    new Vector3(s * (gap * 0.5f + 0.12f), doorH * 0.5f, d * 0.5f + 0.16f),
                    new Vector3(0.22f, doorH, 0.26f), trim);
            Box(root.transform, "DoorLintel", new Vector3(0f, doorH + 0.06f, d * 0.5f + 0.16f),
                new Vector3(gap + 0.5f, 0.20f, 0.26f), trim);
            Box(root.transform, "DoorGlow", new Vector3(0f, doorH + 0.20f, d * 0.5f + 0.23f),
                new Vector3(gap * 0.52f, 0.055f, 0.04f), _warmGlow);

            // Vertical ribs on the front, and a colour band that breaks up the box.
            int ribs = Mathf.Max(2, Mathf.RoundToInt(w / 2.3f));
            for (int i = 0; i <= ribs; i++)
            {
                float x = -w * 0.5f + 0.35f + i * (w - 0.7f) / ribs;
                if (Mathf.Abs(x) < gap * 0.5f + 0.55f) continue;
                Box(root.transform, "Rib", new Vector3(x, (h - 1.35f) * 0.5f + 0.18f, d * 0.5f + 0.10f),
                    new Vector3(0.11f, h - 1.45f, 0.09f), trim);
            }
            Color accent = b.Id == "airlock" ? new Color(0.93f, 0.50f, 0.10f) : new Color(0.18f, 0.42f, 0.62f);
            Box(root.transform, "FacadeBand", new Vector3(0f, 0.92f, d * 0.5f + 0.105f),
                new Vector3(w - 0.12f, 0.10f, 0.04f), Mat(accent, 0.2f, 0.4f));

            // Lit side windows. They are inside the wall plane, so they glow rather than cut.
            for (int s = -1; s <= 1; s += 2)
                for (int i = -1; i <= 1; i += 2)
                    Box(root.transform, "Window",
                        new Vector3(s * (w * 0.5f + 0.015f), h * 0.62f, i * d * 0.23f),
                        new Vector3(0.035f, 1.05f, 1.45f), glass);

            // Roof machinery and an antenna.
            Box(root.transform, "RoofUnit", new Vector3(w * 0.24f, h + 0.35f, d * 0.18f),
                new Vector3(w * 0.24f, 0.75f, d * 0.24f), trim);
            Cylinder(root.transform, "RoofTank", new Vector3(-w * 0.26f, h + 0.55f, -d * 0.18f),
                Mathf.Min(1.1f, w * 0.10f), 1.1f, _white);
            var mast = Cylinder(root.transform, "RoofMast", new Vector3(w * 0.30f, h + 1.55f, d * 0.22f),
                0.055f, 2.6f, trim);
            Box(root.transform, "Beacon", new Vector3(w * 0.30f, h + 2.87f, d * 0.22f),
                new Vector3(0.16f, 0.16f, 0.16f), _warmGlow);

            // Nameplate.
            float plateW = Mathf.Clamp(b.NameEn.Length * 0.24f + 0.8f, 2.6f, 5.2f);
            float plateY = h - 0.56f;
            var plate = Box(root.transform, "Nameplate", new Vector3(0f, plateY, d * 0.5f + 0.25f),
                new Vector3(plateW, 0.52f, 0.08f), dark);
            plate.transform.localRotation = Quaternion.identity;
            MakeText(root.transform, "NameText", new Vector3(0f, plateY, d * 0.5f + 0.30f),
                0.30f, new Color(0.88f, 0.94f, 1f), b.NameEn, b.NameAr, false);

            // Interior: room mats, low partitions, a table and a light strip.
            BuildInterior(root.transform, b, wall, floor, dark);
            BuildStations(root.transform, b);
            // F2: hide the plain box wall meshes (colliders stay live inside the kit shell).
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t.gameObject.name is "FrontL" or "FrontR" or "Lintel" or "Back" or "BackL" or "BackR"
                    or "BackLintel" or "SideL" or "SideR")
                {
                    var r = t.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                }
            }
            BuildShell(root.transform, b, w, d, h);
            BuildPorch(root.transform, w, d, h);

            BuildInteriorLight(root.transform, b);

            // Warm porch light at the important doors.
            if (b.Id == "habitat" || b.Id == "airlock" || b.Id == "mission_control" ||
                b.Id == "life_support")
            {
                var porchGo = new GameObject("PorchLight");
                porchGo.transform.SetParent(root.transform, false);
                porchGo.transform.localPosition = new Vector3(0f, h - 1.0f, d * 0.5f + 1.6f);
                var porch = porchGo.AddComponent<Light>();
                porch.type = LightType.Point;
                porch.color = new Color(1f, 0.87f, 0.66f);
                porch.intensity = 1.5f;
                porch.range = 8f;
                porch.shadows = LightShadows.None;
            }
        }

        private void BuildInterior(Transform root, BuildingDefinition b, Material wall, Material floor, Material dark)
        {
            int index = 0;
            foreach (var room in b.Rooms)
            {
                Vector3 centre = new Vector3(room.OffsetX, 0.16f, room.OffsetZ);
                Box(root, "RoomFloor", centre + new Vector3(0f, 0.005f, 0f),
                    new Vector3(room.SizeX * 0.82f, 0.02f, room.SizeZ * 0.82f),
                    Mat(Color.Lerp(new Color(0.16f, 0.18f, 0.21f), new Color(0.33f, 0.29f, 0.22f), index * 0.18f),
                        0f, 0.9f));

                float px = room.OffsetX - room.SizeX * 0.5f;
                float pz = room.OffsetZ + room.SizeZ * 0.5f;
                Box(root, "PartitionL", new Vector3(px, 0.78f, room.OffsetZ),
                    new Vector3(0.12f, 1.25f, room.SizeZ * 0.52f), wall);
                Box(root, "PartitionF", new Vector3(room.OffsetX, 0.78f, pz),
                    new Vector3(room.SizeX * 0.42f, 1.25f, 0.12f), wall);

                Box(root, "Table", centre + new Vector3(room.SizeX * 0.22f, 0.35f, -room.SizeZ * 0.16f),
                    new Vector3(1.15f, 0.70f, 0.70f), dark);
                Box(root, "Crate", centre + new Vector3(-room.SizeX * 0.25f, 0.32f, room.SizeZ * 0.20f),
                    new Vector3(0.62f, 0.58f, 0.62f), _orange);

                MakeText(root, "RoomLabel",
                    centre + new Vector3(0f, 1.55f, 0f), 0.12f,
                    new Color(0.72f, 0.83f, 0.94f), room.NameEn, room.NameAr, false);
                index++;
            }
        }

        private void BuildStations(Transform root, BuildingDefinition b)
        {
            foreach (var ip in b.Interactions)
            {
                InteriorRoom room = b.Rooms.Find(r => r.Id == ip.RoomId) ?? (b.Rooms.Count > 0 ? b.Rooms[0] : null);
                if (room == null) continue;

                int inRoom = 0;
                int at = 0;
                foreach (var other in b.Interactions)
                {
                    if (other.RoomId != ip.RoomId) continue;
                    if (other.Id == ip.Id) at = inRoom;
                    inRoom++;
                }

                float spread = inRoom <= 1 ? 0f :
                    (at - (inRoom - 1) * 0.5f) * Mathf.Min(1.65f, room.SizeX / (inRoom + 0.4f));
                Vector3 local = new Vector3(room.OffsetX + spread, 0f, room.OffsetZ - room.SizeZ * 0.16f);

                var console = new GameObject("Station_" + ip.Id);
                console.transform.SetParent(root, false);
                console.transform.localPosition = local;

                Box(console.transform, "Pedestal", new Vector3(0f, 0.48f, 0f),
                    new Vector3(0.72f, 0.96f, 0.42f), _black);
                Box(console.transform, "Console", new Vector3(0f, 1.08f, 0.04f),
                    new Vector3(0.70f, 0.44f, 0.10f), _blueGlow);
                Box(console.transform, "ConsoleLip", new Vector3(0f, 0.88f, 0.16f),
                    new Vector3(0.78f, 0.05f, 0.34f), _orange);
                MakeText(console.transform, "StationLabel", new Vector3(0f, 1.52f, 0f),
                    0.095f, new Color(0.92f, 0.97f, 1f), ip.NameEn, ip.NameAr, false);

                var station = console.AddComponent<Station>();
                station.BuildingId = b.Id;
                station.InteractionId = ip.Id;
                station.PromptEn = ip.NameEn;
                station.PromptAr = ip.NameAr;
                Stations.Add(station);

                // Give the console a trigger volume on the interaction layer so the camera
                // ray can select it and walls can still block the line of sight.
                console.layer = 8;
                var interactCollider = console.AddComponent<BoxCollider>();
                interactCollider.isTrigger = true;
                interactCollider.center = new Vector3(0f, 0.95f, 0.1f);
                interactCollider.size = new Vector3(1.15f, 1.9f, 1.2f);
            }
        }

        private void BuildInteriorLight(Transform root, BuildingDefinition b)
        {
            Box(root, "CeilingStrip", new Vector3(0f, b.Height - 0.22f, 0f),
                new Vector3(b.FootprintW * 0.44f, 0.05f, 0.20f), _warmGlow);

            var lightGo = new GameObject("InteriorLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, b.Height - 0.75f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.93f, 0.80f);
            light.intensity = 1.0f;
            light.range = Mathf.Max(b.FootprintW, b.FootprintD) * 0.55f;
            light.shadows = LightShadows.None;
        }

        // ---- infrastructure --------------------------------------------------

        private void BuildSolarFarm()
        {
            if (!_buildings.TryGetValue("power_farm", out Transform root)) return;
            var panel = Mat(new Color(0.035f, 0.075f, 0.14f), 0.55f, 0.30f,
                new Color(0.03f, 0.12f, 0.25f), 0.5f);
            var frame = Mat(new Color(0.45f, 0.48f, 0.52f), 0.75f, 0.35f);

            for (int row = -1; row <= 1; row++)
                for (int col = -2; col <= 2; col++)
                {
                    var panelGo = Box(root, "SolarPanel",
                        new Vector3(col * 2.75f, 4.62f, row * 3.3f),
                        new Vector3(2.35f, 0.06f, 1.55f), panel);
                    panelGo.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
                    Box(root, "SolarRail", new Vector3(col * 2.75f, 4.46f, row * 3.3f),
                        new Vector3(2.5f, 0.06f, 0.08f), frame);
                }
            MakeText(root, "FarmLabel", new Vector3(0f, 5.7f, 7.3f),
                0.22f, new Color(0.88f, 0.94f, 1f), "SOLAR FARM · 30 kW", "مزرعة الطاقة · 30 كيلوواط", false);
        }

        private void BuildCommsMast()
        {
            if (!_buildings.TryGetValue("comms_tower", out Transform root)) return;
            var metal = Mat(new Color(0.62f, 0.66f, 0.70f), 0.8f, 0.35f);
            var dish = Mat(new Color(0.86f, 0.88f, 0.90f), 0.2f, 0.5f);
            Cylinder(root, "Mast", new Vector3(0f, 10.5f, 0f), 0.16f, 12f, metal);
            var bowl = Sphere(root, "Dish", new Vector3(0f, 14.5f, 0.4f),
                new Vector3(5.2f, 5.2f, 0.75f), dish);
            bowl.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);
            Box(root, "Feed", new Vector3(0f, 16.2f, 1.15f),
                new Vector3(0.25f, 0.25f, 0.25f), _warmGlow);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Beam(root, "GuyWire", new Vector3(0f, 14.8f, 0f),
                    new Vector3(Mathf.Cos(a) * 5.5f, 0f, Mathf.Sin(a) * 5.5f), 0.025f, metal);
            }
        }

        private void BuildRover()
        {
            if (!_buildings.ContainsKey("garage")) return;
            var root = new GameObject("Rover");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-11.5f, 0f, -31.5f);
            root.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);

            var foil = Mat(new Color(0.82f, 0.60f, 0.16f), 0.8f, 0.32f);
            var body = Mat(new Color(0.86f, 0.87f, 0.88f), 0.2f, 0.4f);
            var tyre = Mat(new Color(0.055f, 0.06f, 0.068f), 0.1f, 0.7f);

            Box(root.transform, "Body", new Vector3(0f, 1.05f, 0f),
                new Vector3(2.3f, 0.9f, 3.6f), foil);
            Box(root.transform, "Cabin", new Vector3(0f, 1.95f, -0.4f),
                new Vector3(1.8f, 1.05f, 1.5f), body);
            Box(root.transform, "Windscreen", new Vector3(0f, 2.0f, 0.38f),
                new Vector3(1.45f, 0.62f, 0.05f), _blueGlow);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var wheel = Cylinder(root.transform, "Wheel",
                        new Vector3(sx * 1.35f, 0.55f, sz * 1.25f), 0.52f, 0.36f, tyre);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            MakeText(root.transform, "RoverLabel", new Vector3(0f, 3.0f, 0f),
                0.14f, new Color(0.9f, 0.95f, 1f), "ROVER", "المركبة", false);
        }

        private void BuildIceRig()
        {
            if (!_buildings.TryGetValue("ice_site", out Transform root)) return;
            var blueIce = Mat(new Color(0.32f, 0.62f, 0.80f), 0.1f, 0.2f,
                new Color(0.15f, 0.45f, 0.7f), 0.7f);
            var metal = Mat(new Color(0.58f, 0.62f, 0.66f), 0.75f, 0.4f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Beam(root, "DrillLeg", new Vector3(Mathf.Cos(a) * 2.0f, 0f, Mathf.Sin(a) * 2.0f),
                    new Vector3(Mathf.Cos(a) * 0.7f, 6.0f, Mathf.Sin(a) * 0.7f), 0.09f, metal);
            }
            Cylinder(root, "DrillMast", new Vector3(0f, 4.2f, 0f), 0.14f, 8f, metal);
            Cylinder(root, "DrillBit", new Vector3(0f, 0.9f, 0f), 0.28f, 1.8f, _orange);
            Box(root, "IceBlock", new Vector3(2.2f, 0.38f, -1.8f),
                new Vector3(1.8f, 0.7f, 1.6f), blueIce);
            MakeText(root, "IceLabel", new Vector3(0f, 8.7f, 0f),
                0.18f, new Color(0.72f, 0.9f, 1f), "ICE PROSPECT", "استكشاف الجليد", false);
        }

        private void BuildRegolithPlant()
        {
            if (!_buildings.TryGetValue("regolith_plant", out Transform root)) return;
            var metal = Mat(new Color(0.55f, 0.57f, 0.60f), 0.7f, 0.4f);
            Cylinder(root, "Silo", new Vector3(-7.8f, 3.0f, 2.5f), 1.6f, 6.0f, metal);
            Box(root, "Conveyor", new Vector3(-4.8f, 1.0f, 0.0f),
                new Vector3(4.0f, 0.18f, 0.8f), metal);
            Box(root, "Hopper", new Vector3(-2.6f, 1.6f, 0f),
                new Vector3(1.6f, 1.4f, 1.6f), _black);
            var mound = Sphere(root, "RegolithMound", new Vector3(5.8f, 0.2f, 2.2f),
                new Vector3(6.0f, 1.8f, 4.5f), _regolith);
            NoCollider(mound);
        }

        private void BuildCraters()
        {
            var rim = Mat(new Color(0.235f, 0.23f, 0.225f), 0f, 0.98f);
            var bowl = Mat(new Color(0.105f, 0.103f, 0.10f), 0f, 1f);
            var rng = new System.Random(44);
            for (int c = 0; c < 7; c++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                float dist = 125f + (float)rng.NextDouble() * 150f;
                float radius = 16f + (float)rng.NextDouble() * 24f;
                Vector3 centre = new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);
                var ring = Cylinder(transform, "CraterRim", centre + new Vector3(0f, -0.35f, 0f),
                    radius, 0.7f, rim);
                NoCollider(ring);
                var inside = Cylinder(transform, "CraterFloor", centre + new Vector3(0f, -0.42f, 0f),
                    radius * 0.82f, 0.56f, bowl);
                NoCollider(inside);
            }
        }

        private void BuildRocks()
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                float dist = 55f + (float)rng.NextDouble() * 260f;
                float sx = 0.5f + (float)rng.NextDouble() * 2.4f;
                float sy = 0.35f + (float)rng.NextDouble() * 1.4f;
                float sz = 0.5f + (float)rng.NextDouble() * 2.4f;
                var rock = Sphere(transform, "Rock",
                    new Vector3(Mathf.Cos(a) * dist, sy * 0.35f, Mathf.Sin(a) * dist),
                    new Vector3(sx, sy, sz), _rock);
                rock.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                NoCollider(rock);
            }
        }

        // ---- crew ------------------------------------------------------------

        private void SpawnCrew()
        {
            var crewColours = new[]
            {
                new Color(0.20f, 0.55f, 0.90f), new Color(0.25f, 0.75f, 0.58f),
                new Color(0.85f, 0.55f, 0.20f), new Color(0.65f, 0.45f, 0.85f),
                new Color(0.90f, 0.70f, 0.25f), new Color(0.30f, 0.65f, 0.85f),
                new Color(0.75f, 0.35f, 0.35f), new Color(0.55f, 0.75f, 0.35f),
            };
            int index = 0;
            foreach (var p in _game.Sim.Content.Npcs)
            {
                DoorPoint(p.LocationId, out Vector3 door);
                int slot = Math.Abs(p.Id.GetHashCode()) % 5 - 2;
                Vector3 start = door + new Vector3(slot * 1.1f, 0f, (index % 2) * 0.9f);

                var go = new GameObject("Crew_" + p.Id);
                go.transform.SetParent(transform, false);
                var agent = go.AddComponent<CrewAgent>();
                agent.Init(_game, p.Id, p.DisplayNameEn, p.DisplayNameAr, start);

                var visual = p.IsRobot ? CreateRobot()
                                       : CreateAstronaut(1.75f, crewColours[index % crewColours.Length], false);
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                var label = MakeText(go.transform, "CrewName", new Vector3(0f, 2.18f, 0f),
                    0.115f, new Color(0.9f, 0.96f, 1f), p.DisplayNameEn, p.DisplayNameAr, true);
                agent.Label = label;
                Crew.Add(agent);
                index++;
            }
        }

        /// <summary>NEXO v2: rounded white shell, cyan accent ring, antenna with orange tip,
        /// solar fins, dark curved visor, two glowing eyes (warned-by-signals).</summary>
        private GameObject CreateRobot()
        {
            var root = new GameObject("NEXO");

            var shellMat = Mat(new Color(0.88f, 0.90f, 0.92f), 0.3f, 0.15f);
            var darkTrim = Mat(new Color(0.28f, 0.30f, 0.33f), 0.55f, 0f);
            var cyan = Mat(new Color(0.10f, 0.55f, 0.70f), 0.2f, 0.2f,
                new Color(0.25f, 0.85f, 1.0f), 1.5f);
            var finMat = Mat(new Color(0.10f, 0.16f, 0.30f), 0.55f, 0.25f,
                new Color(0.07f, 0.16f, 0.38f), 0.5f);

            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Shell";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.SetParent(root.transform, false);
            shell.transform.localPosition = new Vector3(0f, 1.12f, 0f);
            shell.transform.localScale = new Vector3(0.88f, 0.86f, 0.86f);
            shell.GetComponent<Renderer>().sharedMaterial = shellMat;

            foreach (float frac in new[] { -0.17f, 0.16f })
            {
                var seam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seam.name = "PanelSeam";
                Destroy(seam.GetComponent<Collider>());
                seam.transform.SetParent(root.transform, false);
                seam.transform.localPosition = new Vector3(0f, 1.12f + frac, 0f);
                seam.transform.localScale = new Vector3(0.87f, 0.02f, 0.87f);
                seam.GetComponent<Renderer>().sharedMaterial = darkTrim;
            }

            var visor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visor.name = "Visor";
            Destroy(visor.GetComponent<Collider>());
            visor.transform.SetParent(root.transform, false);
            visor.transform.localPosition = new Vector3(0f, 1.14f, 0.29f);
            visor.transform.localScale = new Vector3(0.66f, 0.50f, 0.24f);
            visor.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.02f, 0.025f, 0.035f), 0.4f, 0.08f);

            var eyes = new GameObject("NEXO_Eyes");
            eyes.transform.SetParent(root.transform, false);
            foreach (float side in new[] { -1f, 1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0 ? "EyeL" : "EyeR";
                Destroy(eye.GetComponent<Collider>());
                eye.transform.SetParent(eyes.transform, false);
                eye.transform.localPosition = new Vector3(side * 0.135f, 1.18f, 0.42f);
                eye.transform.localScale = new Vector3(0.115f, 0.115f, 0.115f);
                eye.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.05f, 0.30f, 0.38f), 0.2f, 0.1f,
                    new Color(0.30f, 0.85f, 1.0f), 2.2f);
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "WaistRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 1.03f, 0f);
            ring.transform.localScale = new Vector3(0.875f, 0.035f, 0.875f);
            ring.GetComponent<Renderer>().sharedMaterial = cyan;

            foreach (float side in new[] { -1f, 1f })
            {
                var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fin.name = side < 0 ? "FinL" : "FinR";
                Destroy(fin.GetComponent<Collider>());
                fin.transform.SetParent(root.transform, false);
                fin.transform.localPosition = new Vector3(side * 0.48f, 1.34f, 0f);
                fin.transform.localScale = new Vector3(0.02f, 0.22f, 0.34f);
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
                fin.GetComponent<Renderer>().sharedMaterial = finMat;
            }

            var stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stalk.name = "Antenna";
            Destroy(stalk.GetComponent<Collider>());
            stalk.transform.SetParent(root.transform, false);
            stalk.transform.localPosition = new Vector3(0f, 1.66f, 0f);
            stalk.transform.localScale = new Vector3(0.026f, 0.12f, 0.026f);
            stalk.GetComponent<Renderer>().sharedMaterial = shellMat;

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "AntennaTip";
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0f, 1.80f, 0f);
            tip.transform.localScale = new Vector3(0.065f, 0.065f, 0.065f);
            tip.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.9f, 0.45f, 0.08f), 0.2f, 0.1f,
                new Color(1.0f, 0.6f, 0.2f), 1.8f);

            var skirt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            skirt.name = "Skirt";
            Destroy(skirt.GetComponent<Collider>());
            skirt.transform.SetParent(root.transform, false);
            skirt.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            skirt.transform.localScale = new Vector3(0.72f, 0.21f, 0.48f);
            skirt.GetComponent<Renderer>().sharedMaterial = shellMat;

            var thruster = new GameObject("ThrusterGlow");
            thruster.transform.SetParent(root.transform, false);
            thruster.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            var glow = thruster.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.35f, 0.78f, 0.91f);
            glow.range = 2.0f;
            glow.intensity = 1.6f;
            glow.shadows = LightShadows.None;

            var pointer = new GameObject("ObjectivePointer");
            pointer.transform.SetParent(root.transform, false);
            pointer.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "PointerTip";
            Destroy(cone.GetComponent<Collider>());
            cone.transform.SetParent(pointer.transform, false);
            cone.transform.localScale = new Vector3(0.09f, 0.115f, 0.09f);
            cone.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            cone.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.18f, 0.55f, 0.62f), 0.2f, 0.1f,
                new Color(0.25f, 0.85f, 1.0f), 1.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = side < 0 ? "PointerL" : "PointerR";
                Destroy(blade.GetComponent<Collider>());
                blade.transform.SetParent(pointer.transform, false);
                blade.transform.localPosition = new Vector3(side * 0.115f, 0.14f, 0.07f);
                blade.transform.localScale = new Vector3(0.11f, 0.04f, 0.05f);
                blade.transform.localRotation = Quaternion.Euler(0f, side * 41f, 0f);
                blade.GetComponent<Renderer>().sharedMaterial = cone.GetComponent<Renderer>().sharedMaterial;
            }
            return root;
        }
        public bool DoorPoint(string buildingId, out Vector3 point)
        {
            if (_buildings.TryGetValue(buildingId, out Transform root))
            {
                var b = _game.Sim.Content.Building(buildingId);
                float d = b != null ? b.FootprintD : 8f;
                point = root.TransformPoint(new Vector3(0f, 0.05f, d * 0.5f + 2.4f));
                return true;
            }
            point = new Vector3(0f, 0.05f, 18f);
            return false;
        }

        // ---- astronaut -------------------------------------------------------

        public GameObject CreateAstronaut(float targetHeight, Color accent, bool isPlayer)
        {
            var holder = new GameObject(isPlayer ? "CommanderSuit" : "CrewSuit");
            if (_astronaut == null)
            {
                // There is exactly one human character model in Luna-Quest. A missing bake is a
                // broken checkout, not an invitation to draw a different astronaut.
                Debug.LogError("Luna-Quest: the Cute Astronaut bake is missing. Run tools/bake_astronaut_obj.py. No substitute astronaut will be drawn.");
                return holder;
            }
            var model = Instantiate(_astronaut);
            model.name = "ModelRoot";
            model.transform.SetParent(holder.transform, false);

            // The OBJ is baked from the standing 0.4 s pose; the visible sole-to-crown height
            // is 2.311 model units in both engines, not the raw animated AABB.
            float scale = targetHeight / 2.311f;
            model.transform.localScale = new Vector3(scale, scale, scale);
            model.transform.localPosition = new Vector3(0f, 0.004f * scale, 0f);

            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    materials = new[] { _suit };
                }
                if (materials.Length == 1 && renderer.GetComponent<MeshFilter>() != null &&
                    renderer.GetComponent<MeshFilter>().sharedMesh != null &&
                    renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount > 1)
                {
                    int count = renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                    materials = new Material[count];
                    for (int i = 0; i < count; i++) materials[i] = i == 1 ? _visor : _suit;
                }
                for (int i = 0; i < materials.Length; i++)
                {
                    string n = materials[i] != null ? materials[i].name.ToLowerInvariant() : "";
                    materials[i] = n.Contains("vis") ? _visor : _suit;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            AddSuitDetails(model.transform, accent);
            return holder;
        }

        private void AddSuitDetails(Transform model, Color accent)
        {
            var visor = Sphere(model, "VisorGlass", new Vector3(-0.126f, 1.76f, 0.56f),
                new Vector3(0.66f, 0.86f, 0.63f), _visor);
            NoCollider(visor);

            NoCollider(Box(model, "ChestPack", new Vector3(-0.02f, 1.20f, 0.36f),
                new Vector3(0.46f, 0.30f, 0.14f), _white));
            NoCollider(Box(model, "ChestScreen", new Vector3(-0.02f, 1.22f, 0.44f),
                new Vector3(0.22f, 0.10f, 0.02f), _blueGlow));
            NoCollider(Box(model, "CollarTab", new Vector3(-0.02f, 1.38f, 0.39f),
                new Vector3(0.26f, 0.035f, 0.02f), Mat(accent, 0.1f, 0.35f)));
            for (int s = -1; s <= 1; s += 2)
                NoCollider(Box(model, "ChestTab", new Vector3(-0.02f + s * 0.28f, 1.20f, 0.43f),
                    new Vector3(0.05f, 0.14f, 0.03f), _orange));

            // Life-support backpack. The default camera trails the player, so the back
            // needs as much silhouette as the front: a PLSS box, two gas tanks and the
            // orange shoulder straps that read at a distance.
            NoCollider(Box(model, "Backpack", new Vector3(-0.02f, 1.18f, -0.36f),
                new Vector3(0.44f, 0.58f, 0.22f), _pack));
            NoCollider(Box(model, "BackpackPanel", new Vector3(-0.02f, 1.18f, -0.485f),
                new Vector3(0.30f, 0.36f, 0.03f), _visor));
            for (int s = -1; s <= 1; s += 2)
            {
                NoCollider(Cylinder(model, s < 0 ? "TankL" : "TankR",
                    new Vector3(-0.02f + s * 0.27f, 1.18f, -0.50f), 0.08f, 0.40f, _black));
                NoCollider(Box(model, s < 0 ? "PackBandLow" : "PackBandHigh",
                    new Vector3(-0.02f, s < 0 ? 0.93f : 1.43f, -0.49f),
                    new Vector3(0.38f, 0.035f, 0.02f), _orange));
            }
        }

        // ---- labels and language ---------------------------------------------

        private TextMesh MakeText(Transform parent, string name, Vector3 localPosition, float size,
            Color color, string en, string ar, bool billboard)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (!billboard) go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.richText = false;
            if (L.Font != null) mesh.font = L.Font;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && L.Font != null) renderer.sharedMaterial = L.Font.material;

            var label = go.AddComponent<WorldLabel>();
            label.Init(mesh, en, ar, billboard);
            _labels.Add(label);
            return mesh;
        }

        public void RefreshLanguage()
        {
            foreach (var label in _labels)
                if (label != null) label.Apply();
        }

        // ---- primitive helpers ----------------------------------------------

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 pos, float radius, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            Vector3 mid = (a + b) * 0.5f;
            Vector3 delta = b - a;
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            go.transform.localScale = new Vector3(radius * 2f, delta.magnitude * 0.5f, radius * 2f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
        }

        private static void NoCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
