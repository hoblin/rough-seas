using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class ScatterTests
{
    [Fact]
    public void WoodHoldsAsItsTablesSayOnAverage()
    {
        var draws = Draw(0.25);

        Assert.Equal(1, draws.Average(), 2);
    }

    [Fact]
    public void ItsStrengthVariesByTheMeasuredCoefficient()
    {
        var draws = Draw(0.25);
        double mean = draws.Average();
        double deviation = Math.Sqrt(draws.Average(draw => (draw - mean) * (draw - mean)));

        Assert.Equal(0.25, deviation / mean, 2);
    }

    [Fact]
    public void AMaterialOfNoKnownSpreadHoldsAsItsTablesSay()
    {
        Assert.All(Draw(0), draw => Assert.Equal(1, draw, 9));
    }

    private static double[] Draw(double variation)
    {
        var rand = new Random(7);
        return Enumerable.Range(0, 200000).Select(_ => Scatter.Of(variation, rand)).ToArray();
    }
}
