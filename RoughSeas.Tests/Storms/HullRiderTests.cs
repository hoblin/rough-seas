using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class HullRiderTests
{
    private const double X = 512031.25;
    private const double Z = 498877.5;
    private const double Frame = 1 / 60.0;
    private const double Height = 2;
    private const double AlongTheWaves = -Math.PI / 2;
    private const double AcrossTheWaves = 0;
    private const double DugoutLitres = 300;

    private static readonly RiddenHull Raft = Fixtures.Ridden("boat-raft");
    private static readonly RiddenHull Dugout = Fixtures.Ridden("dugout");
    private static readonly RiddenHull Sailboat = Fixtures.Ridden("boat-sailed");
    private static readonly RiddenHull Speck = new(0.3, 0.3);

    [Fact]
    public void ARaftFollowsTheWaterOfALongSwell()
    {
        var (sway, water, _) = Ride(Fixtures.OneTrain(Height, 20), Raft, AlongTheWaves, 60);

        Assert.InRange(sway / water, 0.95, 1.0);
    }

    [Fact]
    public void ALongDeepHullSmoothsTheSwayARaftFollows()
    {
        var (raft, _, _) = Ride(Fixtures.OneTrain(Height, 11), Raft, AlongTheWaves, 60);
        var (sailboat, _, _) = Ride(Fixtures.OneTrain(Height, 11), Sailboat, AlongTheWaves, 60);

        Assert.True(sailboat < 0.8 * raft, $"sailboat {sailboat:F3} m/s against the raft's {raft:F3} m/s");
    }

    [Fact]
    public void ALoadedHullSwaysAsTheLightOneDoes()
    {
        var loaded = Fixtures.Ridden("dugout", DugoutLitres);
        Assert.True(loaded.Kg > Dugout.Kg + DugoutLitres / 2);

        var (light, _, _) = Ride(Fixtures.OneTrain(Height, 11), Dugout, AlongTheWaves, 60);
        var (laden, _, _) = Ride(Fixtures.OneTrain(Height, 11), loaded, AlongTheWaves, 60);

        // she displaces her own mass, so only her deeper draft and her slower bob tell
        Assert.InRange(laden / light, 0.9, 1.1);
    }

    [Fact]
    public void AHullLyingAlongShortWavesSwaysLessThanOneAcrossThem()
    {
        var (along, _, _) = Ride(Fixtures.OneTrain(Height, 11), Dugout, AlongTheWaves, 60);
        var (across, _, _) = Ride(Fixtures.OneTrain(Height, 11), Dugout, AcrossTheWaves, 60);

        Assert.True(along < 0.9 * across, $"along {along:F3} m/s against across {across:F3} m/s");
    }

    [Fact]
    public void ASmallHullDriftsAtTheStokesDrift()
    {
        var sea = Fixtures.OneTrain(Height, 20);
        var (_, _, drift) = Ride(sea, Speck, AlongTheWaves, 300);

        // a little over: it rides the drawn surface, whose crests the drawn steepness sharpens
        Assert.InRange(drift / sea.Stokes, 0.95, 1.1);
    }

    [Fact]
    public void AHullHalfAWaveLongDriftsLessThanTheWater()
    {
        var stretch = 0;
        while (Fixtures.OneTrain(Height, stretch).Length < 2 * Sailboat.LengthMetres) stretch++;
        var sea = Fixtures.OneTrain(Height, stretch);

        var (_, _, small) = Ride(sea, Speck, AlongTheWaves, 300);
        var (_, _, large) = Ride(Fixtures.OneTrain(Height, stretch), Sailboat, AlongTheWaves, 300);

        Assert.True(large < 0.7 * small, $"half a wave long {large:F3} m/s against small {small:F3} m/s");
    }

    [Fact]
    public void CarriesAHullTheWayTheCrestsItSeesTravel()
    {
        var sea = Fixtures.OneTrain(Height, 20);
        double Crest(double near)
        {
            var (best, highest) = (near, double.NegativeInfinity);
            for (var x = near; x < near + sea.Length; x += 0.05)
            {
                if (sea.Sea.HeightAt(x, Z) is var height && height > highest) (best, highest) = (x, height);
            }
            return best;
        }

        sea.Surface.Advance(120);
        var crest = Crest(X);
        sea.Surface.Advance(120.5);
        var travelled = Crest(crest - 1) - crest;
        Assert.True(travelled > 0, $"the crest moved {travelled:F2} m");

        var rider = new HullRider(Fixtures.Config().Response);
        var (drifted, x) = (0.0, X);
        for (var time = 120.0; time < 720; time += Frame)
        {
            sea.Surface.Advance(time);
            rider.Follow(sea.Sea, x, Z, AlongTheWaves, Raft, time);
            var along = rider.Water(sea.Sea, x, Z, AlongTheWaves, Raft).X;
            if (sea.Sea.HeightAt(x, Z) > Height / 4) Assert.True(along > 0, $"{along:F3} m/s under a crest at {time:F1} s");
            x += along * Frame;
            drifted = x - X;
        }
        Assert.True(drifted > 0, $"drifted {drifted:F2} m");
    }

    [Fact]
    public void ReadsTheWaterAtTheHeaveItIsDrawnAt()
    {
        var sea = Fixtures.OneTrain(Height, 14);
        var rider = new HullRider(Fixtures.Config().Response);
        for (var time = 120.0; time < 150; time += Frame)
        {
            sea.Surface.Advance(time);
            var pose = rider.Follow(sea.Sea, X, Z, AcrossTheWaves, Raft, time);

            Assert.Equal(pose.Lift, rider.Lift);
            Assert.Equal(sea.Sea.HullVelocityAt(X, Z, pose.Lift, Raft.DraftMetres, AcrossTheWaves, Raft.LengthMetres, Raft.BeamMetres),
                rider.Water(sea.Sea, X, Z, AcrossTheWaves, Raft));
        }
    }

    [Fact]
    public void SettlingLeavesNoHeaveBehindForTheNextSea()
    {
        var sea = Fixtures.OneTrain(Height, 14);
        var rider = new HullRider(Fixtures.Config().Response);
        double time;
        for (time = 120; rider.Lift < 0.1; time += Frame)
        {
            sea.Surface.Advance(time);
            rider.Follow(sea.Sea, X, Z, AcrossTheWaves, Raft, time);
        }

        rider.Settle();
        Assert.Equal(0, rider.Lift);

        // met again it starts from rest, with no time caught up
        sea.Surface.Advance(time + 1000);
        Assert.Equal(0, rider.Follow(sea.Sea, X, Z, AcrossTheWaves, Raft, time + 1000).Lift);
    }

    private static (double Sway, double Water, double Drift) Ride(Fixtures.Train sea, RiddenHull hull, double yaw, double seconds)
    {
        var rider = new HullRider(Fixtures.Config().Response);
        var settled = 10 * 2 * Math.PI / sea.Frequency;
        var (x, from) = (X, 0.0);
        var (swayLow, swayHigh, waterLow, waterHigh) = (0.0, 0.0, 0.0, 0.0);
        for (var time = 120.0; time < 120 + settled + seconds; time += Frame)
        {
            sea.Surface.Advance(time);
            rider.Follow(sea.Sea, x, Z, yaw, hull, time);
            var (alongX, _) = rider.Water(sea.Sea, x, Z, yaw, hull);
            x += alongX * Frame;
            if (time < 120 + settled)
            {
                from = x;
                continue;
            }
            var water = sea.Sea.HullVelocityAt(x, Z, sea.Sea.HeightAt(x, Z), 0, yaw, 0, 0).X;
            (swayLow, swayHigh) = (Math.Min(swayLow, alongX), Math.Max(swayHigh, alongX));
            (waterLow, waterHigh) = (Math.Min(waterLow, water), Math.Max(waterHigh, water));
        }
        return ((swayHigh - swayLow) / 2, (waterHigh - waterLow) / 2, (x - from) / seconds);
    }
}
