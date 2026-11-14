using System;
using System.Collections.Generic;
using System.IO;

namespace LunaQuest.Core.Content
{
    public sealed class CropDefinition
    {
        public string Id = "";
        public string NameEn = "";
        public string NameAr = "";
        public double CycleDays = 24;
        public double KcalPerM2 = 450;
        public double WaterLPerM2Day = 3;
        public string Note = "";
    }

    public sealed class LearningObjective
    {
        public string Id = "";
        public string TitleEn = "";
        public string TitleAr = "";
        public string BodyEn = "";
        public string BodyAr = "";
        public string Skill = "";
        public string EvidenceClass = "simplified";
        public string Source = "";
    }

    public sealed class ResearchRecord
    {
        public string Id = "";
        public string Topic = "";
        public string Url = "";
        public string EvidenceClass = "A";
        public string UseEn = "";
        public string UseAr = "";
        public string BoundaryEn = "";
    }

    /// <summary>
    /// Typed, versioned content. The same JSON ships to Unity and Godot, so both engines
    /// read identical rules and identical scientific provenance.
    /// </summary>
    public sealed class ContentDatabase
    {
        public int Version = 1;
        public List<Domain.BuildingDefinition> Buildings = new List<Domain.BuildingDefinition>();
        public List<Domain.NpcProfile> Npcs = new List<Domain.NpcProfile>();
        public List<Domain.IncidentDefinition> Incidents = new List<Domain.IncidentDefinition>();
        public List<CropDefinition> Crops = new List<CropDefinition>();
        public List<LearningObjective> Learning = new List<LearningObjective>();
        public List<ResearchRecord> Research = new List<ResearchRecord>();
        public List<Domain.DialogueTopic> Dialogue = new List<Domain.DialogueTopic>();

        public Domain.BuildingDefinition? Building(string id)
            => Buildings.Find(b => b.Id == id);

        public Domain.NpcProfile? Npc(string id)
            => Npcs.Find(n => n.Id == id);

        public List<Domain.DialogueTopic> TopicsFor(string npcId)
            => Dialogue.FindAll(t => t.NpcId == npcId);

        public LearningObjective? Objective(string id)
            => Learning.Find(l => l.Id == id);

        public static ContentDatabase LoadFromDirectory(string directory)
        {
            var db = new ContentDatabase();
            db.Buildings = LoadBuildings(Path.Combine(directory, "buildings.json"));
            db.Npcs = LoadNpcs(Path.Combine(directory, "npcs.json"));
            db.Incidents = LoadIncidents(Path.Combine(directory, "incidents.json"));
            db.Crops = LoadCrops(Path.Combine(directory, "crops.json"));
            db.Learning = LoadLearning(Path.Combine(directory, "learning.json"));
            db.Research = LoadResearch(Path.Combine(directory, "research.json"));
            db.Dialogue = LoadDialogue(Path.Combine(directory, "dialogue.json"));
            return db;
        }

        public static ContentDatabase LoadFromTexts(
            string buildings, string npcs, string incidents, string crops, string learning, string research,
            string? dialogue = null)
        {
            return new ContentDatabase
            {
                Buildings = ParseBuildings(buildings),
                Npcs = ParseNpcs(npcs),
                Incidents = ParseIncidents(incidents),
                Crops = ParseCrops(crops),
                Learning = ParseLearning(learning),
                Research = ParseResearch(research),
                Dialogue = dialogue == null ? new List<Domain.DialogueTopic>() : ParseDialogue(dialogue),
            };
        }

        // ---- buildings ------------------------------------------------------

        private static List<Domain.BuildingDefinition> LoadBuildings(string path)
            => File.Exists(path) ? ParseBuildings(File.ReadAllText(path)) : new List<Domain.BuildingDefinition>();

