namespace LunaQuest.Core.Domain
{
    /// <summary>
    /// One selectable crew topic. The voice key is authored here so the dialogue UI and
    /// the generated voice pack cannot drift apart. Lines stay short enough for a subtitle.
    /// </summary>
    public sealed class DialogueTopic
    {
        public string NpcId = "";
        public string Id = "";
        public string VoiceKey = "";
        public string LabelEn = "";
        public string LabelAr = "";
        public string LineEn = "";
        public string LineAr = "";
    }
}
