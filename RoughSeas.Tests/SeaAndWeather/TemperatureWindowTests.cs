using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class TemperatureWindowTests
{
    private readonly TemperatureWindow hurricaneWater = new() { Min = 24, Optimum = 29, Max = 40 };

    [Theory]
    [InlineData(20, 0)]
    [InlineData(24, 0)]
    [InlineData(26.5, 0.5)]
    [InlineData(29, 1)]
    [InlineData(34.5, 0.5)]
    [InlineData(40, 0)]
    public void WeighsATemperatureByHowCloseItIsToTheOptimum(double celsius, double weight)
    {
        Assert.Equal(weight, hurricaneWater.Weight(celsius), 3);
    }
}