        public static List<Domain.BuildingDefinition> ParseBuildings(string json)
        {
            var list = new List<Domain.BuildingDefinition>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("buildings") ? root["buildings"] : null))
            {
                var o = Json.Obj(item);
                var b = new Domain.BuildingDefinition
                {
                    Id = Json.Str(o["id"]),
                    NameEn = Json.Str(o["nameEn"]),
                    NameAr = Json.Str(o["nameAr"]),
                    Category = Json.Str(o["category"]) == "decorative"
                        ? Domain.BuildingCategory.Decorative : Domain.BuildingCategory.Functional,
                    Height = (float)Json.Num(o["height"], 5),
                    ColorHex = Json.Str(o["color"], "#cfd6dc"),
                    PowerKw = Json.Num(o["powerKw"]),
                    WaterLPerDay = Json.Num(o["waterLPerDay"]),
                    O2KgPerDay = Json.Num(o["o2KgPerDay"]),
                    RequiresComms = Json.Bool(o["requiresComms"]),
                };
                var pos = Json.Arr(o["position"]);
                if (pos.Count >= 2) { b.PositionX = (float)Json.Num(pos[0]); b.PositionZ = (float)Json.Num(pos[1]); }
                var fp = Json.Arr(o["footprint"]);
                if (fp.Count >= 2) { b.FootprintW = (float)Json.Num(fp[0]); b.FootprintD = (float)Json.Num(fp[1]); }

                foreach (var r in Json.Arr(o["rooms"]))
                {
                    var ro = Json.Obj(r);
                    var room = new Domain.InteriorRoom
                    {
                        Id = Json.Str(ro["id"]),
                        NameEn = Json.Str(ro["nameEn"]),
                        NameAr = Json.Str(ro["nameAr"]),
                        Purpose = Json.Str(ro["purpose"]),
                    };
                    var size = Json.Arr(ro["size"]);
                    if (size.Count >= 2) { room.SizeX = (float)Json.Num(size[0]); room.SizeZ = (float)Json.Num(size[1]); }
                    var off = Json.Arr(ro["offset"]);
                    if (off.Count >= 2) { room.OffsetX = (float)Json.Num(off[0]); room.OffsetZ = (float)Json.Num(off[1]); }
                    b.Rooms.Add(room);
                }
                foreach (var p in Json.Arr(o["interactions"]))
                {
                    var po = Json.Obj(p);
                    b.Interactions.Add(new Domain.InteractionPoint
                    {
                        Id = Json.Str(po["id"]),
                        RoomId = Json.Str(po["roomId"]),
                        NameEn = Json.Str(po["nameEn"]),
                        NameAr = Json.Str(po["nameAr"]),
                        Kind = Json.Str(po["kind"], "inspect"),
                        LearningObjectiveId = Json.Str(po["learning"]),
                    });
                }
                list.Add(b);
            }
            return list;
        }

        // ---- npcs -----------------------------------------------------------

        private static List<Domain.NpcProfile> LoadNpcs(string path)
            => File.Exists(path) ? ParseNpcs(File.ReadAllText(path)) : new List<Domain.NpcProfile>();

        public static List<Domain.NpcProfile> ParseNpcs(string json)
        {
            var list = new List<Domain.NpcProfile>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("npcs") ? root["npcs"] : null))
            {
                var o = Json.Obj(item);
                var n = new Domain.NpcProfile
                {
                    Id = Json.Str(o["id"]),
                    NameEn = Json.Str(o["nameEn"]),
                    NameAr = Json.Str(o["nameAr"]),
                    RoleEn = Json.Str(o["roleEn"]),
                    RoleAr = Json.Str(o["roleAr"]),
                    ResponsibilitiesEn = Json.Str(o["responsibilitiesEn"]),
                    ResponsibilitiesAr = Json.Str(o["responsibilitiesAr"]),
                    LocationId = Json.Str(o["locationId"]),
                    NameStatus = Json.Str(o["nameStatus"], "approved"),
                    IsRobot = o.ContainsKey("isRobot") && Json.Bool(o["isRobot"]),
                    StorySeedEn = Json.Str(o["storySeedEn"]),
                    StorySeedAr = Json.Str(o["storySeedAr"]),
                    BoundaryEn = Json.Str(o["boundaryEn"]),
                    GreetingEn = Json.Str(o["greetingEn"]),
                    GreetingAr = Json.Str(o["greetingAr"]),
                };
                foreach (var s in Json.Arr(o["skills"])) n.Skills.Add(Json.Str(s));
                foreach (var s in Json.Arr(o["traits"])) n.Traits.Add(Json.Str(s));
                foreach (var s in Json.Arr(o["knownSystems"])) n.KnownSystems.Add(Json.Str(s));
                foreach (var s in Json.Arr(o["relationships"])) n.Relationships.Add(Json.Str(s));
                foreach (var s in Json.Arr(o["schedule"]))
                {
                    var so = Json.Obj(s);
                    n.Schedule.Add(new Domain.ScheduleEntry
                    {
                        StartHour = Json.Num(so["start"]),
                        EndHour = Json.Num(so["end"]),
                        LocationId = Json.Str(so["location"]),
                        ActivityEn = Json.Str(so["activityEn"]),
                        ActivityAr = Json.Str(so["activityAr"]),
                    });
                }
                list.Add(n);
            }
            return list;
        }

        // ---- dialogue -------------------------------------------------------

        private static List<Domain.DialogueTopic> LoadDialogue(string path)
            => File.Exists(path) ? ParseDialogue(File.ReadAllText(path)) : new List<Domain.DialogueTopic>();

        public static List<Domain.DialogueTopic> ParseDialogue(string json)
        {
            var list = new List<Domain.DialogueTopic>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("topics") ? root["topics"] : null))
            {
                var o = Json.Obj(item);
                list.Add(new Domain.DialogueTopic
                {
                    NpcId = Json.Str(o["npcId"]),
                    Id = Json.Str(o["id"]),
                    VoiceKey = Json.Str(o["voiceKey"]),
                    LabelEn = Json.Str(o["labelEn"]),
                    LabelAr = Json.Str(o["labelAr"]),
                    LineEn = Json.Str(o["lineEn"]),
                    LineAr = Json.Str(o["lineAr"]),
                });
            }
            return list;
        }

        // ---- incidents ------------------------------------------------------

        private static List<Domain.IncidentDefinition> LoadIncidents(string path)
            => File.Exists(path) ? ParseIncidents(File.ReadAllText(path)) : new List<Domain.IncidentDefinition>();

        public static List<Domain.IncidentDefinition> ParseIncidents(string json)
        {
            var list = new List<Domain.IncidentDefinition>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("incidents") ? root["incidents"] : null))
            {
                var o = Json.Obj(item);
                list.Add(new Domain.IncidentDefinition
                {
                    Id = Json.Str(o["id"]),
                    Kind = Json.Str(o["kind"], "incident"),
                    Priority = Json.Str(o["priority"], "routine"),
                    Metric = Json.Str(o["metric"]),
                    Comparison = Json.Str(o["op"], "above"),
                    Threshold = Json.Num(o["threshold"]),
                    CooldownMinutes = Json.Num(o["cooldownMin"], 720),
                    OwnerNpcId = Json.Str(o["owner"]),
                    LocationId = Json.Str(o["location"]),
                    TitleEn = Json.Str(o["titleEn"]),
                    TitleAr = Json.Str(o["titleAr"]),
                    BodyEn = Json.Str(o["bodyEn"]),
                    BodyAr = Json.Str(o["bodyAr"]),
                    LearningObjectiveId = Json.Str(o["learning"]),
                    RequiredAction = Json.Str(o["action"]),
                });
            }
            return list;
        }

        // ---- crops / learning / research ------------------------------------

        private static List<CropDefinition> LoadCrops(string path)
            => File.Exists(path) ? ParseCrops(File.ReadAllText(path)) : new List<CropDefinition>();

        public static List<CropDefinition> ParseCrops(string json)
        {
            var list = new List<CropDefinition>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("crops") ? root["crops"] : null))
            {
                var o = Json.Obj(item);
                list.Add(new CropDefinition
                {
                    Id = Json.Str(o["id"]),
                    NameEn = Json.Str(o["nameEn"]),
                    NameAr = Json.Str(o["nameAr"]),
                    CycleDays = Json.Num(o["cycleDays"], 24),
                    KcalPerM2 = Json.Num(o["kcalPerM2"], 450),
                    WaterLPerM2Day = Json.Num(o["waterLPerM2Day"], 3),
                    Note = Json.Str(o["note"]),
                });
            }
            return list;
        }

        private static List<LearningObjective> LoadLearning(string path)
            => File.Exists(path) ? ParseLearning(File.ReadAllText(path)) : new List<LearningObjective>();

        public static List<LearningObjective> ParseLearning(string json)
        {
            var list = new List<LearningObjective>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("learning") ? root["learning"] : null))
            {
                var o = Json.Obj(item);
                list.Add(new LearningObjective
                {
                    Id = Json.Str(o["id"]),
                    TitleEn = Json.Str(o["titleEn"]),
                    TitleAr = Json.Str(o["titleAr"]),
                    BodyEn = Json.Str(o["bodyEn"]),
                    BodyAr = Json.Str(o["bodyAr"]),
                    Skill = Json.Str(o["skill"]),
                    EvidenceClass = Json.Str(o["evidenceClass"], "simplified"),
                    Source = Json.Str(o["source"]),
                });
            }
            return list;
        }

        private static List<ResearchRecord> LoadResearch(string path)
            => File.Exists(path) ? ParseResearch(File.ReadAllText(path)) : new List<ResearchRecord>();

        public static List<ResearchRecord> ParseResearch(string json)
        {
            var list = new List<ResearchRecord>();
            var root = Json.Obj(Json.Parse(json));
            foreach (var item in Json.Arr(root.ContainsKey("research") ? root["research"] : null))
            {
                var o = Json.Obj(item);
                list.Add(new ResearchRecord
                {
                    Id = Json.Str(o["id"]),
                    Topic = Json.Str(o["topic"]),
                    Url = Json.Str(o["url"]),
                    EvidenceClass = Json.Str(o["evidenceClass"], "A"),
                    UseEn = Json.Str(o["useEn"]),
                    UseAr = Json.Str(o["useAr"]),
                    BoundaryEn = Json.Str(o["boundaryEn"]),
                });
            }
            return list;
        }
    }
}
