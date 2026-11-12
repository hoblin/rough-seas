using RoughSeas.Person;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Tests.Storms;
using Vintagestory.API.Config;

namespace RoughSeas.Tests.Person;

public class FloatingBodyTests
{
    private const double Frame = 1 / 60.0;
    private const double Still = 100;
    private const double X = 512031.25;
    private const double Z = 498877.5;
    private const double EyeHeight = 1.7;
    private const double Width = 0.6;
    private const double Height = 2;
    private const int Stretch = 20;

    private static readonly Waves Waves = new(Fixtures.Config().Waves);
    private static readonly PlayerPerson Man = Fixtures.Person;
    private static readonly double Kept = WaterDrag.Kept(0.92, Frame);

    private static Carried Kit(double kg) => new(kg, kg / Fixtures.PersonConfig.Carried.DensityKgPerLitre);

    private sealed class Run(IDrawnSea sea, double feet, bool salt = true, double kit = 0, double rate = 1, SwimStroke stroke = default)
    {
        private readonly FloatingBody body = new(Man, Fixtures.PersonConfig, Waves);
        private BodyMotion motion;

        public SwimStroke Stroke { get; set; } = stroke;

        public double Feet { get; private set; } = feet;

        public double MovedX { get; private set; }

        public double MovedZ { get; private set; }

        public FloatingBody Body => body;

        public WaterSurface Surface => new(Still + sea.HeightAt(X + MovedX, Z + MovedZ), sea.HeightAt(X + MovedX, Z + MovedZ), salt);

        public FloatReading Step()
        {
            if (sea is WaterFixtures.Sea still) still.Seconds += Frame;
            var next = body.Step(sea, Surface, new BodyPlace(X + MovedX, Feet, Z + MovedZ, EyeHeight, Width), motion, Kit(kit), Frame, rate, Kept, Stroke);
            motion = new BodyMotion(next.X * Kept, next.Y, next.Z * Kept);
            Feet += motion.Y * WaterDrag.TicksPerSecond * Frame;
            MovedX += motion.X * WaterDrag.TicksPerSecond * Frame;
            MovedZ += motion.Z * WaterDrag.TicksPerSecond * Frame;
            return body.Reading;
        }

        public double Settle(double seconds)
        {
            for (var t = 0.0; t < seconds; t += Frame) Step();
            return Feet;
        }

        public void Strike(BreakerPacket crest) => body.Struck(sea, crest);
    }

    private static IDrawnSea Calm => new WaterFixtures.Sea(0, swell: false);

    [Fact]
    public void FloatsInTheSeaWithTheWaterAtTheFace()
    {
        var run = new Run(Calm, Still - 1.6);
        run.Settle(120);

        var waterline = Still - run.Feet;
        Assert.InRange(waterline, 1.6, 1.74);
        Assert.InRange(Math.Abs(waterline - EyeHeight), 0, 0.1);
    }

    [Fact]
    public void SinksSlowlyInFreshWater()
    {
        var run = new Run(Calm, Still - 1.6, salt: false);
        var from = run.Feet;
        run.Settle(60);

        var speed = (from - run.Feet) / 60;
        Assert.InRange(speed, 0.05, 0.5);
    }

    [Fact]
    public void ALadenBodyFloatsLower()
    {
        var bare = new Run(Calm, Still - 1.6);
        var laden = new Run(Calm, Still - 1.6, kit: 1);
        bare.Settle(120);
        laden.Settle(120);

        Assert.True(laden.Feet < bare.Feet - 0.02, $"{laden.Feet:F3} against {bare.Feet:F3}");
    }

    [Fact]
    public void FullPlateSinksIt()
    {
        var run = new Run(Calm, Still - 1.6, kit: 35);
        var at = run.Settle(5);
        run.Settle(5);

        Assert.True(run.Feet < at - 3, $"{at - run.Feet:F2} m in 5 s");
        Assert.True(run.Feet < Still - 2);
    }

