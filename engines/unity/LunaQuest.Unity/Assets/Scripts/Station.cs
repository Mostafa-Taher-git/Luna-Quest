using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>
    /// A physical station the player can use. The prompt is short on purpose; the
    /// console itself carries the reading and the shared core carries the rule.
    /// </summary>
    public sealed class Station : MonoBehaviour
    {
        public string BuildingId = "";
        public string InteractionId = "";
        public string PromptEn = "";
        public string PromptAr = "";
        public string NpcId = "";

        public string Prompt => L.T(PromptEn, PromptAr);
    }
}
