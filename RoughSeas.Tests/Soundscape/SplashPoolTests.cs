using RoughSeas.Soundscape;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Soundscape;

public class SplashPoolTests
{
    private static readonly AssetLocation[] Files = Enumerable.Range(1, 10).Select(i => new AssetLocation($"roughseas:sounds/water/splash/{i:00}.ogg")).ToArray();

    private readonly SplashPool pool = new(Files, fullFromMetres: 1);
    private readonly Random rand = new(7);

    [Fact]
    public void NeverPlaysTheSameSplashTwiceInARow()
    {
        var picked = new List<AssetLocation?>();
        for (var i = 0; i < 200; i++) picked.Add(pool.Pick(0.5, rand, picked.LastOrDefault()));
        Assert.All(picked.Zip(picked.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void KeepsEachHullFromRepeatingWhileHullsTakeTurns()
    {
        var (first, second) = (new List<AssetLocation?>(), new List<AssetLocation?>());
        for (var i = 0; i < 100; i++)
        {
            first.Add(pool.Pick(3, rand, first.LastOrDefault()));
            second.Add(pool.Pick(3, rand, second.LastOrDefault()));
        }
        Assert.All(first.Zip(first.Skip(1)).Concat(second.Zip(second.Skip(1))), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void GivesASmallWaveASmallSplash()
    {
        var picked = Enumerable.Range(0, 50).Select(_ => Index(pool.Pick(0.05, rand))).ToList();
        Assert.All(picked, index => Assert.InRange(index, 0, 1));
    }

    [Fact]
    public void GivesAWaveOfFullHeightTheFullestSplashes()
    {
        var picked = Enumerable.Range(0, 50).Select(_ => Index(pool.Pick(3, rand))).ToList();
        Assert.All(picked, index => Assert.InRange(index, 8, 9));
    }

    [Fact]
    public void PlaysItsOnlySplashForEveryWave()
    {
        var single = new SplashPool([Files[0]], fullFromMetres: 1);
        Assert.Equal(Files[0], single.Pick(0.5, rand));
        Assert.Equal(Files[0], single.Pick(0.5, rand, Files[0]));
    }

    [Fact]
    public void StaysSilentWithoutFiles() => Assert.Null(new SplashPool([], fullFromMetres: 1).Pick(0.5, rand));

    private static int Index(AssetLocation? file) => Array.IndexOf(Files, file);
}
