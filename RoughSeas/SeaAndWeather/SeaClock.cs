using System;
using Vintagestory.API.Common;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// The one clock everything at sea keeps: the world's calendar, so it stands under <c>/time stop</c> and pause, races
/// while players sleep, and leaps with <c>/time add</c>. See docs/modding.md for which mechanics keep it.
/// </summary>
public sealed class SeaClock(Func<double> hours, Func<double> calendarSpeedMul, Func<double> speedOfTime, Func<double> realSeconds)
{
    public const double VanillaSpeedOfTime = 60;

    private const double SecondsPerGameHour = 3600;
    private const double VanillaCalendarSpeedMul = 0.5;
    private const double SlackSeconds = 1;

    public SeaClock(Func<IGameCalendar?> calendar)
        : this(() => calendar()?.TotalHours ?? 0, () => calendar()?.CalendarSpeedMul ?? VanillaCalendarSpeedMul,
            () => calendar()?.SpeedOfTime ?? 0, () => Environment.TickCount64 / 1000.0) { }

    /// <summary>The game time, in total game hours; 0 before the calendar exists.</summary>
    public double Hours => hours();

    /// <summary>The game time in seconds at vanilla's pace: real seconds at the normal speed of time.</summary>
    public double Seconds => SecondsAt(Hours);

    public double SecondsAt(double totalHours) => totalHours * SecondsPerGameHour / (VanillaSpeedOfTime * calendarSpeedMul());

    public double HoursAt(double seconds) => seconds * VanillaSpeedOfTime * calendarSpeedMul() / SecondsPerGameHour;

    public Watch Start() => new(this);

    private (double Hours, double Real, double Speed) Read() => (hours(), realSeconds(), speedOfTime());

    private double HoursRun(double realSeconds, double speed) => realSeconds * speed * calendarSpeedMul() / SecondsPerGameHour;

    public sealed class Watch(SeaClock clock)
    {
        private double from = double.NaN;
        private double fromReal;
        private double fromSpeed;

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
/// <param name="Leapt">The calendar was moved on rather than ran, so no tick lived through these hours.</param>
public readonly record struct Lap(double Hours, double Seconds, bool Leapt);
