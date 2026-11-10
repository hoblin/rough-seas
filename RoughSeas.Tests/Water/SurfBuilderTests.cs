using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class SurfBuilderTests
{
    private const int Size = 32;
    private const int Level = 100;
    private static readonly BedConfig Stone = ShoreFixtures.Bed("stone");

    private static SurfBuilder Builder(FaceBuilder faces) => new(new SwashSheets(ShoreFixtures.Band), faces, new WaveTransformation(1.28, faces.SmoothingBlocks, 1, 0.12, 0.45));

    private static SurfField Stairs(params double[] elevations)
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces);
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (x < 16) map.Put(x, z, new ShoreCell(ShoreKind.Water, 10, Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Land, elevations[Math.Min(x - 16, elevations.Length - 1)], Stone, 1), Level);
        }
        return Builder(faces).Build(map, (_, _) => new Sea(4, 8), [(0.1, 0)]);
    }

    private static SurfField Bed(Func<int, double> depthAlongX)
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces);
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            map.Put(x, z, new ShoreCell(ShoreKind.Water, depthAlongX(x), Stone, 1), Level);
        }
        return Builder(faces).Build(map, (_, _) => new Sea(4, 8), [(0.1, 0)]);
    }

    private static SurfField Beach(Func<int, double> depthAlongX)
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces);
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (x < 16) map.Put(x, z, new ShoreCell(ShoreKind.Water, depthAlongX(x), Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Land, 0.125, Stone, 1), Level);
        }
        return Builder(faces).Build(map, (_, _) => new Sea(4, 8), [(0.1, 0)]);
    }

    [Fact]
    public void AShoreBehindABarRunsUpLessThanAnOpenOne()
    {
        var open = Beach(_ => 10);
        var sheltered = Beach(x => x is >= 4 and < 8 ? 0.5 : 10);

        Assert.True(sheltered.RunUpOf(17, 16) < open.RunUpOf(17, 16));
    }

    [Fact]
    public void TheLandASheetReachesHoldsTheGainOfTheShoreItCameFrom()
    {
        var field = Beach(_ => 10);

        Assert.Equal(field.GainOf(0, 15, 16), field.GainOf(0, 17, 16));
        Assert.True(field.GainOf(0, 17, 16) > 0);
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
