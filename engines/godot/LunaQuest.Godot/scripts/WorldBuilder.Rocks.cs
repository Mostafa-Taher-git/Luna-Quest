using System.Collections.Generic;
using Godot;
namespace LunaQuest.Godot
{
    /// <summary>
    /// Photoscanned lunar boulders (Poly Haven "Moon Rock 01-07", CC0). The glTF scenes are
    /// loaded once; each variant keeps its own PBR material. The terrain streamer scatters them
    /// per chunk with one MultiMesh per variant, and large boulders get a collider.
    /// </summary>
    public sealed class RockVariant
    {
        public Mesh Mesh = null!;
        public Transform3D Local = Transform3D.Identity;   // node transform inside the glTF
        public float Extent = 1f;                          // largest AABB side, metres
        public float Height = 1f;
        public Vector3 Center;
    }

    public partial class WorldBuilder
    {
        public static readonly List<RockVariant> RealRocks = new List<RockVariant>();

        public static void LoadRealRocks()
        {
            if (RealRocks.Count > 0) return;
            for (int i = 1; i <= 7; i++)
            {
                string id = $"moon_rock_{i:00}";
                string path = $"res://assets/real/rocks/{id}/{id}.gltf";
                if (!ResourceLoader.Exists(path)) continue;
                var scene = GD.Load<PackedScene>(path);
                if (scene == null) continue;
                var inst = scene.Instantiate<Node3D>();
                MeshInstance3D? mi = FindMesh(inst);
                if (mi?.Mesh != null)
                {
                    // Bake the node transform relative to the scene root.
                    Transform3D local = Transform3D.Identity;
                    Node? n = mi;
                    while (n != null && n != inst)
                    {
                        if (n is Node3D n3) local = n3.Transform * local;
                        n = n.GetParent();
                    }
                    var aabb = local * mi.Mesh.GetAabb();
                    var v = new RockVariant
                    {
                        Mesh = mi.Mesh,
                        Local = local,
                        Extent = Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z)),
                        Height = aabb.Size.Y,
                        Center = aabb.GetCenter(),
                    };
                    // Keep the scan's own material on the mesh surface, and make it cast shadows well.
                    RealRocks.Add(v);
                }
                inst.QueueFree();
            }
            GD.Print($"Real rock scans loaded: {RealRocks.Count}");
        }

        private static MeshInstance3D? FindMesh(Node n)
        {
            if (n is MeshInstance3D m) return m;
            foreach (var c in n.GetChildren())
            {
                var r = FindMesh(c);
                if (r != null) return r;
            }
            return null;
        }
    }
}
