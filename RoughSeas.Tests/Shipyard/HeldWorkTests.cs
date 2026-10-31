using RoughSeas.Shipyard;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Shipyard;

public class HeldWorkTests
{
    private readonly HeldWork work = new();
    private readonly EntityAgent holder = new() { EntityId = 7 };

    [Fact]
    public void TheHoldGoesOnUntilItsTimeIsUp()
    {
        Assert.True(work.Step(2.9f, 3));
        Assert.False(work.Step(3, 3));
    }

    [Fact]
    public void TheWorkIsDoneOnlyIfHeldItsWholeTime()
    {
        work.Start(holder);
        Assert.False(work.Stop(holder, 2.5f, 3));

        work.Start(holder);
        Assert.True(work.Stop(holder, 2.95f, 3));
    }

    [Fact]
    public void AHoldIsDoneOnceHoweverOftenItsEndIsReported()
    {
        work.Start(holder);

        Assert.True(work.Stop(holder, 3, 3));
        Assert.False(work.Stop(holder, 3, 3));
    }

    [Fact]
    public void EachHolderHoldsTheirOwn()
    {
        var other = new EntityAgent { EntityId = 8 };
        work.Start(holder);

        Assert.False(work.Stop(other, 3, 3));
        Assert.True(work.Stop(holder, 3, 3));
    }

    [Fact]
    public void ACancelledHoldDoesNoWork()
    {
        work.Start(holder);
        work.Stop(holder);

        Assert.False(work.Stop(holder, 3, 3));
    }
}
