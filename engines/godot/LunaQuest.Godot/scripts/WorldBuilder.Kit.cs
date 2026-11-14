using System;
using System.Collections.Generic;
using Godot;
namespace LunaQuest.Godot
{
    /// <summary>
    /// Exterior set dressing from the generated colony kit (assets/models/lunar_colony). The
    /// walkable modules and their interiors stay procedural, because the interiors are tied
    /// to the simulation; everything around them (landers, power yard, skids, rovers, tanks)
    /// comes from the kit, so the colony reads as a working industrial site, not as boxes.
    /// Every prop gets a box collider so the commander cannot walk through a battery container.
    /// </summary>
    public partial class WorldBuilder
    {
        private const string KitDir = "res://assets/models/lunar_colony/";
        private readonly Dictionary<string, PackedScene?> _kitCache = new Dictionary<string, PackedScene?>();
        public int KitPropCount { get; private set; }

        // (file stem, x, z, yaw degrees, scale)
        private static readonly (string stem, float x, float z, float yaw, float scale)[] KitLayout =
        {
            // Landing apron: a cargo lander on the pad, guide beacons and a blast wall in front of the plaza.
            ("cargo-lander-94227716",           0f,  52f,    0f, 1.0f),
            ("approach-light-beacon-20fb9bc6", -11f, 44f,    0f, 1.0f),
            ("approach-light-beacon-20fb9bc6", 9f, 46f, 0f, 1.0f),
            ("approach-light-beacon-20fb9bc6", -11f, 62f,    0f, 1.0f),
            ("approach-light-beacon-20fb9bc6",  11f, 62f,    0f, 1.0f),
            ("blast-deflector-wall-51ed7365",  -22f, 52f,   90f, 1.0f),
            ("blast-deflector-wall-51ed7365",   22f, 52f,  -90f, 1.0f),
            ("cargo-hauler-flatbed-76ce4ade", -14f, 36f, 20f, 1.0f),
            ("equipment-sled-408c66bc", -10f, 42f, 30f, 1.0f),
            ("propellant-tank-farm-3dcbfa21",  -30f, 50f,   90f, 1.0f),
            ("cryo-transfer-arm-5d350240",     -24f, 40f,    0f, 1.0f),
            // Power yard behind the solar farm.
            ("battery-container-9755ed67",     -66f, -14f,  90f, 1.0f),
            ("battery-container-9755ed67",     -66f, -20f,  90f, 1.0f),
            ("radiator-bank-0c1ce93e",         -52f,  16f,   0f, 1.0f),
            ("nuclear-power-unit-8239903e",    -72f, -32f,   0f, 1.0f),
            ("vertical-solar-tower-c58cca7d", -68f, 10f, 0f, 1.0f),
            // Resource processing: regolith to oxygen, ice to water.
            ("oxygen-extraction-plan-002fb0ae", -46f, 19f,   0f, 1.0f),
            ("regolith-excavator-64b4560f",    -56f,  22f,  35f, 1.0f),
            ("sintering-print-gantry-64cc192a", -40f, 30f,   0f, 1.0f),
            ("water-processing-skid-27dcfe03",  42f,  26f,   0f, 1.0f),
            ("greenhouse-module-b35be615",      46f, -14f,  90f, 1.0f),
            ("survey-marker-array-99d264a2", 58f, -44f, 0f, 1.0f),
            ("regolith-hopper-screen-148a2c37", 63f, -24f, -30f, 1.0f),
            // Motor pool in front of the garage and a relay mast beside mission control.
            ("crane-rover-d5a76830", -34f, -30f, 25f, 1.0f),
            ("pressurised-long-range-35466e96", -28f, -46f, 30f, 1.0f),
            ("small-survey-rover-f1fa6584", -24f, -52f, 0f, 1.0f),
            ("eva-tool-cart-3f077d9d", -30f, -38f, 60f, 1.0f),
            ("comms-relay-mast-4cdfe54f", 26f, -30f, 0f, 1.0f),
            ("open-rover-unpressuris-0da93181", -12f, 22f, 160f, 1.0f),
            // Protective berms on the open flanks.
            ("dust-berm-2dec8cfd",              62f,  12f,   90f, 1.0f),
            ("dust-berm-2dec8cfd",             -62f,  30f,  -90f, 1.0f),
        };

        private void BuildKitDressing()
        {
            var root = new Node3D { Name = "KitDressing" };
            AddChild(root);
            foreach (var item in KitLayout)
            {
                var scene = LoadKit(item.stem);
                if (scene == null) continue;
                var node = scene.Instantiate<Node3D>();
                node.Name = item.stem;
                node.Position = new Vector3(item.x, (float)Terrain.Height(item.x, item.z), item.z);
                node.RotationDegrees = new Vector3(0, item.yaw, 0);
                node.Scale = Vector3.One * item.scale;
                root.AddChild(node);
                AddKitCollider(node);
                KitPropCount++;
            }
            GD.Print($"Kit dressing: {KitPropCount} props placed.");
        }

        private PackedScene? LoadKit(string stem)
        {
            if (_kitCache.TryGetValue(stem, out var cached)) return cached;
            string path = KitDir + stem + ".glb";
            PackedScene? scene = ResourceLoader.Exists(path) ? ResourceLoader.Load<PackedScene>(path) : null;
            if (scene == null) GD.PushWarning($"Kit model missing or not imported: {path}");
            _kitCache[stem] = scene;
            return scene;
        }

        /// <summary>One box collider around the visible meshes, in the prop's own space.</summary>
        private static void AddKitCollider(Node3D node)
        {
            bool any = false;
            Aabb box = new Aabb();
            foreach (var child in node.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)child;
                var local = node.GlobalTransform.AffineInverse() * mi.GlobalTransform;
                var a = local * mi.GetAabb();
                if (!any) { box = a; any = true; } else box = box.Merge(a);
            }
            if (!any || box.Size.X < 0.4f || box.Size.Z < 0.4f) return;
            var body = new StaticBody3D { Name = "Collider", CollisionLayer = 4, CollisionMask = 0 };
            var shape = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(box.Size.X * 0.92f, Mathf.Max(box.Size.Y, 0.5f), box.Size.Z * 0.92f) },
                Position = box.GetCenter(),
            };
            body.AddChild(shape);
            node.AddChild(body);
        }
    }
}
