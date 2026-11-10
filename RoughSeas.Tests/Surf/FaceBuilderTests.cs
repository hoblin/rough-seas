using RoughSeas.Shore;
using RoughSeas.Surf;
using RoughSeas.Tests.Shore;

namespace RoughSeas.Tests.Surf;

public class FaceBuilderTests
{
    private const int Size = 32;
    private const int Level = 100;
    private static readonly FaceBuilder Builder = new(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces);
    private static readonly BedConfig Stone = ShoreFixtures.Bed("stone");

    private static SeabedMap Wall(int columns, double rise, double depth = 10)
    {
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (x < columns) map.Put(x, z, new ShoreCell(ShoreKind.Water, depth, Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Land, rise, Stone, 1), Level);
        }
        return map;
    }

    private static (SurfField Field, IReadOnlyList<WallFace> Walls) Built(SeabedMap map, double height = 2, double period = 8)
    {
        var field = new SurfField(0, 0, Size, []);
        var walls = Builder.Build(map, (_, _) => new Sea(height, period), field);
        return (field, walls);
    }

    [Fact]
    public void FindsAFaceAlongEveryCellOfAWall() => Assert.Equal(Size, Built(Wall(16, 5)).Walls.Count);

    [Fact]
    public void AFaceKnowsHowHighItStands() => Assert.All(Built(Wall(16, 5)).Walls, wall => Assert.Equal(5, wall.Rise, 6));

    [Fact]
    public void ADeepWallReflectsTheWave() => Assert.All(Built(Wall(16, 5, 20)).Walls, wall => Assert.True(wall.Reflection > 0.9));

    [Fact]
    public void AWallsNormalPointsOutToSea()
    {
        var (field, _) = Built(Wall(16, 5));
        var face = field.FaceAt(10.5, 16.5);
        Assert.Equal(-1, face.NormalX, 6);
        Assert.Equal(0, face.NormalZ, 6);
    }

    [Fact]
    public void TheDistanceToAFaceGrowsOutToSea()
    {
        var (field, _) = Built(Wall(16, 5));
        Assert.Equal(0.5, field.FaceAt(15.5, 16.5).Distance, 3);
        Assert.Equal(5.5, field.FaceAt(10.5, 16.5).Distance, 3);
    }

    [Fact]
    public void LandIsOnTheFarSideOfTheFace()
    {
        var (field, _) = Built(Wall(16, 5));
        Assert.Equal(FaceBuilder.LandDistance, field.Face[(16 * Size + 20) * SurfField.Channels]);
    }

    [Fact]
    public void AStaircaseAveragesToItsDiagonal()
    {
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (x + z < 32) map.Put(x, z, new ShoreCell(ShoreKind.Water, 10, Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Land, 5, Stone, 1), Level);
        }
        var walls = Built(map).Walls.Where(wall => wall.X is > 10 and < 20).ToList();
        Assert.NotEmpty(walls);
        Assert.All(walls, wall => Assert.Equal(Math.Sqrt(0.5), Math.Abs(wall.NormalX), 1));
    }

    [Fact]
    public void WaterWithNoFaceBesideItHasNone()
    {
        var map = Wall(Size, 5);
        Assert.Empty(Built(map).Walls);
    }
}