    [Fact]
    public void RisesFromAboveTheBoyleDepthAndSinksFromBelowIt()
    {
        var rises = new Run(Calm, Still - 4);
        var sinks = new Run(Calm, Still - 14);
        var (risesFrom, sinksFrom) = (rises.Feet, sinks.Feet);
        rises.Settle(5);
        sinks.Settle(5);

        Assert.True(rises.Feet > risesFrom + 0.1, $"from 4 m it moved {rises.Feet - risesFrom:F2} m");
        Assert.True(sinks.Feet < sinksFrom - 0.1, $"from 14 m it moved {sinks.Feet - sinksFrom:F2} m");
    }

    [Fact]
    public void SqueezesItsBreathByTheDepthOfItsChest()
    {
        var deep = new Run(Calm, Still - 14).Step();
        Assert.True(deep.LungLitres < 0.7 * Man.Body.BreathingLitres, $"{deep.LungLitres:F2} L");

        var surface = new Run(Calm, Still - 1.6).Step();
        Assert.InRange(surface.LungLitres, 0.9 * Man.Body.BreathingLitres, Man.Body.BreathingLitres);
    }

    [Fact]
    public void HeavesWithALongSwell()
    {
        var sea = Fixtures.OneTrain(Height, Stretch);
        sea.Surface.Advance(100);
        var run = new Run(sea.Sea, Still - 1.6);
        var (time, low, high) = (100.0, double.MaxValue, double.MinValue);
        for (; time < 160; time += Frame)
        {
            sea.Surface.Advance(time);
            run.Step();
        }
        for (; time < 260; time += Frame)
        {
            sea.Surface.Advance(time);
            run.Step();
            (low, high) = (Math.Min(low, run.Feet), Math.Max(high, run.Feet));
        }

        Assert.InRange(high - low, 0.9 * Height / Math.Sqrt(2), 1.1 * Height / Math.Sqrt(2));
    }

    private static (double Flow, double Rise) PeakWaterRead(double wavesPerSecond)
    {
        var sea = Fixtures.OneTrain(Height, Stretch);
        var run = new Run(sea.Sea, Still - 1.6, rate: wavesPerSecond);
        var (flow, rise) = (0.0, 0.0);
        for (var time = 100.0; time < 160; time += Frame * wavesPerSecond)
        {
            sea.Surface.Advance(time);
            var read = run.Step();
            (flow, rise) = (Math.Max(flow, Math.Abs(read.FlowX)), Math.Max(rise, Math.Abs(read.Rise)));
        }
        return (flow, rise);
    }

    [Fact]
    public void ReadsTheWatersMotionOnTheTicksClock()
    {
        var normal = PeakWaterRead(1);
        var fast = PeakWaterRead(3);

        Assert.InRange(fast.Flow / normal.Flow, 2.7, 3.3);
        Assert.InRange(fast.Rise / normal.Rise, 2.7, 3.3);
    }

    [Fact]
    public void IsCarriedWithTheWaterAcrossWithoutLaggingItMuch()
    {
        var sea = Fixtures.OneTrain(Height, Stretch);
        var run = new Run(sea.Sea, Still - 1.6);
        var (time, peakWater, peakBody, atWater, atBody) = (100.0, 0.0, 0.0, 0.0, 0.0);
        for (; time < 160; time += Frame)
        {
            sea.Surface.Advance(time);
            run.Step();
        }
        var period = 2 * Math.PI / sea.Frequency;
        for (; time < 160 + 3 * period; time += Frame)
        {
            sea.Surface.Advance(time);
            var from = run.MovedX;
            var read = run.Step();
            var moving = Math.Abs(run.MovedX - from) / Frame;
            if (Math.Abs(read.FlowX) > peakWater) (peakWater, atWater) = (Math.Abs(read.FlowX), time);
            if (moving > peakBody) (peakBody, atBody) = (moving, time);
        }

        Assert.InRange(peakBody / peakWater, 0.9, 1.1);
        var lag = Math.Abs(atBody - atWater) % period;
        Assert.True(Math.Min(lag, period - lag) < period * 5 / 360, $"{Math.Min(lag, period - lag) / period * 360:F1} degrees behind");
    }

