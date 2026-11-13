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
            _regolith = Mat(new Color(0.165f, 0.160f, 0.152f), 0.0f, 0.96f);
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

        // ---- environment -----------------------------------------------------

        private void BuildEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.24f, 0.32f);
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
            fill.color = new Color(0.48f, 0.62f, 0.88f);
            fill.intensity = 0.18f;
            fill.shadows = LightShadows.None;

            // A small unshaded solar disc. At 89.9° S the Sun crawls along the horizon;
            // this is the glare the crew sees all shift, not a noon sun.
            var disc = Sphere(transform, "SunDisc", new Vector3(-300f, 52f, -360f),
                new Vector3(22f, 22f, 22f), Unlit(new Color(1f, 0.94f, 0.78f)));
            NoCollider(disc);

            // Earth: the real day map from Solar System Scope, credited in CREDITS.md.
            var texture = Resources.Load<Texture2D>("Textures/earth");
            _earthMat = Unlit(Color.white, texture);
            var earth = Sphere(transform, "Earth", new Vector3(95f, 118f, -305f),
                new Vector3(26f, 26f, 26f), _earthMat);
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
            var dark = new Color32(3, 4, 10, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = dark;

            var rng = new System.Random(1972);
            // A faint Milky Way band behind the sharp stars.
            for (int i = 0; i < 9000; i++)
            {
                float x = (float)rng.NextDouble() * w;
                float centre = h * 0.52f + Mathf.Sin(x / w * Mathf.PI * 2f) * h * 0.12f;
                float y = centre + (float)(rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * h * 0.11f;
                if (y < 0 || y >= h) continue;
                int idx = (int)y * w + (int)x;
                byte v = (byte)(9 + rng.Next(8));
                pixels[idx] = new Color32(v, v, (byte)(v + 4), 255);
            }
            for (int i = 0; i < 1700; i++)
            {
                int x = rng.Next(w);
                int y = rng.Next(h);
                int idx = y * w + x;
                byte v = (byte)(120 + rng.Next(136));
                pixels[idx] = new Color32(v, v, v, 255);
                if (rng.NextDouble() > 0.72)
                {
                    int x2 = Mathf.Min(w - 1, x + 1);
                    pixels[y * w + x2] = new Color32((byte)(v * 0.7f), (byte)(v * 0.7f), (byte)(v * 0.8f), 255);
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

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Regolith";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(160f, 1f, 160f);
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = _regolith;
            renderer.receiveShadows = true;

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

                var visual = CreateAstronaut(1.72f, crewColours[index % crewColours.Length], false);
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
            var model = _astronaut != null ? Instantiate(_astronaut) : BuildFallbackSuit();
            model.name = "ModelRoot";
            model.transform.SetParent(holder.transform, false);

            // The OBJ is baked from the standing 0.4 s pose; the visible sole-to-crown
            // height is 2.309 model units, not the raw animated AABB.
            float scale = targetHeight / 2.309f;
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

        private GameObject BuildFallbackSuit()
        {
            var root = new GameObject("FallbackSuit");
            var white = _suit;
            var body = Cylinder(root.transform, "Body", new Vector3(0f, 0.85f, 0f), 0.30f, 1.15f, white);
            body.name = "Body";
            var head = Sphere(root.transform, "Helmet", new Vector3(0f, 1.62f, 0f),
                new Vector3(0.56f, 0.58f, 0.56f), white);
            head.name = "Helmet";
            Sphere(root.transform, "FallbackVisor", new Vector3(0f, 1.62f, 0.24f),
                new Vector3(0.40f, 0.32f, 0.24f), _visor);
            Cylinder(root.transform, "LegL", new Vector3(-0.15f, 0.28f, 0f), 0.10f, 0.56f, white);
            Cylinder(root.transform, "LegR", new Vector3(0.15f, 0.28f, 0f), 0.10f, 0.56f, white);
            Cylinder(root.transform, "ArmL", new Vector3(-0.38f, 1.0f, 0f), 0.09f, 0.65f, white);
            Cylinder(root.transform, "ArmR", new Vector3(0.38f, 1.0f, 0f), 0.09f, 0.65f, white);
            foreach (var collider in root.GetComponentsInChildren<Collider>()) Destroy(collider);
            return root;
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
