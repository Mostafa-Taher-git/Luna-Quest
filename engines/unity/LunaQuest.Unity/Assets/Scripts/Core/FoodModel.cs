using System;
using LunaQuest.Core.Time;

namespace LunaQuest.Core.Sim
{
    /// <summary>
    /// Food is a consumable, not a score. Nine crew at roughly 2,800 kcal each need about
    /// 25,200 kcal per Earth day, which is why stored food carries the mission and the
    /// greenhouse is a fresh-food supplement rather than a full diet.
    /// </summary>
    public static class FoodModel
    {
        public const double KcalPerPersonDay = 2800.0;

        public static void Step(ColonyState s, GameClock clock, double realSeconds)
        {
            double gameDays = realSeconds * GameClock.MinutesPerSecond / GameClock.MinutesPerDay;
            double need = KcalPerPersonDay * s.CrewCount * gameDays;

            if (s.FoodKcal >= need)
            {
                s.FoodKcal -= need;
                s.FoodShortage = false;
                s.FoodShortageDays = Math.Max(0.0, s.FoodShortageDays - gameDays);
            }
            else
            {
                double shortfall = need - s.FoodKcal;
                s.FoodKcal = 0.0;
                s.FoodShortage = true;
                s.FoodShortageDays += gameDays;
                // A shortfall is not a death screen: the crew slows down and morale falls.
                s.AverageFatigue = Math.Min(1.0, s.AverageFatigue + gameDays * 0.10);
                if (shortfall > 0 && s.AverageMorale > 0.2)
                    s.AverageMorale = Math.Max(0.0, s.AverageMorale - gameDays * 0.05);
            }
        }

        /// <summary>Days of food left at the current crew size.</summary>
        public static double DaysRemaining(ColonyState s)
            => s.FoodKcal / (KcalPerPersonDay * Math.Max(1, s.CrewCount));
    }
}
