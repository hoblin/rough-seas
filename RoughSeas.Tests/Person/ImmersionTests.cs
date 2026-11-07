using RoughSeas.Person;

namespace RoughSeas.Tests.Person;

public class ImmersionTests
{
    [Fact]
    public void SwimsWhileTheChestIsUnder()
    {
        Assert.True(new Immersion(100, 98.5, 99.7).Swimming);
        Assert.False(new Immersion(100, 99, 100.2).Swimming);
    }

    [Fact]
    public void HasItsFeetInWhileTheyAreUnder()
    {
        Assert.True(new Immersion(100, 99, 100.2).FeetIn);
        Assert.False(new Immersion(100, 100.1, 101.3).FeetIn);
    }

    [Fact]
    public void MeasuresTheChestsDepth()
    {
        Assert.Equal(0.3, new Immersion(100, 98.5, 99.7).Depth, 6);
        Assert.Equal(-0.2, new Immersion(100, 99, 100.2).Depth, 6);
    }
}