    [Fact]
    public void ABreakerDrivesItUnderAndItComesBack()
    {
        var calmly = new Run(Calm, Still - 1.6);
        var riding = calmly.Settle(120);

        calmly.Strike(new BreakerPacket { HeightMetres = 2, PeriodSeconds = 8, Celerity = 9, TowardRadians = 0 });
        var lowest = calmly.Feet;
        var churned = 0.0;
        for (var t = 0.0; t < 3; t += Frame)
        {
            churned = Math.Max(churned, calmly.Step().Churn);
            lowest = Math.Min(lowest, calmly.Feet);
        }
        var back = calmly.Settle(60);

        Assert.True(churned > 0.05, $"{churned:P1} of its water");
        Assert.True(lowest < riding - 0.2, $"driven {riding - lowest:F2} m under");
        Assert.False(calmly.Body.Breaking);
        Assert.InRange(back - riding, -0.05, 0.05);
    }

    [Fact]
    public void ABreakersAirOnlyTakesWhatLiesInThePlume()
    {
        var small = new Run(Calm, Still - 1.6);
        var large = new Run(Calm, Still - 1.6);
        small.Settle(20);
        large.Settle(20);
        small.Strike(new BreakerPacket { HeightMetres = 0.5, PeriodSeconds = 8, Celerity = 5 });
        large.Strike(new BreakerPacket { HeightMetres = 4, PeriodSeconds = 8, Celerity = 9 });

        Assert.True(small.Step().Churn < 0.6 * large.Step().Churn);
    }

    [Fact]
    public void ARollerCarriesItAlongTheWave()
    {
        var run = new Run(Calm, Still - 1.6);
        run.Settle(20);
        var from = run.MovedX;
        run.Strike(new BreakerPacket { HeightMetres = 3, PeriodSeconds = 8, Celerity = 9, TowardRadians = 0 });
        run.Settle(3);

        Assert.True(run.MovedX > from + 1, $"{run.MovedX - from:F2} m");
        Assert.Equal(0, run.MovedZ, 6);
    }

    [Fact]
    public void ASecondCrestRenewsTheChurnAsTheFirstFades()
    {
        var run = new Run(Calm, Still - 1.6);
        run.Settle(20);
        var crest = new BreakerPacket { HeightMetres = 2, PeriodSeconds = 8, Celerity = 9 };
        run.Strike(crest);
        var first = run.Step().Churn;
        run.Settle(crest.PeriodSeconds);
        var faded = run.Step().Churn;

        run.Strike(crest);

        var renewed = run.Step().Churn;
        Assert.True(faded < first / 5, $"{faded:P1} of {first:P1} after a period");
        Assert.True(renewed > 5 * faded, $"{renewed:P1} against {faded:P1} before it");
    }

    private static readonly SwimStroke Ahead = new(1, 0, 0);

    [Fact]
    public void SwimsByTheThrustOfItsStrokeAsFastAsTheDragItMeetsAllows()
    {
        var run = new Run(Calm, Still - 1.6, salt: false, stroke: Ahead);
        run.Settle(30);
        var from = run.MovedX;
        run.Settle(10);

        Assert.InRange((run.MovedX - from) / 10, 0.87, 0.93);
    }

    [Fact]
    public void GlidesToAStopWhenItStopsStroking()
    {
        var run = new Run(Calm, Still - 1.6, stroke: Ahead);
        run.Settle(30);
        run.Stroke = default;
        run.Settle(30);
        var from = run.MovedX;
        run.Settle(1);

        Assert.InRange(run.MovedX - from, 0, 0.05);
    }

    [Fact]
    public void SwimmingWithAWavesFaceItIsCarriedFasterThanItSwims()
    {
        var face = new WaterFixtures.Sea(0, flow: (1.0, 0), push: (0.4, 0));
        var run = new Run(face, Still - 1.6, stroke: Ahead);
        run.Settle(30);
        var from = run.MovedX;
        run.Settle(10);

        Assert.True((run.MovedX - from) / 10 > 1.0 + 0.9, $"{(run.MovedX - from) / 10:F2} m/s");
    }

    [Fact]
    public void AStrokeCarriesOnThroughTheStep()
    {
        var body = new FloatingBody(Man, Fixtures.PersonConfig, Waves);
        var own = 0.05;
        var next = body.Step(Calm, new WaterSurface(Still, 0, true), new BodyPlace(X, Still - 1.6, Z, EyeHeight, Width), new BodyMotion(own, 0, 0), default, Frame, 1, Kept);

        Assert.Equal(own, next.X, 9);
    }
}
