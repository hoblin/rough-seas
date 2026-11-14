using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Tests.Vessels;

public class BoatsTests
{
    [Fact]
    public void VanillasBoatIsABoat()
    {
        Assert.Equal(typeof(EntityBoat), Boats.Of(new EntityBoat())?.Type);
    }

    [Fact]
    public void AVesselOfOursIsVanillasKindOfBoat()
    {
        Assert.Equal(typeof(EntityBoat), Boats.Of(new EntityVessel())?.Type);
    }

    [Fact]
    public void AnEntityThatIsNoBoatIsNone()
    {
        Assert.False(Boats.Is(new NoBoat()));
        Assert.False(Boats.Is(null));
    }

    [Fact]
    public void AnotherModsCopyOfVanillasBoatJoinsTheBoats()
    {
        Boats.Add(typeof(CopiedBoat));
        var boat = new CopiedBoat { ForwardSpeed = 0.3, mountAngle = new Vec3f(0.1f, 0, 0.2f), MountAnimations = new() { ["idle"] = "sitboat" } };

        var kind = Boats.Of(boat)!;
        Assert.Equal(typeof(CopiedBoat), kind.Type);
        Assert.Equal(0.3, kind.ForwardSpeed(boat));
        Assert.Equal(0.2f, kind.MountAngle(boat).Z);
        Assert.Equal("sitboat", kind.MountAnimations(boat)["idle"]);
        kind.SetSpeedMultiplier(boat, 0.5f);
        Assert.Equal(0.5f, boat.SpeedMultiplier);
    }

    [Fact]
    public void APatchReachesTheCopiesAddedUntilItIsDisposedButNotVanillasOwn()
    {
        var reached = new List<Type>();
        using (Boats.ReachCopies(boat => reached.Add(boat.Type)))
        {
            Boats.Add(typeof(LaterBoat));
        }
        Boats.Add(typeof(LastBoat));

        Assert.DoesNotContain(typeof(EntityBoat), reached);
        Assert.Contains(typeof(LaterBoat), reached);
        Assert.DoesNotContain(typeof(LastBoat), reached);
    }

    [Fact]
    public void ACopyRemovedIsNoLongerABoat()
    {
        Boats.Add(typeof(RemovedBoat));
        Boats.Remove(typeof(RemovedBoat));

        Assert.False(Boats.Is(new RemovedBoat()));
        Assert.True(Boats.Is(new EntityBoat()));
    }

    [Fact]
    public void AClassMissingABoatsMembersIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => Boats.Add(typeof(NoBoat)));
        Assert.False(Boats.Is(new NoBoat()));
    }

    private abstract class BoatCopy : Entity
    {
        public double ForwardSpeed;
        public Vec3f mountAngle = new();
        public Dictionary<string, string> MountAnimations = [];

        public virtual float SpeedMultiplier { get; set; } = 1;
    }

    private class CopiedBoat : BoatCopy
    {
        public override double SwimmingOffsetY => 0.5;

        public virtual void OnRenderFrame(float dt, EnumRenderStage stage) { }

        protected virtual void updateBoatAngleAndMotion(float dt) { }
    }

    private class LaterBoat : BoatCopy
    {
        public override double SwimmingOffsetY => 0.5;

        public virtual void OnRenderFrame(float dt, EnumRenderStage stage) { }

        protected virtual void updateBoatAngleAndMotion(float dt) { }
    }

    private class LastBoat : BoatCopy
    {
        public override double SwimmingOffsetY => 0.5;

        public virtual void OnRenderFrame(float dt, EnumRenderStage stage) { }

        protected virtual void updateBoatAngleAndMotion(float dt) { }
    }

    private class RemovedBoat : BoatCopy
    {
        public override double SwimmingOffsetY => 0.5;

        public virtual void OnRenderFrame(float dt, EnumRenderStage stage) { }

        protected virtual void updateBoatAngleAndMotion(float dt) { }
    }

    private class NoBoat : Entity;
}
