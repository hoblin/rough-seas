using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class HullDriveTests
{
    private const double X = 512031.25;
    private const double Z = 498877.5;
    private const double Frame = 1 / 60.0;
    private const double Height = 2;
    private const double AlongTheWaves = -Math.PI / 2;
    private const double AcrossTheWaves = 0;

    private static readonly DrivenHull Raft = new(3, 3, 0.1, 0.25, 280);
    private static readonly DrivenHull Dugout = new(4.5, 0.75, 0.21, 0.41, 480);
    private static readonly DrivenHull LoadedDugout = Dugout with { DraftMetres = 0.38, MassKg = 870 };
    private static readonly DrivenHull Keelboat = new(7, 2.2, 0.6, 1.14, 1500);

    [Fact]
    public void ARaftFollowsTheWaterOfALongSwell()
    {
        var sea = Sea(stretch: 20);
        var (sway, water, _) = Ride(sea, Raft, AlongTheWaves, 60);

        Assert.InRange(sway / water, 0.97, 1.0);
    }

    [Fact]
    public void ALongDeepHullSmoothsTheSwayARaftFollows()
    {
        var (raft, water, _) = Ride(Sea(stretch: 11), Raft, AlongTheWaves, 60);
        var (keelboat, _, _) = Ride(Sea(stretch: 11), Keelboat, AlongTheWaves, 60);

        Assert.InRange(raft / water, 0.8, 1.0);
        Assert.True(keelboat < 0.5 * raft, $"keelboat {keelboat:F3} m/s against the raft's {raft:F3} m/s");
    }

    [Fact]
    public void ALoadedHullSwaysAsTheLightOneDoes()
    {
        // she displaces her own mass, so the water's pressure on her grows with it: only her deeper draft and her bob tell
        var (light, _, _) = Ride(Sea(stretch: 11), Dugout, AlongTheWaves, 60);
        var (loaded, _, _) = Ride(Sea(stretch: 11), LoadedDugout, AlongTheWaves, 60);

        Assert.InRange(loaded / light, 0.95, 1.05);
    }

    [Fact]
    public void AHullLyingAlongShortWavesSwaysLessThanOneAcrossThem()
    {
        var (along, water, _) = Ride(Sea(stretch: 11), Dugout, AlongTheWaves, 60);
        var (across, _, _) = Ride(Sea(stretch: 11), Dugout, AcrossTheWaves, 60);

        Assert.True(along < 0.8 * across, $"along {along:F3} m/s against across {across:F3} m/s");
        Assert.True(across < water, $"across {across:F3} m/s against the water's {water:F3} m/s");
    }

    [Fact]
    public void ASmallHullDriftsAtTheStokesDrift()
    {
        var sea = Sea(stretch: 20);
        var (_, _, drift) = Ride(sea, new DrivenHull(0.3, 0.3, 0.02, 0.05, 2), AlongTheWaves, 300);

        // a little over: it rides the drawn surface, whose crests the drawn steepness sharpens
        Assert.InRange(drift / sea.Stokes, 0.95, 1.1);
    }

    [Fact]
    public void AHullHalfAWaveLongDriftsLessThanTheWater()
    {
        var sea = Sea(stretch: 14);
        var half = Math.PI / sea.Number;
        var (_, _, small) = Ride(sea, new DrivenHull(0.3, 0.3, 0.02, 0.05, 2), AlongTheWaves, 300);
        var (_, _, large) = Ride(sea, new DrivenHull(half, 1, 0.2, 0.4, 400), AlongTheWaves, 300);

        Assert.True(large < 0.7 * small, $"half a wave long {large:F3} m/s against small {small:F3} m/s");
        Assert.True(large > 0, $"half a wave long {large:F3} m/s");
    }

    [Fact]
    public void AHullDragsWaterAsAnEllipsoidDoes()
    {
        var drive = Drive();

        // a sphere drags half its mass whichever way it moves (Lamb 1932)
        var (surge, sway) = drive.AddedMass(new DrivenHull(2, 2, 1, 1, 1));
        Assert.Equal(0.5, surge, 3);
        Assert.Equal(0.5, sway, 3);

        // a prolate spheroid ten times as long as it is wide: 0.021 along and 0.960 across (Lamb 1932, §373)
        (surge, sway) = drive.AddedMass(new DrivenHull(10, 1, 0.5, 1, 1));
        Assert.Equal(0.021, surge, 3);
        Assert.Equal(0.960, sway, 2);

        // a flat raft drags little water edgeways
        Assert.InRange(drive.AddedMass(Raft).Sway, 0, 0.1);
    }

    [Fact]
    public void HasTheSourcedDragCoefficients()
    {
        var drive = Drive();

        Assert.Equal(0.075 / 16, drive.Friction(1, 1.19), 6);
        Assert.Equal(1.848 - 0.0591 * 20, drive.CrossFlow(4, 0.2), 9);
        Assert.Equal(1.848 - 0.0591 * 25, drive.CrossFlow(3, 0.05), 9);
    }

    private static HullDrive Drive()
    {
        var config = Fixtures.Config();
        return new HullDrive(config.Drive, config.Response, config.Waves.WaterDensity);
    }

    private static (double Sway, double Water, double Drift) Ride(Train sea, DrivenHull hull, double yaw, double seconds)
    {
        var drive = Drive();
        var settled = 10 * 2 * Math.PI / sea.Frequency;
        var (x, from) = (X, 0.0);
        var (swayLow, swayHigh, waterLow, waterHigh) = (0.0, 0.0, 0.0, 0.0);
        for (var time = 120.0; time < 120 + settled + seconds; time += Frame)
        {
            sea.Surface.Advance(time);
            var (alongX, _) = drive.Step(sea.Sea, x, Z, yaw, hull, Frame);
            x += alongX * Frame;
            if (time < 120 + settled)
            {
                from = x;
                continue;
            }
            var water = sea.Sea.OrbitalVelocityAt(x, Z).X;
            (swayLow, swayHigh) = (Math.Min(swayLow, alongX), Math.Max(swayHigh, alongX));
            (waterLow, waterHigh) = (Math.Min(waterLow, water), Math.Max(waterHigh, water));
        }
        return ((swayHigh - swayLow) / 2, (waterHigh - waterLow) / 2, (x - from) / seconds);
    }

    private readonly record struct Train(DrawnSea Sea, SeaSurface Surface, double Number, double Frequency, double Stokes);

    /// <summary>One train along +X, (1 + stretch)^<paramref name="stretch"/> blocks long and <see cref="Height"/> high.</summary>
    private static Train Sea(int stretch)
    {
        var config = Fixtures.Config().Surface;
        var single = new SurfaceConfig
        {
            Trains = [new SurfaceTrainConfig { AngleDegrees = 0, LengthFactor = 1, Energy = 1 }],
            Steepness = config.Steepness,
            MinLengthBlocks = config.MinLengthBlocks,
            SettleSeconds = config.SettleSeconds,
            TurnDegrees = config.TurnDegrees,
            Stretch = config.Stretch,
            AimSeconds = config.AimSeconds,
            FullFoamMetres = config.FullFoamMetres,
            Whitecaps = config.Whitecaps,
        };
        var length = Math.Pow(1 + config.Stretch, stretch);
        var number = 2 * Math.PI / length;
        var frequency = Math.Sqrt(9.81 * number);
        var surface = new SeaSurface(single, new Waves(Fixtures.Config().Waves));
        var raised = new WaveGeneration(0, Math.Sqrt(2 * Math.PI * length / 9.81));
        surface.Aim(Height, 0, 6);
        surface.Raise(raised, raised, 0);
        surface.Advance(120);
        var amplitude = Height / (2 * Math.Sqrt(2));
        return new Train(new DrawnSea(surface, (_, _) => 1), surface, number, frequency, amplitude * amplitude * frequency * number);
    }
}
