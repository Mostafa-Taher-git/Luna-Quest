using System;

namespace LunaQuest.Core.Sim
{
    public enum SimEventKind { Info, Warning, Critical, Discovery, Dialogue, Mission }

    /// <summary>
    /// A single thing the player can see. Every event has an id, a cause and a safe next step;
    /// the UI must never show a bare number (GDD §12.3).
    /// </summary>
    public sealed class SimEvent
    {
        public string Id = "";
        public SimEventKind Kind = SimEventKind.Info;
        public string TitleEn = "";
        public string TitleAr = "";
        public string BodyEn = "";
        public string BodyAr = "";
        public string SpeakerId = "";
        public string LocationId = "";
        public double GameMinute;

        public static SimEvent Make(string id, SimEventKind kind, string titleEn, string titleAr,
            string bodyEn, string bodyAr, double gameMinute, string speakerId = "", string locationId = "")
            => new SimEvent
            {
                Id = id, Kind = kind, TitleEn = titleEn, TitleAr = titleAr,
                BodyEn = bodyEn, BodyAr = bodyAr, GameMinute = gameMinute,
                SpeakerId = speakerId, LocationId = locationId
            };

        public override string ToString() => $"[{Kind}] {TitleEn}";
    }
}
