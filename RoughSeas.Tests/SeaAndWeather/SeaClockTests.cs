using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class SeaClockTests
{
    private const double CalendarSpeedMul = 0.5;
    private const double Normal = 60;
    private const double Sleeping = 17060;
    private const double Tick = 1 / 30.0;

    private double hours = 1160.25;
    private double speed = Normal;
    private readonly SeaClock clock;

    public SeaClockTests() => clock = new SeaClock(() => hours, () => CalendarSpeedMul, () => speed);

    private void Run(double realSeconds)
    {
        for (var ran = 0.0; ran < realSeconds; ran += Tick)
        {
            hours += Tick * speed * CalendarSpeedMul / 3600;
            clock.Track(Tick);
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
        Assert.Equal(60, lap.LivedSeconds, 1);
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
        Run(1);

        Assert.Equal(0, watch.Lap().Hours);
    }

    [Fact]
    public void LivesThroughTheHoursItRacesWhileEveryoneSleeps()
    {
        var watch = Started();
        speed = Sleeping;
        Run(0.3);
        speed = Normal;
        Run(0.2);
        var lap = watch.Lap();

        Assert.Equal(lap.Seconds, lap.LivedSeconds, 1);
    }

    [Fact]
    public void LivesNoneOfTheHoursTheCalendarIsMovedOn()
    {
        var watch = Started();
        speed = Sleeping;
        Run(0.25);
        hours += 2;
        Run(0.25);
        var lap = watch.Lap();

        Assert.InRange(lap.Seconds - lap.LivedSeconds, clock.SecondsAt(2) - TwoTicksAt(Sleeping), clock.SecondsAt(2));
    }

    private double TwoTicksAt(double speedOfTime) => clock.SecondsAt(2 * Tick * speedOfTime * CalendarSpeedMul / 3600);

    [Fact]
    public void LivesNoneOfTheHoursAddedWhileTheWorldsClockStands()
    {
        var watch = Started();
        speed = 0;
        hours += 0.1;
        Run(1);
        var lap = watch.Lap();

        Assert.Equal(12, lap.Seconds, 6);
        Assert.InRange(lap.LivedSeconds, 0, TwoTicksAt(Normal));
    }
}
