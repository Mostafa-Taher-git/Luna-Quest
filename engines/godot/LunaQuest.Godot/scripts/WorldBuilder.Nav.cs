using System.Collections.Generic;
using Godot;
namespace LunaQuest.Godot
{
    /// <summary>
    /// Crew navigation. A navigation mesh is baked once at start-up from the real colliders
    /// (module walls with their door gaps, interior partitions, kit props and the terrain), so
    /// crew members walk through doorways and around rovers instead of through geometry.
    /// Only layers 1 (structure, terrain) and 4 (props) are parsed; crew bodies (layer 8) are not.
    /// </summary>
    public partial class WorldBuilder
    {
        public const string NavGroup = "nav_source";
        private NavigationRegion3D? _navRegion;
        public bool NavReady { get; private set; }
        public Rid NavMap => GetWorld3D().NavigationMap;

        private void BuildNavigation()
        {
            AddToGroup(NavGroup);
            var mesh = new NavigationMesh
            {
                GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
                GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.GroupsWithChildren,
                GeometrySourceGroupName = NavGroup,
                GeometryCollisionMask = 1 | 4,
                CellSize = 0.25f,
                CellHeight = 0.25f,
                AgentRadius = 0.5f,
                AgentHeight = 1.8f,
                AgentMaxClimb = 0.4f,
                AgentMaxSlope = 32f,
                RegionMinSize = 4f,
                FilterBakingAabb = new Aabb(new Vector3(-90, -6, -72), new Vector3(180, 30, 150)),
            };
            _navRegion = new NavigationRegion3D { Name = "CrewNavigation", NavigationMesh = mesh };
            AddChild(_navRegion);
            _navRegion.BakeFinished += () =>
            {
                NavReady = true;
                int polys = _navRegion.NavigationMesh.GetPolygonCount();
                GD.Print($"Crew navigation baked: {polys} polygons.");
            };
            // Wait one physics frame so every collider is registered, then bake on a thread.
            GetTree().CreateTimer(0.2, true, true).Timeout += () => _navRegion.BakeNavigationMesh(true);
        }

        /// <summary>Path on the baked mesh, or null before the bake has finished.</summary>
        public List<Vector3>? NavPath(Vector3 from, Vector3 to)
        {
            if (!NavReady) return null;
            var pts = NavigationServer3D.MapGetPath(NavMap, from, to, true);
            if (pts.Length < 2) return null;
            // Reject a path that ends far from the goal (target outside the mesh).
            if (pts[pts.Length - 1].DistanceTo(to) > 2.0f) return null;
            return new List<Vector3>(pts);
        }
    }
}
