using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Storms;

/// <summary>
/// A hull's set: the velocity the water has given her, on top of whatever else moves her (a rower's way through the
/// water, a push). It lives in her motion and goes through vanilla's physics there: each move sets her motion so that
/// after vanilla's water drag and swim drag she carries the set whole and the rest of her motion as vanilla would have
/// left it alone. Called from the physics thread and the tick, so it holds its own lock.
/// </summary>
public sealed class HullSet
{
    private readonly object gate = new();
    private double ownX;
    private double ownZ;
    private double waveX;
    private double waveZ;
    private double pivotX;
    private double pivotZ;
    private double heldX;
    private double heldZ;
    private bool met;

    public (double X, double Z) Velocity
    {
        get
        {
            lock (gate) return (waveX + ownX, waveZ + ownZ);
        }
    }

    /// <summary>Her set beside the waves' water at her place, m/s: the part forces change, and the part the other side takes over.</summary>
    public (double X, double Z) Own
    {
        get
        {
            lock (gate) return (ownX, ownZ);
        }
    }

    /// <summary>What her motion holds of the set now, blocks per tick.</summary>
    public (double X, double Z) Held
    {
        get
        {
            lock (gate) return (heldX, heldZ);
        }
    }

    /// <summary>The water moves her for <paramref name="seconds"/>.</summary>
    /// <returns>The water's push on her besides its drag, m/s².</returns>
    public (double X, double Z) Settle(HullDrag drag, WaterOnHull water, double yaw, double seconds)
    {
        lock (gate)
        {
            var (pushX, pushZ) = Push(water, yaw);
            var (relativeX, relativeZ) = drag.Settle(water.CurrentX - ownX, water.CurrentZ - ownZ, yaw, seconds, water.WayAlong, pushX, pushZ);
            ownX = water.CurrentX - relativeX;
            ownZ = water.CurrentZ - relativeZ;
            return Wash(water, pushX, pushZ, seconds);
        }
    }

    /// <summary>As <see cref="Settle"/> for a hull whose drag isn't known: she comes toward the current by this share of the difference.</summary>
    public (double X, double Z) Relax(WaterOnHull water, double yaw, double share, double seconds)
    {
        lock (gate)
        {
            var (pushX, pushZ) = Push(water, yaw);
            ownX += pushX * seconds + (water.CurrentX - ownX) * share;
            ownZ += pushZ * seconds + (water.CurrentZ - ownZ) * share;
            return Wash(water, pushX, pushZ, seconds);
        }
    }

    /// <summary>What the waves pushed her by in the last step, m/s², for her moving through their water: −(s·∇)u.</summary>
    public (double X, double Z) Pushed { get; private set; }

    private (double X, double Z) Wash(WaterOnHull water, double pushX, double pushZ, double seconds)
    {
        Pushed = (pushX, pushZ);
        var (washX, washZ) = met ? ((water.WaveX - waveX) / seconds + pushX, (water.WaveZ - waveZ) / seconds + pushZ) : (pushX, pushZ);
        (waveX, waveZ, met) = (water.WaveX, water.WaveZ, true);
        return (washX, washZ);
    }

    public void Push(double metresPerSecondX, double metresPerSecondZ)
    {
        lock (gate)
        {
            ownX += metresPerSecondX;
            ownZ += metresPerSecondZ;
        }
    }

    public (double X, double Z) Pull(HullDrag drag, WaterOnHull water, double yaw)
    {
        lock (gate) return drag.Pull(water.CurrentX - ownX, water.CurrentZ - ownZ, yaw, water.WayAlong);
    }

    public double Swing(HullDrag drag, WaterOnHull water, double yaw, double turnPerSecond, double seconds)
    {
        lock (gate) return drag.Swing(water.CurrentX - ownX, water.CurrentZ - ownZ, yaw, turnPerSecond, seconds);
    }

    private (double X, double Z) Push(WaterOnHull water, double yaw)
    {
        var (wayX, wayZ) = HullShape.ToWorld(water.WayAlong, 0, yaw);
        var (x, z) = water.Gradient.Along(ownX - water.CurrentX + wayX, ownZ - water.CurrentZ + wayZ);
        return (-x, -z);
    }

