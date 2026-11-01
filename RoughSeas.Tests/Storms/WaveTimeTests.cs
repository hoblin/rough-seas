using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class WaveTimeTests
{
    private const double Frame = 1 / 60.0;
    private const double Normal = 60;
    private const double CalendarSpeedMul = 0.5;
    private const double GameHoursPerSecond = Normal * CalendarSpeedMul / 3600;
    private const double StartHours = 1160.25;

    private double hours = StartHours;
    private double calendarSpeedMul = CalendarSpeedMul;

    private SeaClock Clock => new(() => hours, () => calendarSpeedMul, () => Normal);

    private static double CalendarHours(double realSeconds) =>
        StartHours + Math.Floor(realSeconds / 0.033) * 0.033 * GameHoursPerSecond;

    private WaveTime Run(double fromSeconds, double seconds, double speed = Normal)
    {
        var time = new WaveTime(Clock);
        for (var t = fromSeconds; t < fromSeconds + seconds; t += Frame) Advance(time, CalendarHours(t), speed);
        return time;
    }

    private double Advance(WaveTime time, double calendarHours, double speed = Normal)
    {
        hours = calendarHours;
        return time.Advance(Frame, speed);
    }

    [Fact]
    public void RunsInRealSecondsAtTheWorldsNormalPace()
    {
        var time = new WaveTime(Clock);
        var start = Advance(time, StartHours);
        var later = Advance(time, StartHours + 10 * GameHoursPerSecond);

        Assert.Equal(10, later - start, 2);
    }

    [Fact]
    public void ReadsTheSameTimeOnClientsThatJoinedAtDifferentMoments()
    {
        var early = Run(0, 600);
        var late = Run(590, 10);

        Assert.Equal(early.Seconds, late.Seconds, 1);
    }

    [Fact]
    public void RunsSmoothlyOverTheCalendarsSteps()
    {
        var time = new WaveTime(Clock);
        var previous = double.NaN;
        for (var t = 0.0; t < 30; t += Frame)
        {
            var seconds = Advance(time, CalendarHours(t));
            Assert.True(double.IsNaN(previous) || seconds - previous is >= 0 and < 2 * Frame, $"stepped {seconds - previous:F4} s in a frame");
            previous = seconds;
        }
    }

    [Fact]
    public void StandsStillWhileTheWorldsClockDoes()
    {
        var time = new WaveTime(Clock);
        var stopped = Advance(time, StartHours, 0);
        for (var frame = 0; frame < 600; frame++) Advance(time, StartHours, 0);

        Assert.Equal(stopped, time.Seconds);
    }

    [Fact]
    public void SettlesOnTheWorldsTimeWhenTheClockStopsBehindIt()
    {
        var time = Run(0, 5);
        var stoppedAt = StartHours + 4.9 * GameHoursPerSecond;
        for (var frame = 0; frame < 600; frame++) Advance(time, stoppedAt, 0);

        Assert.Equal(Clock.SecondsAt(stoppedAt), time.Seconds, 3);
    }

    [Fact]
    public void SettlesOnTheWorldsTimeWhenTheCalendarIsStopped()
    {
        var time = Run(0, 5);
        var stoppedAt = StartHours + 4.9 * GameHoursPerSecond;
        calendarSpeedMul = 0;
        for (var frame = 0; frame < 600; frame++) Advance(time, stoppedAt);

        Assert.Equal(Clock.SecondsAt(stoppedAt), time.Seconds, 3);
    }

    [Fact]
    public void FollowsTheWorldsClockWhenItLeaps()
    {
        var time = Run(0, 5);
        var leapt = Advance(time, StartHours + 8);

        Assert.Equal(Clock.SecondsAt(StartHours + 8), leapt);
    }
}
