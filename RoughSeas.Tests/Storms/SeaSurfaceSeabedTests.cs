using RoughSeas.Storms;
using RoughSeas.Surf;

namespace RoughSeas.Tests.Storms;

public class SeaSurfaceSeabedTests
{
    private const int Size = 64;
    private const int OriginX = 1000;
    private const int OriginZ = 2000;
    private const double MiddleX = OriginX + Size / 2.0;
    private const double MiddleZ = OriginZ + Size / 2.0;

    private static SeaSurface Settled(double height)
    {
        var surface = new SeaSurface(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves));
        var raised = new RoughSeas.SeaAndWeather.WaveGeneration(0.3, 3.5 * Math.Sqrt(height));
        for (var frame = 0; frame < 3600; frame++)
        {
            if (frame % 30 == 0)
            {
                surface.Aim(height, 6);
                surface.Raise(raised, raised, 0);
            }
            surface.Advance(frame / 60.0);
        }
        return surface;
    }

    private static SurfField Flat(SeaSurface surface, float depth, (double X, double Z)[]? numbers = null, float lag = 0)
    {
        var field = new SurfField(OriginX, OriginZ, Size, numbers ?? surface.Trains.Vectors());
        for (var x = OriginX; x < OriginX + Size; x++)
        for (var z = OriginZ; z < OriginZ + Size; z++)
        {
            field.SetDepth(x, z, depth);
            for (var slot = 0; slot < SurfField.Slots; slot++) field.SetLag(slot, x, z, lag);
        }
        return field;
    }

    private static double Highest(SeaSurface surface, bool shortWaves = true) =>
        Enumerable.Range(0, 400).Max(i => surface.HeightAt(MiddleX + i % 20 * 0.7, MiddleZ + i / 20 * 0.9, 1, shortWaves));

    [Fact]
    public void OverLandTheSeaIsNotDrawn()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, -0.5f);
        Assert.Equal(0, surface.HeightAt(MiddleX, MiddleZ, 1));
    }

    [Fact]
    public void TheEnergySolvedForATrainSetsItsHeight()
    {
        var surface = Settled(3);
        surface.Seabed = Flat(surface, 1000);
        surface.Seabed.EdgeFadeBlocks = 1;
        var open = Highest(surface, shortWaves: false);
        for (var x = OriginX; x < OriginX + Size; x++)
        for (var z = OriginZ; z < OriginZ + Size; z++)
        for (var slot = 0; slot < SurfField.Slots; slot++) surface.Seabed.SetGain(slot, x, z, 0.5f);
        Assert.InRange(Highest(surface, shortWaves: false) / open, 0.45, 0.65);
    }

    private static void Face(SurfField field, double normalX, double normalZ, float reflection)
    {
        for (var x = OriginX; x < OriginX + Size; x++)
        for (var z = OriginZ; z < OriginZ + Size; z++)
            field.SetFace(x, z, 1, (float)normalX, (float)normalZ, reflection);
    }

    [Fact]
    public void AtAWallTheWaveAndItsReflectionStandTwiceAsHigh()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, 1000);
        var open = Highest(surface);
        Face(surface.Seabed, -Math.Cos(0.3), -Math.Sin(0.3), 1);
        Assert.InRange(Highest(surface) / open, 1.6, 2.1);
    }

    [Fact]
    public void AReflectionShiftsWithItsDistanceFromTheWall()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, 1000);
        Face(surface.Seabed, -Math.Cos(0.3), -Math.Sin(0.3), 1);
        var atTheWall = surface.HeightAt(MiddleX, MiddleZ, 1);
        for (var x = OriginX; x < OriginX + Size; x++)
        for (var z = OriginZ; z < OriginZ + Size; z++) surface.Seabed.SetFaceDistance(x, z, 9);
        Assert.NotEqual(atTheWall, surface.HeightAt(MiddleX, MiddleZ, 1), 2);
    }

    [Fact]
    public void ReflectedWaterRisesFromTheFaceNoSteeperThanTheLimit()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, 1000);
        var open = surface.HeightAt(MiddleX, MiddleZ, 1);
        Face(surface.Seabed, -Math.Cos(0.3), -Math.Sin(0.3), 1);
        surface.Seabed.FaceSlope = 0;
        Assert.Equal(open, surface.HeightAt(MiddleX, MiddleZ, 1), 1);
    }

    [Fact]
    public void AWallBehindTheWavesReflectsNothing()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, 1000);
        var open = surface.HeightAt(MiddleX, MiddleZ, 1);
        Face(surface.Seabed, Math.Cos(0.3), Math.Sin(0.3), 1);
        Assert.Equal(open, surface.HeightAt(MiddleX, MiddleZ, 1), 3);
    }

    [Fact]
    public void OverDeepWaterTheSeabedChangesNothing()
    {
        var surface = Settled(2);
        var open = surface.HeightAt(MiddleX, MiddleZ, 1);
        surface.Seabed = Flat(surface, 1000);
        Assert.Equal(open, surface.HeightAt(MiddleX, MiddleZ, 1), 3);
    }

    [Fact]
    public void ALagSolvedForOtherWavesIsLeftOut()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, 1000);
        var open = surface.HeightAt(MiddleX, MiddleZ, 1);
        surface.Seabed = Flat(surface, 1000, surface.Trains.Vectors().Select(number => (number.X * 2, number.Z)).ToArray(), lag: 1.5f);
        Assert.Equal(open, surface.HeightAt(MiddleX, MiddleZ, 1), 3);
    }

    [Fact]
    public void AWaveHeldBackByTheBedRisesLater()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, 1000);
        var open = surface.HeightAt(MiddleX, MiddleZ, 1);
        surface.Seabed = Flat(surface, 1000, lag: 1.5f);
        Assert.NotEqual(open, surface.HeightAt(MiddleX, MiddleZ, 1), 3);
    }

    [Fact]
    public void WithoutAShareTheSeaIsReadAsOpenSea()
    {
        var surface = Settled(2);
        var open = surface.DisplacementAt(MiddleX, MiddleZ).Y;
        surface.Seabed = Flat(surface, -1);
        Assert.Equal(open, surface.DisplacementAt(MiddleX, MiddleZ).Y, 9);
    }
}
