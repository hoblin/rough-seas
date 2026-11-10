using RoughSeas.Shore;
using RoughSeas.Surf;
using RoughSeas.Tests.Shore;

namespace RoughSeas.Tests.Surf;

public class FaceBuilderTests
{
    private const int Size = 32;
    private const int Level = 100;
    private static readonly FaceBuilder Builder = new(new WallReflection(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio), ShoreFixtures.Config.Faces, 1.28);
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
    public void BothSidesOfAThinWallLookTheirOwnWay()
    {
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (z is >= 14 and <= 17) map.Put(x, z, new ShoreCell(ShoreKind.Land, 5, Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Water, 10, Stone, 1), Level);
        }
        var walls = Built(map, 4, 12).Walls;
        Assert.All(walls.Where(wall => wall.Z == 13 && wall.SideZ == 1), wall => Assert.Equal(-1, wall.NormalZ, 3));
        Assert.All(walls.Where(wall => wall.Z == 18 && wall.SideZ == -1), wall => Assert.Equal(1, wall.NormalZ, 3));
    }

    [Fact]
    public void AOneBlockWallLeavesItsNormalToTheWaterOnEachSide()
    {
        var map = new SeabedMap(0, 0, Size);
        for (var x = 0; x < Size; x++)
        for (var z = 0; z < Size; z++)
        {
            if (z == 14) map.Put(x, z, new ShoreCell(ShoreKind.Land, 5, Stone, 1), Level);
            else map.Put(x, z, new ShoreCell(ShoreKind.Water, 10, Stone, 1), Level);
        }
        var (field, _) = Built(map, 4, 12);
        Assert.Equal(0, field.Get(field.Face, 16, 14, 2));
        Assert.Equal(-1, field.FaceAt(16.5, 13.5).NormalZ, 3);
    }

    [Fact]
    public void AFaceBelowTheCrestDoesNotStandAboveIt() => Assert.All(Built(Wall(16, 0.125), 2, 8).Walls, wall => Assert.False(wall.StandsAboveCrest));

    [Fact]
    public void AShallowToeHoldsTheCrestBelowAFaceTheOpenSeaWouldOvertop() =>
        Assert.All(Built(Wall(16, 1.2, 1.5), 6, 10).Walls, wall => Assert.True(wall.StandsAboveCrest));

    [Fact]
    public void AFaceAboveTheCrestStandsAboveIt() => Assert.All(Built(Wall(16, 6), 2, 8).Walls, wall => Assert.True(wall.StandsAboveCrest));

    [Fact]
    public void ALandTexelBesideAFaceCarriesItsReflectionToTheWallVertex()
    {
        var (field, walls) = Built(Wall(16, 6, 20));
        Assert.Equal((float)walls[0].Reflection, field.Face[(walls[0].Z * Size + walls[0].X + 1) * SurfField.Channels + 3]);
    }

    [Fact]
    public void AFaceBesideAWaterColumnUnderADeckStandsAsAWall()
    {
        var map = Wall(16, 5);
        map.Put(16, 8, new ShoreCell(ShoreKind.Water, 3, Stone, 1), Level - 4);
        map.SetFace(16, 8, Level + 6, Stone);
        Assert.Contains(Built(map).Walls, wall => wall is { X: 15, Z: 8, SideX: 1 } && Math.Abs(wall.Rise - (Level + 6 - (Level + 7.0 / 8))) < 1e-9);
    }

    [Fact]
    public void WaterWithNoFaceBesideItHasNone()
    {
        var map = Wall(Size, 5);
        Assert.Empty(Built(map).Walls);
    }
}
