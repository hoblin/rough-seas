using RoughSeas.Surf;

namespace RoughSeas.Tests.Surf;

public class WaveEnergyTests
{
    private const int Size = 96;
    private const double DepthPerHeight = 1.28;
    private const double Number = 0.1;

    private static (float[] Gain, float[] Breaking) Along(Func<int, double> depthAlongX, double amplitude)
    {
        var depth = new float[Size * Size];
        for (var i = 0; i < depth.Length; i++) depth[i] = (float)depthAlongX(i % Size);
        var (gains, breaking) = WaveEnergy.Solve(Size, depth, [new WaveEnergy.Train(Number, 0, amplitude, new float[Size * Size])], DepthPerHeight, 1);
        return (gains[0], breaking);
    }

    private static int Middle(int x) => Size / 2 * Size + x;

    [Fact]
    public void DeepWaterLeavesAWaveAsItIs()
    {
        var (gain, breaking) = Along(_ => 1000, 1);

        Assert.Equal(1, gain[Middle(60)], 2);
        Assert.Equal(0, breaking[Middle(60)]);
    }

    [Fact]
    public void AWaveShoalsByItsEnergyFluxGreensLawInShallowWater()
    {
        var (gain, _) = Along(x => x < 48 ? 4 : 1, 0.01);

        Assert.InRange(gain[Middle(70)] / gain[Middle(30)], 1.25, 1.55);
    }

    [Fact]
    public void AWaveBreaksOverABarAndGoesOnSmallerPastIt()
    {
        static double Bar(int x) => x is >= 44 and < 56 ? 1 : 12;
        var (open, _) = Along(_ => 12, 1.5);
        var (gain, breaking) = Along(Bar, 1.5);

        Assert.True(breaking[Middle(50)] > 0.3, "breaks over the bar");
        Assert.True(breaking[Middle(30)] < 0.05, "doesn't break before the bar");
        Assert.True(gain[Middle(80)] < 0.8 * open[Middle(80)], "goes on smaller past it");
    }

    [Fact]
    public void ThereIsNoWaveOverLand()
    {
        var (gain, _) = Along(x => x < 48 ? 8 : -1, 0.5);

        Assert.Equal(0, gain[Middle(60)]);
    }

    [Fact]
    public void TheSeaBehindLandIsTheSeaMapsToSay()
    {
        var depth = new float[Size * Size];
        for (var i = 0; i < depth.Length; i++) depth[i] = i % Size == 40 && i / Size is >= 30 and < 66 ? -1 : 20;
        var (gains, _) = WaveEnergy.Solve(Size, depth, [new WaveEnergy.Train(Number, 0, 0.5, new float[Size * Size])], DepthPerHeight, 1);

        Assert.True(gains[0][Middle(60)] > 0.8);
    }

    [Fact]
    public void WaterShutInByLandHasWavesFromItsOwnUpwindShore()
    {
        var depth = new float[Size * Size];
        for (var i = 0; i < depth.Length; i++) depth[i] = i % Size is >= 30 and < 70 && i / Size is >= 30 and < 70 ? 20 : -1;
        var (gains, _) = WaveEnergy.Solve(Size, depth, [new WaveEnergy.Train(Number, 0, 0.5, new float[Size * Size])], DepthPerHeight, 1);

        Assert.True(gains[0][Middle(60)] > 0.8);
    }

    [Fact]
    public void WhereTheSeaIsQuieterItBreaksLessOverTheSameBed()
    {
        var depth = new float[Size * Size];
        for (var i = 0; i < depth.Length; i++) depth[i] = i % Size is >= 44 and < 56 ? 1 : 12;
        var quiet = new float[Size * Size];
        Array.Fill(quiet, 0.2f);
        var train = new WaveEnergy.Train(Number, 0, 1.5, new float[Size * Size]);
        var (_, full) = WaveEnergy.Solve(Size, depth, [train], DepthPerHeight, 1);
        var (_, calmer) = WaveEnergy.Solve(Size, depth, [train], DepthPerHeight, 1, null, quiet);

        Assert.True(calmer[Middle(50)] < full[Middle(50)]);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.8)]
    public void TheBrokenFractionIsTheRootOfBattjesAndJanssen(double share)
    {
        var fraction = WaveEnergy.BrokenFraction(share);

        Assert.Equal(0, 1 - fraction + share * Math.Log(fraction), 6);
    }
}
