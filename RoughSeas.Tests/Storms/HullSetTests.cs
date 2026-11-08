using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Tests.Storms;

public class HullSetTests
{
    private const double TicksPerSecond = 60;
    private const double VanillaWaterDrag = 0.92;
    private const double PhysicsSeconds = 1 / 30.0;
    private const double TickSeconds = 0.05;
    private const double Yaw = 0;

    private static HullDrag Raft() => Fixtures.DragOf("boat-raft", Fixtures.Hull("boat-raft").Crewed());

    // one of vanilla's moves along the water: its water drag, then its swim drag
    private static void VanillaMove(Vec3d motion, double kept, double stepFactor)
    {
        motion.X *= kept;
        motion.Z *= kept;
        var swim = VesselDraught.SwimDrag(Math.Sqrt(motion.X * motion.X + motion.Z * motion.Z), stepFactor);
        motion.X /= swim;
        motion.Z /= swim;
    }

    private static double Kept(double seconds) => WaterDrag.Kept(VanillaWaterDrag, seconds);

    private static double Speed(Vec3d motion) => TicksPerSecond * Math.Sqrt(motion.X * motion.X + motion.Z * motion.Z);

    [Fact]
    public void ADriftingHullReachesTheWatersSpeedThroughVanillasDrag()
    {
        var drag = Raft();
        var set = new HullSet();
        var motion = new Vec3d();
        for (var t = 0.0; t < 600; t += PhysicsSeconds)
        {
            set.Settle(drag, 1, 0, Yaw, PhysicsSeconds);
            set.Keep(motion, Kept(PhysicsSeconds), PhysicsSeconds * TicksPerSecond, swimming: true);
            VanillaMove(motion, Kept(PhysicsSeconds), PhysicsSeconds * TicksPerSecond);
        }
        Assert.InRange(motion.X * TicksPerSecond, 0.95, 1.0);
        Assert.InRange(motion.Z, -1e-9, 1e-9);
    }

    [Fact]
    public void AHullThatHasVanillasDragUndoneKeepsHerSetWithoutPilingItUp()
    {
        var set = new HullSet();
        var motion = new Vec3d();
        set.Take(1.5, 0, motion);
        for (var i = 0; i < 2000; i++)
        {
            set.Keep(motion, 1, PhysicsSeconds * TicksPerSecond, swimming: true);
            VanillaMove(motion, 1, PhysicsSeconds * TicksPerSecond);
        }
        Assert.Equal(1.5, motion.X * TicksPerSecond, 9);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(3.5)]
    [InlineData(5.0)]
    public void ARowersWayAndTheSetAddUpWhateverSwimDragTheirSumMeets(double wayMps)
    {
        var step = PhysicsSeconds * TicksPerSecond;
        var set = new HullSet();
        var motion = new Vec3d();
        set.Take(1, 0, motion);
        var forwardSpeed = wayMps / TicksPerSecond * VesselDraught.MostSwimDrag;
        var realized = forwardSpeed / VesselDraught.SwimDrag(forwardSpeed, step);
        for (var i = 0; i < 200; i++)
        {
            // vanilla writes her motion over from her forward speed each frame, then the set comes back
            motion.X = forwardSpeed;
            motion.Z = 0;
            set.Rowed(motion, moves: true);
            set.Keep(motion, 1, step, swimming: true);
            VanillaMove(motion, 1, step);
            Assert.Equal((realized + 1 / TicksPerSecond) * TicksPerSecond, motion.X * TicksPerSecond, 9);
        }
    }

    [Fact]
    public void WhatElseMovesHerStillMeetsVanillasDragAndTheSetDoesNot()
    {
        var set = new HullSet();
        var motion = new Vec3d();
        set.Take(0.5, 0, motion);
        motion.Z += 0.5 / TicksPerSecond;
        var kept = Kept(PhysicsSeconds);
        for (var i = 0; i < 300; i++)
        {
            set.Keep(motion, kept, PhysicsSeconds * TicksPerSecond, swimming: true);
            VanillaMove(motion, kept, PhysicsSeconds * TicksPerSecond);
        }
        Assert.Equal(0.5, motion.X * TicksPerSecond, 9);
        Assert.InRange(motion.Z * TicksPerSecond, 0, 1e-3);
    }

    [Fact]
    public void ACollisionStopsTheSetOnTheAxisItStopped()
    {
        var set = new HullSet();
        var motion = new Vec3d();
        set.Take(1, 1, motion);
        motion.X = 0;
        set.Collided(motion);
        Assert.Equal((0.0, 1.0), set.Velocity);
    }

    [Fact]
    public void AMooringStopsOnlyTheSetAwayFromIt()
    {
        var set = new HullSet();
        var motion = new Vec3d();
        set.Take(1, 2, motion);
        set.Hold(1, 0);
        Assert.Equal((0.0, 2.0), set.Velocity);
        set.Hold(-1, 0);
        Assert.Equal((0.0, 2.0), set.Velocity);
    }

    [Fact]
    public void ADraggingAnchorSlowsHerSetAwayFromItByItsPullAndNeverTurnsItBack()
    {
        var set = new HullSet();
        set.Take(1, 2, new Vec3d());
        Assert.Equal(0.25, set.Slow(1, 0, 0.25));
        Assert.Equal((0.75, 2.0), set.Velocity);
        Assert.Equal(0.75, set.Slow(1, 0, 5));
        Assert.Equal((0.0, 2.0), set.Velocity);
        Assert.Equal(0, set.Slow(-1, 0, 1));
    }

    [Fact]
    public void TheOtherSidesMotionIsNotCountedTwiceWhenHerPhysicsChangesHands()
    {
        var set = new HullSet();
        var motion = new Vec3d(9 / TicksPerSecond, 0, 0);
        set.Take(1, 0, motion);
        Assert.Equal(1, motion.X * TicksPerSecond, 9);
        set.Keep(motion, 1, PhysicsSeconds * TicksPerSecond, swimming: true);
        Assert.Equal(1, motion.X * TicksPerSecond, 9);
    }

    [Fact]
    public void AShoreSheHasNoSetAndHerMotionIsVanillasToTakeOff()
    {
        var set = new HullSet();
        var motion = new Vec3d();
        set.Take(1, 0, motion);
        set.Clear();
        Assert.Equal((0.0, 0.0), set.Velocity);
        set.Keep(motion, 0.5, PhysicsSeconds * TicksPerSecond, swimming: false);
        Assert.Equal(1, motion.X * TicksPerSecond, 9);
    }

    [Fact]
    public void ALoadedHullComesUpToTheCurrentMoreSlowlyThanAnEmptyOne()
    {
        var hull = Fixtures.Hull("dugout");
        var empty = CarriedAfter(Fixtures.DragOf("dugout", hull.Crewed(bodies: 0, kit: 0)), seconds: 10);
        var loaded = CarriedAfter(Fixtures.DragOf("dugout", hull.Crewed(containers: 400)), seconds: 10);
        Assert.True(loaded < empty, $"loaded {loaded:F3}, empty {empty:F3}");
    }

    private static double CarriedAfter(HullDrag drag, double seconds)
    {
        var set = new HullSet();
        for (var t = 0.0; t < seconds; t += TickSeconds) set.Settle(drag, 0, 1, Yaw, TickSeconds);
        return set.Velocity.Z;
    }
}
