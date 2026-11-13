using System.Collections.Generic;

namespace LunaQuest.Core.Domain
{
    public enum BuildingCategory { Functional, Decorative }

    /// <summary>A real interior room. Functional buildings must have at least one.</summary>
    public sealed class InteriorRoom
    {
        public string Id = "";
        public string NameEn = "";
        public string NameAr = "";
        public string Purpose = "";
        public float SizeX = 6f;
        public float SizeZ = 6f;
        public float OffsetX;
        public float OffsetZ;
    }

    public sealed class InteractionPoint
    {
        public string Id = "";
        public string RoomId = "";
        public string NameEn = "";
        public string NameAr = "";
        /// <summary>"inspect" | "clean" | "repair" | "harvest" | "read" | "talk" | "use"</summary>
        public string Kind = "inspect";
        public string LearningObjectiveId = "";
    }

    /// <summary>
    /// Shared definition of a building. The exterior mesh, interior scene and interactions are
    /// engine adapters; identity, costs, utilities and acceptance rules live here (GDD §21).
    /// </summary>
    public sealed class BuildingDefinition
    {
        public string Id = "";
        public string NameEn = "";
        public string NameAr = "";
        public BuildingCategory Category = BuildingCategory.Functional;
        public float PositionX;
        public float PositionZ;
        public float FootprintW = 8f;
        public float FootprintD = 8f;
        public float Height = 5f;
        public string ColorHex = "#cfd6dc";

        public List<InteriorRoom> Rooms = new List<InteriorRoom>();
        public List<InteractionPoint> Interactions = new List<InteractionPoint>();

        public double PowerKw;
        public double WaterLPerDay;
        public double O2KgPerDay;
        public bool RequiresComms;

        /// <summary>GDD §5 acceptance rule: no functional building without a real interior.</summary>
        public bool IsValid => Category == BuildingCategory.Decorative || (Rooms.Count > 0 && Interactions.Count >= 1);

        public bool HasInterior => Rooms.Count > 0;
    }
}
