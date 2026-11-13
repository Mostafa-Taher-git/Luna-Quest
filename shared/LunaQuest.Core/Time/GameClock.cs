using System;

namespace LunaQuest.Core.Time
{
    /// <summary>
    /// Deterministic game clock. One lunar synodic month (29.53059 Earth days) is compressed
    /// into a playable session; the mission itself is authored as 30 Earth days.
    ///
    /// The colony sits on the Shackleton rim (89.9° S). There the Sun never rises high:
    /// it circles the horizon, so elevation stays within roughly ±2°, giving long shadows and
    /// near-continuous but grazing illumination. That is real; the compression is the simulation.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>Real seconds -> simulated minutes. 12 min/s makes a 30-day mission 60 real minutes.</summary>
        public const double MinutesPerSecond = 12.0;

        /// <summary>Synodic month (new moon to new moon) in Earth days. Source: NASA Moon Facts.</summary>
        public const double SynodicDays = 29.53059;

        /// <summary>Authored mission length in Earth days.</summary>
        public const double MissionDays = 30.0;

        public const double MinutesPerDay = 1440.0;

        /// <summary>Simulated minutes since mission start.</summary>
        public double TotalMinutes { get; private set; }

        public double TotalSeconds => TotalMinutes * 60.0;

        /// <summary>1-based mission day (1..30).</summary>
        public int Day => (int)Math.Floor(TotalMinutes / MinutesPerDay) + 1;

        public double DayFraction => (TotalMinutes % MinutesPerDay) / MinutesPerDay;

        public double HourOfDay => DayFraction * 24.0;

        /// <summary>0..1 through the synodic month; drives the Sun's slow circle of the sky.</summary>
        public double LunarPhase => (TotalMinutes / MinutesPerDay % SynodicDays) / SynodicDays;

        /// <summary>Mission progress 0..1.</summary>
        public double MissionProgress => Math.Min(1.0, TotalMinutes / (MissionDays * MinutesPerDay));

        public bool IsMissionComplete => Day > (int)MissionDays;

        /// <summary>
        /// Solar elevation in degrees for a carefully chosen Shackleton-rim site during polar
        /// summer. The Moon's 1.54° axial tilt keeps the Sun crawling along the horizon; here it
        /// stays just above it, which is why this site was chosen for the colony. Terrain
        /// shadowing is represented by the slow illumination curve, not a hard day/night switch.
        /// </summary>
        public double SunElevationDeg
        {
            get
            {
                double tilt = 1.54 * Math.Sin(2.0 * Math.PI * LunarPhase - Math.PI / 2.0);
                return 1.35 + tilt * 0.85;
            }
        }

        public double SunAzimuthDeg => (LunarPhase * 360.0 + 180.0) % 360.0;

        /// <summary>
        /// Fraction of full solar irradiance reaching the farm. The array is vertical and tracks
        /// azimuth, so a grazing Sun still lands close to normal incidence; the remaining loss is
        /// terrain shadowing and the low elevation itself. Full noon power never happens here.
        /// </summary>
        public double SolarIrradianceFraction
        {
            get
            {
                double seasonal = 0.80 + 0.16 * Math.Sin(2.0 * Math.PI * (LunarPhase - 0.15));
                return Math.Max(0.0, Math.Min(1.0, seasonal));
            }
        }

        public bool IsDaylight => SunElevationDeg > 0.0;

        /// <summary>Human-readable mission time, e.g. "Day 4 · 13:20".</summary>
        public string Stamp()
        {
            int h = (int)Math.Floor(HourOfDay);
            int m = (int)Math.Floor((HourOfDay - h) * 60.0);
            return $"Day {Day} · {h:00}:{m:00}";
        }

        public void Step(double realSeconds)
        {
            if (realSeconds <= 0.0) return;
            TotalMinutes += realSeconds * MinutesPerSecond;
        }

        public void SetTotalMinutes(double minutes) => TotalMinutes = Math.Max(0.0, minutes);

        public void LoadFrom(double minutes) => TotalMinutes = Math.Max(0.0, minutes);
    }
}
