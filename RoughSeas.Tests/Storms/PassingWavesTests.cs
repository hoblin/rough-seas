using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Water;

namespace RoughSeas.Tests.Storms;

public class PassingWavesTests
{
    private const double Tick = 0.5;
    private const double Period = 8;
    private static readonly Waves Waves = new(Fixtures.Config().Waves);
    private static readonly WaveReading OpenSea = new(2, Period, 0, double.PositiveInfinity, 0, 0, 2);

    private double hours = 1000;
    private readonly SeaClock clock;
    private readonly PassingWaves passing;

    public PassingWavesTests()
    {
        clock = new SeaClock(() => hours, () => 0.5, () => 60);
        passing = new PassingWaves(clock, Waves, Fixtures.Config().Waves.MinHeight);
        Run(Tick, true, OpenSea, 0);
    }

    private List<PassingWave> Run(double seconds, bool afloat, WaveReading here, double windMps)
    {
        var waves = new List<PassingWave>();
        var rand = new Random(7);
        for (var ran = 0.0; ran < seconds; ran += Tick)
        {
            hours += Tick * 60 * 0.5 / 3600;
            if (passing.Lap(afloat)) waves.AddRange(passing.Over(here, windMps, rand));
        }
        return waves;
    }

    [Fact]
    public void PassesOneWaveAPeriodOfTheWorldsClock()
    {
        Assert.InRange(Run(100 * Period, true, OpenSea, 0).Count, 99, 100);
    }

    [Fact]
    public void PassesNoneOutOfTheWaterAndStartsAfreshInIt()
    {
        Run(Period - Tick, true, OpenSea, 0);

        Assert.Empty(Run(10 * Period, false, OpenSea, 0));
        Assert.Empty(Run(Period - 2 * Tick, true, OpenSea, 0));
    }

    [Fact]
    public void BringsTheWavesOfHoursTheCalendarIsMovedOn()
    {
        hours += clock.HoursAt(10 * Period);

        Assert.InRange(Run(Tick, true, OpenSea, 0).Count, 10, 11);
    }

    [Fact]
    public void PassesNoneInASeaTooSmallToCount()
    {
        Assert.Empty(Run(10 * Period, true, OpenSea with { Height = Fixtures.Config().Waves.MinHeight / 2 }, 30));
    }

    [Fact]
    public void BreaksNoneInACalmAtSeaAndEveryOneInSurf()
    {
        var surf = OpenSea with { Depth = 1, Breaking = 1 };

        Assert.DoesNotContain(Run(50 * Period, true, OpenSea, 0), wave => wave.Breaks);
        Assert.All(Run(50 * Period, true, surf, 0), wave => Assert.True(wave.Surf && wave.Breaks));
    }

    [Fact]
    public void BreaksByTheWindsShareAtSea()
    {
        const double wind = 20;
        var waves = Run(2000 * Period, true, OpenSea, wind);

        Assert.InRange(waves.Count(wave => wave.Breaks) / (double)waves.Count, 0.8 * Waves.BreakingChance(wind), 1.2 * Waves.BreakingChance(wind));
    }

    [Fact]
    public void DrawsItsHeightsFromTheSeasRayleighSpread()
    {
        var heights = Run(20000 * Period, true, OpenSea, 0).Select(wave => wave.Height).OrderBy(height => height).ToList();
        var median = WaveTheory.RayleighHeight(OpenSea.Height, 0.5);

        Assert.InRange(heights[heights.Count / 2], 0.97 * median, 1.03 * median);
    }

    [Fact]
    public void RunsABreakerAtTheSpeedItsDepthGives()
    {
        var shallow = OpenSea with { Depth = 1.5, Breaking = 1 };
        var wave = Run(2 * Period, true, shallow, 0)[0];

        Assert.Equal(Waves.BreakerCelerity(wave.Height, Period, shallow.Depth), wave.Celerity, 9);
    }
}
