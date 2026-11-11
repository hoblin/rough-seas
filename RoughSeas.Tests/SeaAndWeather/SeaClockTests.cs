using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaClockTests
{
    private const double Normal = 60;
    private const double Sleeping = 17060;
    private const double Tick = 1 / 30.0;

    private double calendarSpeedMul = 0.5;
    private double hours = 1160.25;
    private double speed = Normal;
    private readonly SeaClock clock;

    public SeaClockTests() => clock = new SeaClock(() => hours, () => calendarSpeedMul, () => speed);

    private void Run(double realSeconds)
    {
        for (var ran = 0.0; ran < realSeconds; ran += Tick)
        {
            hours += Tick * speed * calendarSpeedMul / 3600;
        }
    }

    private SeaClock.Watch Started()
    {
        Run(Tick);
        var watch = clock.StartWatch();
        watch.Lap();
        return watch;
    }

    [Fact]
    public void RunsAGameHourInTwoMinutesOfTheSeasSecondsAtVanillasPace()
    {
        Assert.Equal(120, clock.SecondsAt(1));
        Assert.Equal(hours, clock.HoursAt(clock.SecondsAt(hours)), 9);
    }

    [Fact]
    public void LapsNothingAtItsFirstReading()
    {
        var watch = clock.StartWatch();
        Run(60);

        Assert.Equal(default, watch.Lap());
    }

    [Fact]
    public void LapsTheTimeTheWorldsClockRan()
    {
        var watch = Started();
        Run(60);
        var lap = watch.Lap();

        Assert.Equal(0.5, lap.Hours, 3);
        Assert.Equal(60, lap.Seconds, 1);
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
    public void StandsWhereItStoodWhenTheCalendarIsStopped()
    {
        var watch = Started();
        var stoppedAt = clock.Seconds;
        calendarSpeedMul = 0;
        Run(60);

        Assert.Equal(stoppedAt, clock.Seconds);
        Assert.Equal(default, watch.Lap());
        Assert.Equal(hours, clock.HoursAt(clock.SecondsAt(hours)), 9);
    }

    [Fact]
    public void LapsNothingWhenTheWorldsClockIsSetBack()
    {
        var watch = Started();
        hours -= 10;
        Run(1);

        Assert.Equal(0, watch.Lap().Hours);
    }

    [Fact]
    public void LapsTheHoursItRacesWhileEveryoneSleeps()
    {
        var watch = Started();
        speed = Sleeping;
        Run(0.3);
        var lap = watch.Lap();

        Assert.Equal(clock.SecondsAt(0.3 * Sleeping * calendarSpeedMul / 3600), lap.Seconds, 1);
    }

    [Fact]
    public void LapsTheHoursTheCalendarIsMovedOn()
    {
        var watch = Started();
        hours += 2;

        Assert.Equal(clock.SecondsAt(2), watch.Lap().Seconds, 6);
    }

    [Fact]
    public void LapsEveryStepOfACalendarThatMovesOnlyNowAndThen()
    {
        var watch = Started();
        var lapped = 0.0;
        for (var tick = 1; tick <= 600; tick++)
        {
            if (tick % 6 == 0) hours += 6 * Tick * speed * calendarSpeedMul / 3600;
            lapped += watch.Lap().Seconds;
        }

        Assert.Equal(600 * Tick, lapped, 6);
    }
}
