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

    [Fact]
    public void GivesTheTablesProbableWaveHeightMidBand()
    {
        Assert.Equal(5.5, scale.WaveHeightAt((17.2 + 20.8) / 2), 3);
    }

    [Fact]
    public void RaisesWavesSteadilyWithTheWind()
    {
        var heights = Enumerable.Range(0, 80).Select(step => scale.WaveHeightAt(step * 0.5)).ToArray();

        Assert.True(heights.Zip(heights.Skip(1)).All(pair => pair.Second >= pair.First));
    }
}
