using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class HullResponseTests
{
    private const double FrameSeconds = 1 / 60.0;

    private readonly ResponseConfig config = Fixtures.Config().Response;

    [Fact]
    public void AHeavierHullHeavesAndRollsSlower()
    {
        Assert.True(HullResponse.HeavePeriod(0.6, config) > HullResponse.HeavePeriod(0.2, config));
        Assert.True(HullResponse.RollPeriod(6, 2.5, 0.64, 1.14) > HullResponse.RollPeriod(4.5, 0.75, 0.2, 0.41));
    }

    [Fact]
    public void ALightHullRidesALongSwellAsTheWaterLies()
    {
        var canoe = HullResponse.Of(4.5, 0.75, 0.04, 0.29, config);

        Assert.InRange(Peak(canoe, periodSeconds: 10, seconds: 40), 0.95, 1.1);
    }

    [Fact]
    public void AHeavyHullLetsAShortChopPassUnderHer()
    {
        var heavy = new HullResponse(4, 6, config);

        Assert.True(Peak(heavy, periodSeconds: 1.5, seconds: 40) < 0.5);
    }

    [Fact]
    public void AHullBackOnTheWaterAfterALongGapStaysNearTheWater()
    {
        var canoe = HullResponse.Of(4.5, 0.75, 0.04, 0.29, config);
        var swell = new SeaPose(1, 0.3, 0.1, 0.05);

        var pose = canoe.Follow(swell, 60);
        for (int frame = 0; frame < 180; frame++) pose = canoe.Follow(swell, FrameSeconds);

        Assert.InRange(pose.Lift, 0, 0.6);
        Assert.InRange(pose.Roll, 0, 0.2);
    }

    [Fact]
    public void AnUnknownGapLeavesHerWhereSheWas()
    {
        var response = new HullResponse(2, 2, config);

        Assert.Equal(0, response.Follow(new SeaPose(1, 1, 1, 1), double.NaN).Lift);
    }

    [Fact]
    public void AHullWithNoPeriodsFollowsTheWaterExactly()
    {
        var pose = new SeaPose(1, 0.3, 0.1, -0.05);

        Assert.Equal(pose, new HullResponse(0, 0, config).Follow(pose, FrameSeconds));
    }

    [Fact]
    public void ACalmSeaLeavesHerAtRest()
    {
        var response = new HullResponse(2, 2, config);
        response.Follow(new SeaPose(1, 1, 1, 1), 5);

        Assert.Equal(default, response.Follow(default, FrameSeconds));
    }

    private static double Peak(HullResponse response, double periodSeconds, double seconds)
    {
        double peak = 0;
        for (double t = 0; t < seconds; t += FrameSeconds)
        {
            double water = Math.Sin(2 * Math.PI * t / periodSeconds);
            var pose = response.Follow(new SeaPose(1, water, 0, 0), FrameSeconds);
            if (t > seconds / 2) peak = Math.Max(peak, Math.Abs(pose.Lift));
        }
        return peak;
    }
}
