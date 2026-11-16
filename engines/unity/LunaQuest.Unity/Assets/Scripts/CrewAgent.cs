using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// One crew member walking the schedule in npcs.json. The shared core decides where
    /// they are and how tired they are; this component only moves the suit and the name.
    /// </summary>
    public sealed class CrewAgent : MonoBehaviour
    {
        public LunaQuestGame Game;
        public string NpcId = "";
        public string DisplayNameEn = "";
        public string DisplayNameAr = "";
        public string ActivityEn = "";
        public string ActivityAr = "";
        public TextMesh Label;

        private Vector3 _target;
        private float _repath;
        private float _bob;

        /// <summary>True when this crew agent is NEXO the robot.</summary>
        public bool IsRobot => NpcId == "npc_nexo";

        private static readonly System.Collections.Generic.Dictionary<string, string> ObjectiveTargets = new()
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

        private Vector3 ObjectivePosition()
        {
            foreach (var objective in Game.Sim.Content.Learning)
            {
                if (Game.Sim.Learning.HasEntry(objective.Id)) continue;
                var targetId = ObjectiveTargets.GetValueOrDefault(objective.Id, "habitat");
                var b = Game.Sim.Content.Building(targetId);
                if (b == null) continue;
                var pos = Game.World.HeightAt(b.PositionX, b.PositionZ);
                return new Vector3(b.PositionX, pos, b.PositionZ);
            }
            var mc = Game.Sim.Content.Building("mission_control");
            if (mc == null) return Vector3.zero;
            return new Vector3(mc.PositionX, Game.World.HeightAt(mc.PositionX, mc.PositionZ), mc.PositionZ);
        }

        public string DisplayName => L.T(DisplayNameEn, DisplayNameAr);

        public void Init(LunaQuestGame game, string npcId, string nameEn, string nameAr, Vector3 position)
        {
            Game = game;
            NpcId = npcId;
            DisplayNameEn = nameEn;
            DisplayNameAr = nameAr;
            transform.position = position;
            _target = position;

            // Ray-focus volume on the same interaction layer as the consoles.
            gameObject.layer = 8;
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.center = new Vector3(0f, 1.0f, 0f);
            capsule.height = 1.9f;
            capsule.radius = 0.42f;
        }

        private void Update()
        {
            if (Game == null || !Game.Started || Game.World == null) return;

            // F10: NEXO behaves per the shared robot rules — hover, polite follow, objective pointer,
            // orange eyes in incidents. Same contract in all three engines.
            if (IsRobot)
            {
                var player = Game.Player;
                _bob += Time.deltaTime * 1.6f;
                transform.position += Vector3.up * (Mathf.Sin(Time.timeSinceLevelLoad * 2f) * 0.004f);
                if (player != null)
                {
                    Vector3 toPlayer = player.transform.position - transform.position;
                    toPlayer.y = 0f;
                    float dist = toPlayer.magnitude;
                    if (dist > 4.6f)
                    {
                        Vector3 slot = player.transform.position - toPlayer.normalized * 3.4f;
                        Vector3 delta2 = slot - transform.position;
                        delta2.y = 0f;
                        float step = Mathf.Min(2.6f * Time.deltaTime, delta2.magnitude);
                        transform.position += delta2.normalized * step;
                        transform.position = new Vector3(transform.position.x,
                            0.35f + Mathf.Sin(_bob) * 0.06f, transform.position.z);
                        if (delta2.sqrMagnitude > 0.001f)
                        {
                            Quaternion look = Quaternion.LookRotation(delta2.normalized, Vector3.up);
                            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 4f);
                        }
                    }
                    else
                    {
                        Vector3 hover = new Vector3(transform.position.x,
                            0.35f + Mathf.Sin(_bob) * 0.06f, transform.position.z);
                        transform.position = hover;
                        if (toPlayer.sqrMagnitude > 0.001f)
                        {
                            Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 2.4f);
                        }
                    }
                }
                // Objective pointer — yaws toward the next unfinished objective.
                var pointer = transform.Find("ObjectivePointer");
                if (pointer != null)
                {
                    Vector3 goal = ObjectivePosition();
                    Vector3 to = goal - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                    {
                        float rel = Mathf.Atan2(to.x, to.z) - transform.eulerAngles.y * Mathf.Deg2Rad;
                        pointer.localRotation = Quaternion.Slerp(pointer.localRotation,
                            Quaternion.Euler(0f, rel * Mathf.Rad2Deg, 0f), Time.deltaTime * 3f);
                    }
                }
                // Warning eyes: orange pulse during live incidents.
                bool warn = Game.Sim.Incidents.Active.Exists(a => !a.Resolved && !a.Ignored);
                var eyes = transform.Find("NEXO_Eyes");
                if (eyes != null)
                {
                    foreach (Transform eye in eyes)
                    {
                        var r = eye.GetComponent<Renderer>();
                        if (r == null || r.sharedMaterial == null) continue;
                        if (warn)
                        {
                            float pulse = 1.6f + Mathf.Sin(Time.timeSinceLevelLoad * 7.5f) * 0.9f;
                            r.sharedMaterial.SetColor("_EmissionColor",
                                new Color(1.0f * pulse, 0.55f * pulse, 0.15f * pulse));
                        }
                        else
                        {
                            float pulse = 0.9f + Mathf.Sin(Time.timeSinceLevelLoad * 2.3f) * 0.15f;
                            r.sharedMaterial.SetColor("_EmissionColor",
                                new Color(0.30f * pulse, 0.85f * pulse, 1.0f * pulse));
                        }
                    }
                }
                // Thruster glow: cyan idle, orange pulse during incidents.
                var thruster = transform.Find("ThrusterGlow");
                if (thruster != null)
                {
                    var light = thruster.GetComponent<Light>();
                    if (light != null)
                    {
                        if (warn)
                        {
                            light.color = Color.Lerp(new Color(1.0f, 0.55f + 0.25f * Mathf.Abs(Mathf.Sin(Time.timeSinceLevelLoad * 7f)), 0.15f),
                                Color.white, 0f);
                            light.intensity = 1.1f + Mathf.Abs(Mathf.Sin(Time.timeSinceLevelLoad * 7f)) * 0.8f;
                        }
                        else
                        {
                            light.color = new Color(0.35f, 0.78f, 0.91f);
                            light.intensity = 1.25f + Mathf.Abs(Mathf.Sin(Time.timeSinceLevelLoad * 2.4f)) * 0.25f;
                        }
                    }
                }
                if (Label != null && Camera.main != null)
                    Label.transform.rotation = Camera.main.transform.rotation;
                return;
            }

            _repath -= Time.deltaTime;
            if (_repath <= 0f)
            {
                _repath = 2.0f;
                var state = Game.Sim.Npcs.State(NpcId);
                ActivityEn = state.ActivityEn;
                ActivityAr = state.ActivityAr;
                if (Game.World.DoorPoint(state.LocationId, out Vector3 door))
                    _target = door;
            }

            Vector3 delta = _target - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.36f)
            {
                Vector3 step = delta.normalized * Mathf.Min(0.62f * Time.deltaTime, delta.magnitude - 0.55f);
                transform.position += step;
                Quaternion look = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 4f);
            }
            else
            {
                _bob += Time.deltaTime * 1.6f;
                Transform visual = transform.childCount > 0 ? transform.GetChild(0) : null;
                if (visual != null)
                    visual.localPosition = new Vector3(0f, Mathf.Sin(_bob) * 0.01f, 0f);
            }

            if (Label != null && Camera.main != null)
                Label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
