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
                surface.Aim(height, raised.TowardRadians, 6);
                surface.Raise(raised, raised, 0);
            }
            surface.Advance(frame / 60.0);
        }
        return surface;
    }

    private static SurfField Flat(SeaSurface surface, float depth, (double X, double Z)[]? numbers = null, float lag = 0)
    {
        var field = new SurfField(OriginX, OriginZ, Size, numbers ?? surface.Vectors());
        for (var x = OriginX; x < OriginX + Size; x++)
        for (var z = OriginZ; z < OriginZ + Size; z++)
        {
            field.Set(field.Bed, x, z, 0, depth);
            for (var slot = 0; slot < SurfField.Slots; slot++) field.Set(field.Lags[slot / SurfField.Channels], x, z, slot % SurfField.Channels, lag);
        }
        return field;
    }

    private static double Highest(SeaSurface surface) =>
        Enumerable.Range(0, 400).Max(i => surface.HeightAt(MiddleX + i % 20 * 0.7, MiddleZ + i / 20 * 0.9, 1));

    [Fact]
    public void OverLandTheSeaIsNotDrawn()
    {
        var surface = Settled(2);
        surface.Seabed = Flat(surface, -0.5f);
        Assert.Equal(0, surface.HeightAt(MiddleX, MiddleZ, 1));
    }

    [Fact]
    public void InShallowWaterNoCrestStandsHigherThanTheDepthLetsTheWaveBe()
    {
        var surface = Settled(3);
        surface.Seabed = Flat(surface, 1);
        Assert.True(Highest(surface) <= 1 / 1.28 * 1.1);
    }

    private static void Face(SurfField field, double normalX, double normalZ, float reflection)
    {
        for (var x = OriginX; x < OriginX + Size; x++)
        for (var z = OriginZ; z < OriginZ + Size; z++)
        {
            field.Set(field.Face, x, z, 1, (float)normalX);
            field.Set(field.Face, x, z, 2, (float)normalZ);
            field.Set(field.Face, x, z, 3, reflection);
        }
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
        for (var z = OriginZ; z < OriginZ + Size; z++) surface.Seabed.Set(surface.Seabed.Face, x, z, 0, 9);
        Assert.NotEqual(atTheWall, surface.HeightAt(MiddleX, MiddleZ, 1), 2);
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
        surface.Seabed = Flat(surface, 1000, surface.Vectors().Select(number => (number.X * 2, number.Z)).ToArray(), lag: 1.5f);
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
