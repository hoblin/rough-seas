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
    private const double AlongX = -Math.PI / 2;

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
            set.Settle(drag, new WaterOnHull(1, 0), Yaw, PhysicsSeconds);
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
    public void AHullLyingInTheSwellMovesWithItsWaterWhateverHerDrag()
    {
        const double sway = 1.2;
        const double turning = 2 * Math.PI / 8;
        foreach (var drag in new[] { Raft(), Fixtures.DragOf("dugout", Fixtures.Hull("dugout").Crewed()) })
        {
            var set = new HullSet();
            for (var t = 0.0; t < 60; t += TickSeconds)
            {
                var water = new WaterOnHull(0, 0, sway * Math.Sin(turning * t), sway * Math.Cos(turning * t), new FlowGradient(0.1 * Math.Cos(turning * t), 0, 0));
                set.Settle(drag, water, Yaw, TickSeconds);

                Assert.Equal((water.X, water.Z), set.Velocity);
            }
        }
    }

    [Fact]
    public void AHullRunningDownAWavesFaceIsCarriedAheadOfItsWater()
    {
        const double water = 1.0;
        var face = new FlowGradient(-0.3, 0, 0);
        var drag = Raft();
        var set = new HullSet();
        var on = new WaterOnHull(0, 0, water, 0, face, 1.0);
        var wash = (X: 0.0, Z: 0.0);
        for (var t = 0.0; t < 600; t += TickSeconds) wash = set.Settle(drag, on, AlongX, TickSeconds);

        var (pullX, _) = set.Pull(drag, on, AlongX);
        Assert.True(set.Velocity.X > water, $"carried at {set.Velocity.X:F2} m/s in water running {water} m/s");
        Assert.True(wash.X > 0, $"pushed {wash.X:F3} m/s2");
        Assert.Equal(-drag.Newtons(wash.X, 0, AlongX).X, pullX, 0.01 * drag.Newtons(wash.X, 0, AlongX).X);
    }

    [Fact]
    public void HerOwnWayAlreadyPaysItsDragAndTheSetMeetsOnlyWhatTheWaterAddsToIt()
    {
        var drag = Raft();
        var set = new HullSet();

        Assert.Equal(0, set.Pull(drag, new WaterOnHull(0, 0, WayAlong: 1.5), AlongX).X, 9);
        Assert.True(Math.Abs(set.Pull(drag, new WaterOnHull(-0.5, 0, WayAlong: 1.5), AlongX).X) > Math.Abs(set.Pull(drag, new WaterOnHull(-0.5, 0), AlongX).X));
    }

    [Fact]
    public void WhatSheIsSetBesideTheWavesWaterIsWhatPassesToTheOtherSide()
    {
        var drag = Raft();
        var set = new HullSet();
        set.Settle(drag, new WaterOnHull(0, 0, 1.1, 0), Yaw, TickSeconds);

        Assert.Equal((1.1, 0.0), set.Velocity);
        Assert.Equal((0.0, 0.0), set.Own);
    }

    [Fact]
    public void APushAddsToHerSetAsItIs()
    {
        var set = new HullSet();
        set.Take(1, 2, new Vec3d());
        set.Push(0.25, -0.5);
        Assert.Equal((1.25, 1.5), set.Velocity);
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
        for (var t = 0.0; t < seconds; t += TickSeconds) set.Settle(drag, new WaterOnHull(0, 1), Yaw, TickSeconds);
        return set.Velocity.Z;
    }
}
