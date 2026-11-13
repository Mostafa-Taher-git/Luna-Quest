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

        public string DisplayName => L.T(DisplayNameEn, DisplayNameAr);

        public void Init(LunaQuestGame game, string npcId, string nameEn, string nameAr, Vector3 position)
        {
            Game = game;
            NpcId = npcId;
            DisplayNameEn = nameEn;
            DisplayNameAr = nameAr;
            transform.position = position;
            _target = position;
        }

        private void Update()
        {
            if (Game == null || !Game.Started || Game.World == null) return;

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
