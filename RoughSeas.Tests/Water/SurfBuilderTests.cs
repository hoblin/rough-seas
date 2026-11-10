using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class SurfBuilderTests
{
    private const int Size = 32;
    private const int Level = 100;
    private static readonly BedConfig Stone = ShoreFixtures.Bed("stone");

    private static SurfBuilder Builder(FaceBuilder faces) => new(new SwashSheets(ShoreFixtures.Band), faces, new WaveTransformation(1.28, faces.SmoothingBlocks, 1, 0.45));

    [Fact]
    public void AStormElsewhereLeavesTheSpeedOfAQuietShallowPatchAlone()
    {
        const int Across = 32;
        var depth = Enumerable.Repeat(0.05f, Across * Across).ToArray();
        var transformation = new WaveTransformation(1.28, _ => 0, 1, 0.45);
        double[] Seas(double remote) => Enumerable.Range(0, depth.Length).Select(i => i == 0 ? remote : 1).ToArray();

        Assert.Equal(transformation.Solve(Across, depth, [(0.05, 0)], null, Seas(1)).CelerityDepth[20 * Across + 20], transformation.Solve(Across, depth, [(0.05, 0)], null, Seas(16)).CelerityDepth[20 * Across + 20], 2);
    }

    [Fact]
    public void BehindAnIsletTheRaysDontPileUpIntoAMountainOfWater()
    {
        const int Across = 128;
        const double Number = 2 * Math.PI / 150;
        var depth = Enumerable.Range(0, Across * Across).Select(i =>
        {
            var (x, z) = (i % Across - 40, i / Across - 64);
            return x * x + z * z < 64 ? -1f : 40f;
        }).ToArray();
        var waves = new WaveTransformation(1.28, length => Math.Min(16, (int)Math.Ceiling(length * 0.25)), 1, 0.45)
            .Solve(Across, depth, [(Number, 0)], null, Enumerable.Repeat(4.0, depth.Length).ToArray());

        Assert.InRange(waves.Gains[0].Max(), 0, 1.4);
    }

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
    public void TheBrokenWavesReachingTheShorelineRunNoSlowerThanTheSheetTheyThrow()
    {
        var field = Beach(_ => 0.05);
        var launch = Math.Sqrt(2 * WaveTheory.Gravity * field.RunUpOf(17, 16) / Math.Sqrt(WaveTheory.TwoPercentRatio));

        Assert.True(launch > 0 && field.BreakingAt(15.5, 16.5) > 0);
        Assert.True(WaveTheory.Gravity * field.CelerityDepthAt(15.5, 16.5) >= launch * launch * field.BreakingAt(15.5, 16.5) - 0.01);
    }

    [Fact]
    public void ALakeBehindTheBeachKeepsItsOwnSpeedWhateverTheSeaRunsUp()
    {
        var faces = new FaceBuilder(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces);
        SurfField Shore(double seaHeight)
        {
            var map = new SeabedMap(0, 0, Size);
            for (var x = 0; x < Size; x++)
            for (var z = 0; z < Size; z++)
            {
                if (x < 16 || x >= 26) map.Put(x, z, new ShoreCell(ShoreKind.Water, x < 16 ? 0.5 : 0.3, Stone, 1), Level);
                else map.Put(x, z, new ShoreCell(ShoreKind.Land, 0.125, Stone, 1), Level);
            }
            return Builder(faces).Build(map, (x, _) => new Sea(x < 16 ? seaHeight : 0, 8), [(0.1, 0)]);
        }

        Assert.Equal(Shore(1).CelerityDepthAt(29.5, 16.5), Shore(4).CelerityDepthAt(29.5, 16.5), 3);
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
