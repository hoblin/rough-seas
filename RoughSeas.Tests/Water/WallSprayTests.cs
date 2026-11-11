using RoughSeas.Water;

namespace RoughSeas.Tests.Water;

public class WallSprayTests
{
    private static readonly WallSpray Spray = new(ShoreFixtures.Config.Faces, ShoreFixtures.Config.RunUp.SpectralPeriodRatio);

    [Fact]
    public void ADropsPlaceInTheSpreadIsTheShareOfWaterHigherThanIt()
    {
        var faces = ShoreFixtures.Config.Faces;
        var atCrest = Math.Exp(-faces.ImpulsiveFreeboard);
        var above = faces.ImpulsivePowerFactor / faces.ImpulsiveFactor * Math.Pow(1.5, -faces.ImpulsivePower);
        var speed = Spray.Wave(4, 10, 3, 4, 7).Speed(above / atCrest);
        Assert.Equal(2, speed * speed / (2 * 9.81), 6);
    }

    [Fact]
    public void AHigherWaveThrowsMoreWater() => Assert.True(Spray.Wave(6, 10, 4, 6, 8).Litres > Spray.Wave(2, 10, 4, 2, 5).Litres);

    [Fact]
    public void WaterThrownHigherIsLess() => Assert.True(Spray.Wave(4, 10, 3, 8, 7).Litres < Spray.Wave(4, 10, 3, 4, 7).Litres);

    [Fact]
    public void NoDropOutrunsTheMeasuredJet()
    {
        var impact = Spray.Wave(4, 10, 3, 4, 7);
        Assert.Equal(ShoreFixtures.Config.Faces.JetSpeedRatio * 7, impact.TopSpeed, 6);
        Assert.True(impact.Speed(1e-9) <= impact.TopSpeed);
    }

    [Fact]
    public void TheTopOfTheSpreadThrowsDropsHighest()
    {
        var impact = Spray.Wave(4, 10, 3, 4, 7);
        Assert.True(impact.Speed(0.01) > impact.Speed(0.9));
    }

    [Fact]
    public void ASheetsSprayStopsAtItsHead()
    {
        var impact = Spray.Sheet(0.2, 0.5);
        Assert.Equal(Math.Sqrt(2 * 9.81 * 0.5), impact.TopSpeed, 6);
        Assert.True(impact.Speed(1e-9) <= impact.TopSpeed + 1e-9);
    }

    [Fact]
    public void AStoppedSheetThrowsAllItsWaterAtTheStep() => Assert.Equal(200, Spray.Sheet(0.2, 0.5).Litres, 6);

    [Fact]
    public void ASmallStrikeKeepsItsVolumeInSmallParcels()
    {
        var (count, side) = Spray.Parcels(10);
        Assert.True(side >= ShoreFixtures.Config.Faces.SmallestParcelSide);
        Assert.Equal(10.0, count * side * side * side * 1000, 6);
    }

    [Fact]
    public void AGreatStrikeThrowsLargerParcelsWithinItsBudget()
    {
        var (count, side) = Spray.Parcels(4000);
        Assert.Equal(ShoreFixtures.Config.Faces.ParcelsPerMetre, count);
        Assert.Equal(Math.Cbrt(4000.0 / count / 1000), side, 6);
    }

    [Fact]
    public void WaterTooLittleForAParcelThrowsNone() => Assert.Equal(0, Spray.Parcels(0.01).Count);
}
