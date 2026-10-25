using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class ImmersionTests
{
    private const double StillSurface = 63;
    private const double SwimmingOffset = 1.221;
    private const double Tick = 1.0 / 60;

    private static readonly SwimConfig Swim = Fixtures.Config().Swim;

    [Fact]
    public void SwimsUnderACrestAboveTheStillWater()
    {
        var body = At(surface: StillSurface + 1.5, feet: StillSurface + 0.2);

        Assert.True(body.Swimming);
        Assert.True(body.FeetIn);
    }

    [Fact]
    public void StandsOutOfTheWaterInATroughBelowIt()
    {
        var body = At(surface: StillSurface - 1.5, feet: StillSurface - 1.2);

        Assert.False(body.Swimming);
        Assert.False(body.FeetIn);
    }

    [Fact]
    public void WadesWithTheChestAboveAndTheFeetBelow()
    {
        var body = At(surface: StillSurface - 0.5, feet: StillSurface - 1.2);

        Assert.False(body.Swimming);
        Assert.True(body.FeetIn);
    }

    [Fact]
    public void CountsTheShareUnderWaterAsVanillaDoes()
    {
        Assert.Equal(Swim.LeastShare, At(surface: StillSurface, feet: StillSurface).Share(Swim), 9);
        Assert.Equal(0.5 + Swim.LeastShare, At(surface: StillSurface + 0.5, feet: StillSurface - SwimmingOffset).Share(Swim), 9);
        Assert.Equal(1, At(surface: StillSurface + 3, feet: StillSurface).Share(Swim), 9);
    }

    [Fact]
    public void LiftsAStrokeInProportionToTheShareUnder()
    {
        var stroke = new Stroke(Jump: false, Moving: true, Upward: 0.2, Push: 6);

        var deep = Immersion.Lift(Swim, 1, stroke, Tick);
        var shallow = Immersion.Lift(Swim, 0.25, stroke, Tick);

        Assert.Equal(0.2 * 7 * Swim.StrokeLift, deep, 9);
        Assert.Equal(deep / 4, shallow, 9);
    }

    [Fact]
    public void HoldingJumpLiftsByTheTick()
    {
        var still = new Stroke(Jump: true, Moving: false, Upward: 0, Push: 1);

        Assert.Equal(Swim.JumpLiftPerTick * 0.5, Immersion.Lift(Swim, 0.5, still, Tick), 9);
        Assert.Equal(2 * Swim.JumpLiftPerTick * 0.5, Immersion.Lift(Swim, 0.5, still, 2 * Tick), 9);
    }

    [Fact]
    public void AMovingSwimmerBarelyUnderGetsNoLiftFromJump()
    {
        var moving = new Stroke(Jump: true, Moving: true, Upward: 0, Push: 1);

        Assert.Equal(0, Immersion.Lift(Swim, Swim.JumpLiftAboveShare, moving, Tick));
        Assert.True(Immersion.Lift(Swim, Swim.JumpLiftAboveShare + 0.01, moving, Tick) > 0);
    }

    [Fact]
    public void ACrestLiftsAStrokeTheBlocksWouldBarelyCarry()
    {
        var stroke = new Stroke(Jump: false, Moving: true, Upward: 0.2, Push: 1);
        var feet = StillSurface - SwimmingOffset + 0.05;

        var calm = Immersion.Lift(Swim, At(StillSurface, feet).Share(Swim), stroke, Tick);
        var crest = Immersion.Lift(Swim, At(StillSurface + 1, feet).Share(Swim), stroke, Tick);

        Assert.True(crest > 5 * calm);
    }

    private static Immersion At(double surface, double feet) => new(surface, feet, feet + SwimmingOffset);
}
