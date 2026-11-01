using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class BodyTests
{
    private const double Gravity = 9.81;

    private static readonly BodyConfig Config = Fixtures.Config().Body;
    private static readonly Body Man = Body.Of(Config, Config.Builds["male"]);
    private static readonly Body Woman = Body.Of(Config, Config.Builds["female"]);

    [Fact]
    public void HoldsWhatHydrostaticWeighingGivesAMan()
    {
        // 75 kg at 18% fat is 70.9 L of tissue; a 40 year old man of 1.75 m keeps 3.4 L of air after a quiet breath out
        Assert.Equal(70.9, Man.TissueLitres, 1);
        Assert.Equal(3.4 + 0.25, Man.BreathingLitres, 1);
    }

    [Fact]
    public void FloatsAManInTheSeaWithAFewPercentAboveAndTheWaterAtTheFace()
    {
        var waterline = Afloat(Man, Config.SeawaterDensity);
        var above = 1 - Man.LitresUnder(waterline, Man.BreathingLitres) / Man.Litres(Man.BreathingLitres);

        Assert.InRange(above, 0.01, 0.03);
        Assert.InRange(waterline / Man.HeightMetres, 0.870, 1.0);
    }

    [Fact]
    public void SinksAManInFreshWaterWhileHeBreathes()
    {
        Assert.True(Man.Litres(Man.BreathingLitres) * Config.FreshwaterDensity / 1000 < Man.MassKg);
    }

    [Fact]
    public void FloatsAWomanInFreshWaterWhereAManSinks()
    {
        Assert.True(Woman.Litres(Woman.BreathingLitres) * Config.FreshwaterDensity / 1000 > Woman.MassKg);
    }

    [Fact]
    public void PutsTheWholeBodyUnderOnlyOnceTheWaterIsOverItsHead()
    {
        var lungs = Man.BreathingLitres;

        Assert.Equal(0, Man.LitresUnder(0, lungs));
        Assert.Equal(Man.Litres(lungs), Man.LitresUnder(Man.HeightMetres, lungs), 6);
        Assert.Equal(Man.Litres(lungs), Man.LitresUnder(Man.HeightMetres + 3, lungs), 6);
        Assert.True(Man.LitresUnder(Man.HeightMetres - 0.1, lungs) < Man.Litres(lungs));
    }

    [Fact]
    public void LiftsTheBodyOnlyWhileItDisplacesMoreThanItWeighs()
    {
        var waterline = Afloat(Man, Config.SeawaterDensity);
        var lift = (double waterline) => Body.Lift(Man.LitresUnder(waterline, Man.BreathingLitres), Config.SeawaterDensity, Man.MassKg, Config.AddedMass, Gravity);

        Assert.Equal(0, lift(waterline), 3);
        Assert.True(lift(waterline + 0.05) > 0);
        Assert.True(lift(waterline - 0.05) < 0);
        Assert.Equal(-Gravity, lift(0), 6);
    }

    [Fact]
    public void MovesABodyAsDenseAsTheWaterWithTheWaterUnderTheWaves()
    {
        const double waterAcceleration = -1.2;
        var litres = Man.MassKg / Config.SeawaterDensity * 1000;

        Assert.Equal(waterAcceleration, Body.Lift(litres, Config.SeawaterDensity, Man.MassKg, Config.AddedMass, Gravity, waterAcceleration), 6);
    }

    [Fact]
    public void SinksAManInMailWhoFloatsWithout()
    {
        const double mailKg = 10;
        var lungs = Man.BreathingLitres;
        var whole = Man.Litres(lungs) + mailKg / Config.ArmourDensity;

        Assert.True(Body.Lift(whole, Config.SeawaterDensity, Man.MassKg + mailKg, Config.AddedMass, Gravity) < 0);
        Assert.True(Body.Lift(Man.Litres(lungs), Config.SeawaterDensity, Man.MassKg, Config.AddedMass, Gravity) > 0);
    }

    [Fact]
    public void LosesItsHoldInWaterChurnedWithAir()
    {
        var air = Config.Breaking.Aeration;
        var whole = Man.Litres(Man.BreathingLitres);

        Assert.True(Body.Lift(whole, Config.SeawaterDensity * (1 - air), Man.MassKg, Config.AddedMass, Gravity) < 0);
    }

    private static double Afloat(Body body, double density)
    {
        var (low, high) = (0.0, body.HeightMetres);
        for (var i = 0; i < 60; i++)
        {
            var mid = (low + high) / 2;
            if (body.LitresUnder(mid, body.BreathingLitres) * density / 1000 < body.MassKg) low = mid;
            else high = mid;
        }
        return (low + high) / 2;
    }
}
