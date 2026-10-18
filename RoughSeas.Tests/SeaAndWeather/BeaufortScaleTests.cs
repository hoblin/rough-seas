using RoughSeas.SeaAndWeather;

namespace RoughSeas.Tests.SeaAndWeather;

public class BeaufortScaleTests
{
    private readonly BeaufortScale scale = new(Fixtures.Config().Beaufort);

    [Theory]
    [InlineData(0.1, 0, "Calm")]
    [InlineData(7.9, 4, "Moderate breeze")]
    [InlineData(17.2, 8, "Gale")]
    [InlineData(18, 8, "Gale")]
    [InlineData(40, 12, "Hurricane")]
    public void NamesTheForceOfAWind(double mps, int force, string name)
    {
        var row = scale.ForceAt(mps);

        Assert.Equal(force, row.Force);
        Assert.Equal(name, row.Name);
    }
}
