using RoughSeas.Soundscape;

namespace RoughSeas.Tests.Soundscape;

public class CrestWatchTests
{
    private const double Step = 0.1;

    private readonly CrestWatch watch = new();

    [Fact]
    public void FindsOneCrestForEachPeriodOfTheSea()
    {
        var crests = Watch(seconds: 41, height: t => 0.5 * Math.Cos(2 * Math.PI * t / 4));
        Assert.Equal(10, crests.Count);
    }

    [Fact]
    public void ReadsAWaveFromItsTroughToItsCrest()
    {
        var crests = Watch(seconds: 20, height: t => 0.5 * Math.Sin(2 * Math.PI * t / 4));
        Assert.All(crests, wave => Assert.Equal(1, wave, 1));
    }

    [Fact]
    public void ReadsEachWaveOfAGroupByItsOwnHeight()
    {
        var crests = Watch(seconds: 60, height: t => (0.4 + 0.3 * Math.Sin(2 * Math.PI * t / 60)) * Math.Sin(2 * Math.PI * t / 3));
        Assert.True(crests.Max() > 1.2 && crests.Min() < 0.6, $"waves of {string.Join(", ", crests.Select(wave => wave.ToString("F2")))}");
    }

    [Fact]
    public void HearsNothingFromASeaThatStandsStill() => Assert.Empty(Watch(seconds: 10, height: _ => 0.3));

    [Fact]
    public void WaitsForATroughBeforeItCountsACrest()
    {
        Assert.Null(watch.Watch(0.2));
        Assert.Null(watch.Watch(0.4));
        Assert.Null(watch.Watch(0.3));
    }

    [Fact]
    public void StartsOverOnceItForgetsTheSea()
    {
        watch.Watch(-0.5);
        watch.Watch(0.5);
        watch.Forget();
        Assert.Null(watch.Watch(0.4));
        Assert.Null(watch.Watch(0.3));
    }

    private List<double> Watch(double seconds, Func<double, double> height)
    {
        var crests = new List<double>();
        for (var t = 0.0; t < seconds; t += Step)
        {
            if (watch.Watch(height(t)) is { } wave) crests.Add(wave);
        }
        return crests;
    }
}
