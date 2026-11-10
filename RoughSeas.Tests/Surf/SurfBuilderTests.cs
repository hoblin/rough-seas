using RoughSeas.Shore;
using RoughSeas.Surf;
using RoughSeas.Tests.Shore;

namespace RoughSeas.Tests.Surf;

public class SurfBuilderTests
{
    private const int Size = 32;
    private const int Level = 100;
    private static readonly BedConfig Stone = ShoreFixtures.Bed("stone");

    private static SurfField Stairs(params double[] elevations)
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces, 1.28);
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (x < 16) map.Put(x, z, new ShoreCell(ShoreKind.Water, 10, Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Land, elevations[Math.Min(x - 16, elevations.Length - 1)], Stone, 1), Level);
        }
        return new SurfBuilder(ShoreFixtures.Band, faces).Build(map, (_, _) => new Sea(4, 8), [(0.1, 0)]);
    }

    private static SurfField Bed(Func<int, double> depthAlongX)
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces, 1.28);
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            map.Put(x, z, new ShoreCell(ShoreKind.Water, depthAlongX(x), Stone, 1), Level);
        }
        return new SurfBuilder(ShoreFixtures.Band, faces).Build(map, (_, _) => new Sea(4, 8), [(0.1, 0)]);
    }

    private static SurfField Beach(Func<int, double> depthAlongX)
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces, 1.28);
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (x < 16) map.Put(x, z, new ShoreCell(ShoreKind.Water, depthAlongX(x), Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Land, 0.125, Stone, 1), Level);
        }
        return new SurfBuilder(ShoreFixtures.Band, faces).Build(map, (_, _) => new Sea(4, 8), [(0.1, 0)]);
    }

    [Fact]
    public void AShoreBehindABarRunsUpLessThanAnOpenOne()
    {
        var open = Beach(_ => 10);
        var sheltered = Beach(x => x is >= 4 and < 8 ? 0.5 : 10);

        Assert.True(sheltered.Get(sheltered.Bed, 17, 16, 2) < open.Get(open.Bed, 17, 16, 2));
    }

    [Fact]
    public void TheLandASheetReachesHoldsTheGainOfTheShoreItCameFrom()
    {
        var field = Beach(_ => 10);

        Assert.Equal(field.Get(field.Gains[0], 15, 16, 0), field.Get(field.Gains[0], 17, 16, 0));
        Assert.True(field.Get(field.Gains[0], 17, 16, 0) > 0);
    }

    [Fact]
    public void AWaveFeelsAStepInTheBedOverAQuarterOfItsLength()
    {
        var field = Bed(x => x < 16 ? 2 : 8);

        Assert.InRange(field.DepthAt(15.5, 16.5), 2.5, 7.5);
        Assert.True(field.DepthAt(8.5, 16.5) < field.DepthAt(15.5, 16.5));
        Assert.True(field.DepthAt(24.5, 16.5) > field.DepthAt(15.5, 16.5));
    }

    [Fact]
    public void ABedOfNoiseInDeepWaterHasNoSlopeToTurnAWave()
    {
        var field = Bed(x => 70 + x % 2);

        Assert.InRange(field.SlopeXAt(16.5, 16.5), -0.1, 0.1);
    }

    [Fact]
    public void ASecondRiserWithNoTreadStopsTheSheetAndRecordsTheStrike() =>
        Assert.Contains(Stairs(0.125, 1.125, 2.125, 2.125).Steps, step => step is { X: 17, SideX: 1 } && step.Heads.Any(head => head > 0));

    [Fact]
    public void ARiserAfterATreadIsNoStrikeForTheSheetThatCanClimbIt() =>
        Assert.DoesNotContain(Stairs(0.125, 0.125, 1.125, 1.125).Steps, step => step is { X: 17, SideX: 1 } && step.Heads[^1] > 0);
}