    /// <summary>
    /// Before one of vanilla's moves. <paramref name="kept"/> is the share of her motion vanilla's water or air drag
    /// will keep along the water in this move (1 where it is undone for a driving hull), <paramref name="stepFactor"/>
    /// this move's blocks per unit of motion, and <paramref name="swimming"/> whether vanilla's swim drag will act.
    /// </summary>
    public void Keep(Vec3d motion, double kept, double stepFactor, bool swimming)
    {
        lock (gate)
        {
            double Swim(double length) => swimming ? VesselDraught.SwimDrag(length, stepFactor) : 1;

            var (restX, restZ) = ((motion.X - heldX) * kept, (motion.Z - heldZ) * kept);
            var rest = Swim(Math.Sqrt(restX * restX + restZ * restZ));
            heldX = (waveX + ownX + pivotX) / WaterDrag.TicksPerSecond;
            heldZ = (waveZ + ownZ + pivotZ) / WaterDrag.TicksPerSecond;
            var (afterX, afterZ) = (restX / rest + heldX, restZ / rest + heldZ);
            // vanilla's swim drag is a step: at a speed past its offset it takes all it will, so what passes it is the most it takes
            var before = Swim(Math.Sqrt(afterX * afterX + afterZ * afterZ)) <= 1 ? 1 : VesselDraught.MostSwimDrag;
            motion.X = afterX * before / kept;
            motion.Z = afterZ * before / kept;
        }
    }

    /// <summary>After one of vanilla's moves: a collision that stopped an axis stopped the set on it.</summary>
    public void Collided(Vec3d motion)
    {
        lock (gate)
        {
            if (motion.X == 0) (ownX, pivotX, heldX) = (-waveX, 0, 0);
            if (motion.Z == 0) (ownZ, pivotZ, heldZ) = (-waveZ, 0, 0);
        }
    }

    /// <summary>Vanilla has just written her motion over from her forward speed; a side her physics runs on gives the set back.</summary>
    public void Rowed(Vec3d motion, bool moves)
    {
        lock (gate)
        {
            (heldX, heldZ) = moves ? ((waveX + ownX + pivotX) / WaterDrag.TicksPerSecond, (waveZ + ownZ + pivotZ) / WaterDrag.TicksPerSecond) : (0, 0);
            motion.X += heldX;
            motion.Z += heldZ;
        }
    }

    /// <summary>Her origin's velocity while she turns about a held bow, m/s; zero once she is let go.</summary>
    public void Pivot(double metresPerSecondX, double metresPerSecondZ)
    {
        lock (gate) (pivotX, pivotZ) = (metresPerSecondX, metresPerSecondZ);
    }

    /// <summary>This side takes her physics over with her own set: her motion becomes it, whatever the other side left in it, and the waves' water comes with the next step.</summary>
    public void Take(double setX, double setZ, Vec3d motion)
    {
        lock (gate)
        {
            (ownX, ownZ, waveX, waveZ, pivotX, pivotZ, met) = (setX, setZ, 0, 0, 0, 0, false);
            motion.X = heldX = ownX / WaterDrag.TicksPerSecond;
            motion.Z = heldZ = ownZ / WaterDrag.TicksPerSecond;
        }
    }

    /// <summary>She stops being carried here and now: what her motion holds of the set goes with it.</summary>
    public void Drop(Vec3d motion)
    {
        lock (gate)
        {
            motion.X -= heldX;
            motion.Z -= heldZ;
            (ownX, ownZ, waveX, waveZ, pivotX, pivotZ, heldX, heldZ, met) = (0, 0, 0, 0, 0, 0, 0, 0, false);
        }
    }

    /// <summary>Ashore or handed over: no set. What her motion still holds of it is vanilla's to take off.</summary>
    public void Clear()
    {
        lock (gate) (ownX, ownZ, waveX, waveZ, pivotX, pivotZ, heldX, heldZ, met) = (0, 0, 0, 0, 0, 0, 0, 0, false);
    }
}
