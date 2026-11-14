using System;
using System.Collections.Generic;
using Godot;
using LunaQuest.Core.Terrain;
namespace LunaQuest.Godot
{
    /// <summary>
    /// Streams the shared lunar surface around the camera. Two levels of detail share one chunk
    /// grid: a 3x3 block of 256 m tiles around the player is drawn as 2 m resolution 64 m chunks
    /// with real collision; a ring of 256 m tiles at 8 m resolution reaches past 1.7 km, which is
    /// further than a person standing on the Moon can see (the horizon is about 2.4 km away).
    /// The height comes from <see cref="LunarTerrain"/>, so Godot and Unity draw the same ground.
    /// </summary>
    public partial class TerrainStreamer : Node3D
    {
        private const int Segments = 32;               // quads per chunk edge
        private const float NearSize = 64f;
        private const float FarSize = 256f;
        private const int FarRadius = 6;               // far tiles around the centre tile
        private const int NearBudget = 8;              // chunks built per frame while streaming
        private const int FarBudget = 3;
        public LunarTerrain Terrain = null!;
        public Material GroundMaterial = null!;
        public Material RockMaterial = null!;
        public Mesh RockMesh = null!;
        private readonly Dictionary<(int, int), Node3D> _near = new Dictionary<(int, int), Node3D>();
        private readonly Dictionary<(int, int), Node3D> _far = new Dictionary<(int, int), Node3D>();
        private readonly List<(int, int)> _nearQueue = new List<(int, int)>();
        private readonly List<(int, int)> _farQueue = new List<(int, int)>();
        private (int, int) _centre = (int.MinValue, int.MinValue);
        private Vector3 _focus;
        public int NearChunkCount => _near.Count;
        public int FarChunkCount => _far.Count;
        public void SetFocus(Vector3 position) => _focus = position;
        /// <summary>Builds everything around the focus before the first frame is shown.</summary>
        public void Prime(Vector3 focus)
        {
            _focus = focus;
            Retarget();
            while (_nearQueue.Count > 0 || _farQueue.Count > 0) Pump(1000, 1000);
        }
        public override void _Process(double delta)
        {
            var centre = (Mathf.FloorToInt(_focus.X / FarSize), Mathf.FloorToInt(_focus.Z / FarSize));
            if (centre != _centre) Retarget();
            Pump(NearBudget, FarBudget);
        }
        private void Retarget()
        {
            _centre = (Mathf.FloorToInt(_focus.X / FarSize), Mathf.FloorToInt(_focus.Z / FarSize));
            int per = (int)(FarSize / NearSize); // 4 near chunks per far tile edge
            var wantNear = new HashSet<(int, int)>();
            var wantFar = new HashSet<(int, int)>();
            for (int fi = -FarRadius; fi <= FarRadius; fi++)
            for (int fj = -FarRadius; fj <= FarRadius; fj++)
            {
                int tx = _centre.Item1 + fi, tz = _centre.Item2 + fj;
                if (Math.Abs(fi) <= 1 && Math.Abs(fj) <= 1)
                {
                    for (int a = 0; a < per; a++)
                    for (int b = 0; b < per; b++)
                        wantNear.Add((tx * per + a, tz * per + b));
                }
                else wantFar.Add((tx, tz));
            }
            Reconcile(_near, wantNear, _nearQueue);
            Reconcile(_far, wantFar, _farQueue);
            // Nearest chunks first, so the ground under the player always exists.
            _nearQueue.Sort((p, q) => Dist(p, NearSize).CompareTo(Dist(q, NearSize)));
            _farQueue.Sort((p, q) => Dist(p, FarSize).CompareTo(Dist(q, FarSize)));
        }
        private float Dist((int, int) c, float size)
        {
            float dx = (c.Item1 + 0.5f) * size - _focus.X, dz = (c.Item2 + 0.5f) * size - _focus.Z;
            return dx * dx + dz * dz;
        }
        private static void Reconcile(Dictionary<(int, int), Node3D> have, HashSet<(int, int)> want, List<(int, int)> queue)
        {
            var drop = new List<(int, int)>();
            foreach (var kv in have)
                if (!want.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var k in drop) { have[k].QueueFree(); have.Remove(k); }
            queue.Clear();
            foreach (var k in want)
                if (!have.ContainsKey(k)) queue.Add(k);
        }
        private void Pump(int nearBudget, int farBudget)
        {
            while (nearBudget-- > 0 && _nearQueue.Count > 0)
            {
                var k = _nearQueue[0]; _nearQueue.RemoveAt(0);
                if (_near.ContainsKey(k)) continue;
                var node = BuildChunk(k.Item1, k.Item2, NearSize, 2f, true);
                AddChild(node);
                _near[k] = node;
            }
            while (farBudget-- > 0 && _farQueue.Count > 0)
            {
                var k = _farQueue[0]; _farQueue.RemoveAt(0);
                if (_far.ContainsKey(k)) continue;
                var node = BuildChunk(k.Item1, k.Item2, FarSize, 8f, false);
                AddChild(node);
                _far[k] = node;
            }
        }
        private Node3D BuildChunk(int cx, int cz, float size, float res, bool near)
        {
            float ox = cx * size + size * 0.5f, oz = cz * size + size * 0.5f;
            var root = new Node3D { Name = $"{(near ? "N" : "F")}_{cx}_{cz}", Position = new Vector3(ox, 0, oz) };
            int n = Segments;
            int w = n + 1;
            // Heights on a grid with a one-vertex border, so normals need no extra samples.
            int bw = n + 3;
            var hb = new float[bw * bw];
            for (int j = 0; j < bw; j++)
            for (int i = 0; i < bw; i++)
            {
                float lx = (i - 1 - n * 0.5f) * res, lz = (j - 1 - n * 0.5f) * res;
                hb[j * bw + i] = (float)Terrain.Height(ox + lx, oz + lz);
            }
            var verts = new Vector3[w * w];
            var norms = new Vector3[w * w];
            var heights = new float[w * w];
            for (int j = 0; j < w; j++)
            for (int i = 0; i < w; i++)
            {
                float h = hb[(j + 1) * bw + (i + 1)];
                float lx = (i - n * 0.5f) * res, lz = (j - n * 0.5f) * res;
                verts[j * w + i] = new Vector3(lx, h, lz);
                heights[j * w + i] = h;
                float hl = hb[(j + 1) * bw + i], hr = hb[(j + 1) * bw + i + 2];
                float hd = hb[j * bw + i + 1], hu = hb[(j + 2) * bw + i + 1];
                norms[j * w + i] = new Vector3(hl - hr, 2f * res, hd - hu).Normalized();
            }
            var idx = new int[n * n * 6];
            int t = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                // Godot front faces are clockwise when seen from above.
                idx[t++] = a; idx[t++] = b; idx[t++] = c;
                idx[t++] = b; idx[t++] = d; idx[t++] = c;
            }
            var arrays = new global::Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            arrays[(int)Mesh.ArrayType.Normal] = norms;
            arrays[(int)Mesh.ArrayType.Index] = idx;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            var mi = new MeshInstance3D
            {
                Mesh = mesh,
                MaterialOverride = GroundMaterial,
                CastShadow = near ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
            // Chunks must not pop out when the camera is near their edge.
            mi.ExtraCullMargin = 64f;
            root.AddChild(mi);
            if (near)
            {
                var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
                body.Scale = new Vector3(res, 1f, res);
                var shape = new HeightMapShape3D { MapWidth = w, MapDepth = w, MapData = heights };
                body.AddChild(new CollisionShape3D { Shape = shape });
                root.AddChild(body);
                ScatterRocks(root, cx, cz, ox, oz);
            }
            return root;
        }
        /// <summary>Boulders and cobbles: deterministic per chunk, none on the colony pad.</summary>
        private void ScatterRocks(Node3D root, int cx, int cz, float ox, float oz)
        {
            if (RockMesh == null) return;
            var rng = new Random(unchecked(cx * 73856093) ^ unchecked(cz * 19349663) ^ Terrain.Seed);
            int count = 5 + rng.Next(0, 9);
            var xf = new List<Transform3D>();
            for (int i = 0; i < count; i++)
            {
                float x = ox + ((float)rng.NextDouble() - 0.5f) * NearSize;
                float z = oz + ((float)rng.NextDouble() - 0.5f) * NearSize;
                double dx = x - Terrain.FlatCenterX, dz = z - Terrain.FlatCenterZ;
                if (Math.Sqrt(dx * dx + dz * dz) < Terrain.FlatRadius * 0.55 && rng.NextDouble() < 0.85) continue;
                float y = (float)Terrain.Height(x, z);
                float sc = 0.25f + (float)Math.Pow(rng.NextDouble(), 3.0) * 2.4f;
                var basis = Basis.Identity.Rotated(Vector3.Up, (float)(rng.NextDouble() * Math.PI * 2.0))
                    .Scaled(new Vector3(sc, sc * 0.6f, sc * (0.8f + (float)rng.NextDouble() * 0.5f)));
                xf.Add(new Transform3D(basis, new Vector3(x - ox, y + sc * 0.12f, z - oz)));
            }
            if (xf.Count == 0) return;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = RockMesh, InstanceCount = xf.Count };
            for (int i = 0; i < xf.Count; i++) mm.SetInstanceTransform(i, xf[i]);
            root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = RockMaterial });
        }
    }
}
