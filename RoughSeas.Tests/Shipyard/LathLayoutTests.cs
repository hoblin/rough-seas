using Newtonsoft.Json.Linq;
using RoughSeas.Shipyard;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Shipyard;

public class LathLayoutTests
{
    private static readonly Wood Oak = new("game", "oak");
    private static readonly Wood Birch = new("game", "birch");
    private static readonly LathLayout Lattice = LathLayout.Of(JToken.Parse("""{"kind": "lattice", "along": 7, "across": 7, "alongSpacingMetres": 0.1125, "acrossSpacingMetres": 0.16875, "toleranceMetres": 0.1}"""))!;
    private static readonly LathLayout Pair = LathLayout.Of(JToken.Parse("""{"kind": "pair", "beamMetres": 1.2, "toleranceMetres": 0.2}"""))!;

    [Fact]
    public void SevenLathsAlongAndSevenAcrossAllCrossingAreACoraclesLattice()
    {
        var laid = Lattice.Find(Grid(10, 20), 1.8);

        Assert.NotNull(laid);
        Assert.Equal(Oak, laid.Wood);
        Assert.Equal(14, laid.Laths.Length);
        Assert.Equal(10, laid.X, 6);
        Assert.Equal(20, laid.Z, 6);
    }

    [Fact]
    public void ALathThatMissesTheOthersBreaksTheLattice()
    {
        var laths = Grid(10, 20);
        laths[0] = laths[0] with { X = 13 };

        Assert.Null(Lattice.Find(laths, 1.8));
    }

    [Fact]
    public void ALatticeBunchedOrSpreadFarFromHerModelsSpacingMakesNoBlank()
    {
        Assert.Null(Lattice.Find(Grid(10, 20, alongSpacing: 0), 1.8));
        Assert.Null(Lattice.Find(Grid(10, 20, alongSpacing: 0.25), 1.8));
    }

    [Fact]
    public void ALatticeLaidTheOtherWayRoundLiesAlongItsCloserSpacedLaths() =>
        Assert.Equal(EnumAxis.Z, Lattice.Find(Grid(10, 20, alongSpacing: 0.16875, acrossSpacing: 0.1125), 1.8)?.Along);

    [Fact]
    public void SixAcrossIsNotYetALattice() => Assert.Null(Lattice.Find(Grid(10, 20).Take(13).ToList(), 1.8));

    [Fact]
    public void LathsTooShortForTheirPiecesMakeNoBlank() => Assert.Null(Lattice.Find(Grid(10, 20), 2.5));

    [Fact]
    public void LathsOfTwoWoodsMakeNoBlank()
    {
        var laths = Grid(10, 20);
        laths[3] = laths[3] with { Wood = Birch };

        Assert.Null(Lattice.Find(laths, 1.8));
    }

    [Fact]
    public void TwoLongLathsSideBySideAtHerBeamAreACurrachsGunwale()
    {
        var laid = Pair.Find([new LaidLath(Oak, 5.5, 10.4, EnumAxis.Z, 5), new LaidLath(Oak, 6.7, 10.5, EnumAxis.Z, 5)], 5);

        Assert.NotNull(laid);
        Assert.Equal(EnumAxis.Z, laid.Along);
        Assert.Equal(6.1, laid.X, 6);
        Assert.Equal(10.45, laid.Z, 6);
    }

    [Fact]
    public void TwoLathsTooFarApartOrAcrossEachOtherAreNoGunwale()
    {
        Assert.Null(Pair.Find([new LaidLath(Oak, 5.5, 10, EnumAxis.Z, 5), new LaidLath(Oak, 7.5, 10, EnumAxis.Z, 5)], 5));
        Assert.Null(Pair.Find([new LaidLath(Oak, 5.5, 10, EnumAxis.Z, 5), new LaidLath(Oak, 6.7, 10, EnumAxis.X, 5)], 5));
    }

    private static List<LaidLath> Grid(double x, double z, double alongSpacing = 0.1125, double acrossSpacing = 0.16875) =>
    [
        .. Enumerable.Range(-3, 7).Select(n => new LaidLath(Oak, x, z + n * alongSpacing, EnumAxis.X, 2)),
        .. Enumerable.Range(-3, 7).Select(n => new LaidLath(Oak, x + n * acrossSpacing, z, EnumAxis.Z, 2))
    ];
}
