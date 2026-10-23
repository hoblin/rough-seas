using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class WaveTimeTests
{
    private const double Frame = 1 / 60.0;
    private const double Normal = 60;
    private const double CalendarSpeedMul = 0.5;
    private const double GameHoursPerSecond = Normal * CalendarSpeedMul / 3600;
    private const double StartHours = 1160.25;

    private static double CalendarHours(double realSeconds) =>
        StartHours + Math.Floor(realSeconds / 0.033) * 0.033 * GameHoursPerSecond;

    private static WaveTime Run(double fromSeconds, double seconds, double speed = Normal)
    {
        var time = new WaveTime();
        for (var t = fromSeconds; t < fromSeconds + seconds; t += Frame) time.Advance(Frame, speed, CalendarHours(t), CalendarSpeedMul);
        return time;
    }

    [Fact]
    public void RunsInRealSecondsAtTheWorldsNormalPace()
    {
        var time = new WaveTime();
        var start = time.Advance(Frame, Normal, StartHours, CalendarSpeedMul);
        var later = time.Advance(Frame, Normal, StartHours + 10 * GameHoursPerSecond, CalendarSpeedMul);

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
        var time = new WaveTime();
        var previous = double.NaN;
        for (var t = 0.0; t < 30; t += Frame)
        {
            var seconds = time.Advance(Frame, Normal, CalendarHours(t), CalendarSpeedMul);
            Assert.True(double.IsNaN(previous) || seconds - previous is >= 0 and < 2 * Frame, $"stepped {seconds - previous:F4} s in a frame");
            previous = seconds;
        }
    }

    [Fact]
    public void StandsStillWhileTheWorldsClockDoes()
    {
        var time = new WaveTime();
        var stopped = time.Advance(Frame, 0, StartHours, CalendarSpeedMul);
        for (var frame = 0; frame < 600; frame++) time.Advance(Frame, 0, StartHours, CalendarSpeedMul);

        Assert.Equal(stopped, time.Seconds);
    }

    [Fact]
    public void SettlesOnTheWorldsTimeWhenTheClockStopsBehindIt()
    {
        var time = Run(0, 5);
        var stoppedAt = StartHours + 4.9 * GameHoursPerSecond;
        for (var frame = 0; frame < 600; frame++) time.Advance(Frame, 0, stoppedAt, CalendarSpeedMul);

        Assert.Equal(time.SecondsAt(stoppedAt), time.Seconds, 3);
    }

    [Fact]
    public void FollowsTheWorldsClockWhenItLeaps()
    {
        var time = Run(0, 5);
        var leapt = time.Advance(Frame, Normal, StartHours + 8, CalendarSpeedMul);

        Assert.Equal(time.SecondsAt(StartHours + 8), leapt);
    }

    [Fact]
    public void TurnsGameHoursIntoItsSecondsAndBack()
    {
        var time = Run(0, 1);

        Assert.Equal(StartHours, time.HoursAt(time.SecondsAt(StartHours)), 9);
    }
}
