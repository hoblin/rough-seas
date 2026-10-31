using RoughSeas.Shipyard;

namespace RoughSeas.Tests.Shipyard;

public class HeldWorkTests
{
    private readonly HeldWork work = new();

    [Fact]
    public void TheHoldGoesOnUntilItsTimeIsUp()
    {
        Assert.True(work.Step(2.9f, 3));
        Assert.False(work.Step(3, 3));
    }

    [Fact]
    public void TheWorkIsDoneOnlyIfHeldItsWholeTime()
    {
        Assert.True(work.Stop(null!, 2.95f, 3));
        Assert.False(work.Stop(null!, 2.5f, 3));
        Assert.False(work.Stop(null!));
    }
}
