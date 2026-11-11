using System;
using Vintagestory.API.Common;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The one clock everything at sea keeps: the world's calendar, so it stands under <c>/time stop</c>, pause and a
/// stopped calendar (<c>/time calendarspeedmul 0</c>), races
/// while players sleep, and leaps with <c>/time add</c>. Reach it as <c>SeaWeatherSystem.Clock</c>, on either side.
/// </summary>
public sealed class SeaClock
{
    private const double VanillaSpeedOfTime = 60;
    private const double SecondsPerGameHour = 3600;
    private const double VanillaCalendarSpeedMul = 0.5;

    private readonly Func<double> hours, calendarSpeedMul, speedOfTime;

    internal SeaClock(Func<double> hours, Func<double> calendarSpeedMul, Func<double> speedOfTime) =>
        (this.hours, this.calendarSpeedMul, this.speedOfTime) = (hours, calendarSpeedMul, speedOfTime);

    internal SeaClock(Func<IGameCalendar?> calendar)
        : this(() => calendar()?.TotalHours ?? 0, () => calendar()?.CalendarSpeedMul ?? VanillaCalendarSpeedMul,
            () => calendar()?.SpeedOfTime ?? 0) { }

    /// <summary>The game time, in total game hours; 0 before the calendar exists.</summary>
    public double Hours => hours();

    /// <summary>
    /// The game time in seconds at vanilla's pace, the unit of what the real world times in seconds (a wave's period,
    /// a leak's litres a second): real seconds at the normal speed of time, 120 to a game hour in a default world.
    /// </summary>
    public double Seconds => SecondsAt(Hours);

    public double SecondsAt(double totalHours) => totalHours * SecondsPerGameHour / (VanillaSpeedOfTime * Pace);

    public double HoursAt(double seconds) => seconds * VanillaSpeedOfTime * Pace / SecondsPerGameHour;

    public double SecondsIn(double realSeconds, double speedOfTime) =>
        SecondsAt(realSeconds * speedOfTime * calendarSpeedMul() / SecondsPerGameHour);

    private double Pace => calendarSpeedMul() is > 0 and var mul ? mul : VanillaCalendarSpeedMul;

    /// <summary>A watch for one mechanic, which keeps its own last lap.</summary>
    public Watch StartWatch() => new(this);

    public sealed class Watch
    {
        private readonly SeaClock clock;
        private double from = double.NaN;

        internal Watch(SeaClock clock) => this.clock = clock;

        /// <summary>The time since the last lap; nothing at the first, nor when the calendar was set back.</summary>
        public Lap Lap()
        {
            var now = clock.Hours;
            var hours = double.IsNaN(from) ? 0 : Math.Max(0, now - from);
            from = now;
            return new Lap(hours, clock.SecondsAt(hours));
        }
    }
}

/// <param name="Hours">In game hours.</param>
/// <param name="Seconds">The same time in seconds at vanilla's pace.</param>
public readonly record struct Lap(double Hours, double Seconds);
