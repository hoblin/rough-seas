using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaClockTests
{
    private const double CalendarSpeedMul = 0.5;
    private const double Normal = 60;
    private const double Sleeping = 17060;

    private double hours = 1160.25;
    private double speed = Normal;
    private double real = 5000;

    private SeaClock Clock() => new(() => hours, () => CalendarSpeedMul, () => speed, () => real);

    private void Run(double realSeconds)
    {
        real += realSeconds;
        hours += realSeconds * speed * CalendarSpeedMul / 3600;
    }

    private SeaClock.Watch Started()
    {
        var watch = Clock().Start();
        watch.Lap();
        return watch;
    }

    [Fact]
    public void RunsAGameHourInTwoMinutesOfTheSeasSecondsAtVanillasPace()
    {
        var clock = Clock();

        Assert.Equal(120, clock.SecondsAt(1));
        Assert.Equal(hours, clock.HoursAt(clock.SecondsAt(hours)), 9);
    }

    [Fact]
    public void LapsNothingAtItsFirstReading()
    {
        var watch = Clock().Start();
        Run(60);

        Assert.Equal(default, watch.Lap());
    }

    [Fact]
    public void LapsTheTimeTheWorldsClockRan()
    {
        var watch = Started();
        Run(60);

        Assert.Equal(new Lap(0.5, 60, false), watch.Lap());
    }

    [Fact]
    public void LapsNothingWhileTheWorldsClockStands()
    {
        var watch = Started();
        speed = 0;
        Run(60);

        Assert.Equal(0, watch.Lap().Seconds);
    }

    [Fact]
    public void LapsNothingWhenTheWorldsClockIsSetBack()
    {
        var watch = Started();
        hours -= 10;

        Assert.Equal(0, watch.Lap().Hours);
    }

    [Fact]
    public void LivesThroughTheHoursItRacesWhileEveryoneSleeps()
    {
        var watch = Started();
        speed = Sleeping;
        Run(2);
        var asleep = watch.Lap();
        speed = Normal;
        Run(2);

        Assert.False(asleep.Leapt, $"{asleep.Hours:F1} h asleep counted as a leap");
        Assert.False(watch.Lap().Leapt);
    }

    [Fact]
    public void LeapsOverTheHoursTheCalendarIsMovedOn()
    {
        var watch = Started();
        Run(0.5);
        hours += 2;

        Assert.True(watch.Lap().Leapt);
    }

    [Fact]
    public void LeapsOverTheHoursAddedWhileTheWorldsClockStands()
    {
        var watch = Started();
        speed = 0;
        hours += 0.1;

        Assert.True(watch.Lap().Leapt);
    }
}
