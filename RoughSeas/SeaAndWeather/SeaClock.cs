using System;
using Vintagestory.API.Common;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The one clock everything at sea keeps: the world's calendar, so it stands under <c>/time stop</c> and pause, races
/// while players sleep, and leaps with <c>/time add</c>. Reach it as <c>SeaWeatherSystem.Clock</c>, on either side.
/// </summary>
public sealed class SeaClock
{
    internal const double VanillaSpeedOfTime = 60;

    private const double SecondsPerGameHour = 3600;
    private const double VanillaCalendarSpeedMul = 0.5;
    private const double SlackSeconds = 1;

    private readonly Func<double> hours, calendarSpeedMul, speedOfTime, realSeconds;

    internal SeaClock(Func<double> hours, Func<double> calendarSpeedMul, Func<double> speedOfTime, Func<double> realSeconds) =>
        (this.hours, this.calendarSpeedMul, this.speedOfTime, this.realSeconds) = (hours, calendarSpeedMul, speedOfTime, realSeconds);

    internal SeaClock(Func<IGameCalendar?> calendar)
        : this(() => calendar()?.TotalHours ?? 0, () => calendar()?.CalendarSpeedMul ?? VanillaCalendarSpeedMul,
            () => calendar()?.SpeedOfTime ?? 0, () => Environment.TickCount64 / 1000.0) { }

    /// <summary>The game time, in total game hours; 0 before the calendar exists.</summary>
    public double Hours => hours();

    /// <summary>
    /// The game time in seconds at vanilla's pace, the unit of what the real world times in seconds (a wave's period,
    /// a leak's litres a second): real seconds at the normal speed of time, 120 to a game hour in a default world.
    /// </summary>
    public double Seconds => SecondsAt(Hours);

    public double SecondsAt(double totalHours) => totalHours * SecondsPerGameHour / (VanillaSpeedOfTime * calendarSpeedMul());

    public double HoursAt(double seconds) => seconds * VanillaSpeedOfTime * calendarSpeedMul() / SecondsPerGameHour;

    /// <summary>A watch for one mechanic, which keeps its own last lap.</summary>
    public Watch StartWatch() => new(this);

    private (double Hours, double Real, double Speed) Read() => (hours(), realSeconds(), speedOfTime());

    private double HoursRun(double realSeconds, double speed) => realSeconds * speed * calendarSpeedMul() / SecondsPerGameHour;

    public sealed class Watch
    {
        private readonly SeaClock clock;
        private double from = double.NaN;
        private double fromReal;
        private double fromSpeed;

        internal Watch(SeaClock clock) => this.clock = clock;

        /// <summary>The time since the last lap; nothing at the first, nor when the calendar was set back.</summary>
        public Lap Lap()
        {
            var (now, real, speed) = clock.Read();
            var hours = double.IsNaN(from) ? 0 : Math.Max(0, now - from);
            var run = clock.HoursRun(real - fromReal + SlackSeconds, Math.Max(speed, fromSpeed));
            (from, fromReal, fromSpeed) = (now, real, speed);
            return new Lap(hours, clock.SecondsAt(hours), hours > run);
        }
    }
}

/// <param name="Hours">In game hours.</param>
/// <param name="Seconds">The same time in seconds at vanilla's pace.</param>
/// <param name="Leapt">The calendar was moved on, as by <c>/time add</c>, faster than its speed of time runs it, so no tick lived through these hours.</param>
public readonly record struct Lap(double Hours, double Seconds, bool Leapt);
