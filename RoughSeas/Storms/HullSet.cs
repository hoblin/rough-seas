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
    private double x;
    private double z;
    private double pivotX;
    private double pivotZ;
    private double heldX;
    private double heldZ;

    public (double X, double Z) Velocity
    {
        get
        {
            lock (gate) return (x, z);
        }
    }

    /// <summary>What her swing about a held bow adds to her motion, blocks per tick: not hers to lose to a mooring.</summary>
    public (double X, double Z) PivotMotion
    {
        get
        {
            lock (gate) return (pivotX / WaterDrag.TicksPerSecond, pivotZ / WaterDrag.TicksPerSecond);
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

    /// <summary>The water's drag brings her toward its velocity for <paramref name="seconds"/>.</summary>
    public void Settle(HullDrag drag, double waterX, double waterZ, double yaw, double seconds)
    {
        lock (gate)
        {
            var (relativeX, relativeZ) = drag.Settle(waterX - x, waterZ - z, yaw, seconds);
            (x, z) = (waterX - relativeX, waterZ - relativeZ);
        }
    }

    /// <summary>A hull whose drag isn't known comes toward the water's velocity by this share of the difference.</summary>
    public void Relax(double waterX, double waterZ, double share)
    {
        lock (gate)
        {
            x += (waterX - x) * share;
            z += (waterZ - z) * share;
        }
    }

    public (double X, double Z) Pull(HullDrag drag, double waterX, double waterZ, double yaw)
    {
        lock (gate) return drag.Pull(waterX - x, waterZ - z, yaw);
    }

    public double Swing(HullDrag drag, double waterX, double waterZ, double yaw, double turnPerSecond, double seconds)
    {
        lock (gate) return drag.Swing(waterX - x, waterZ - z, yaw, turnPerSecond, seconds);
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
            heldX = (x + pivotX) / WaterDrag.TicksPerSecond;
            heldZ = (z + pivotZ) / WaterDrag.TicksPerSecond;
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
            if (motion.X == 0) (x, pivotX, heldX) = (0, 0, 0);
            if (motion.Z == 0) (z, pivotZ, heldZ) = (0, 0, 0);
        }
    }

    /// <summary>Vanilla has just written her motion over from her forward speed; a side her physics runs on gives the set back.</summary>
    public void Rowed(Vec3d motion, bool moves)
    {
        lock (gate)
        {
            (heldX, heldZ) = moves ? ((x + pivotX) / WaterDrag.TicksPerSecond, (z + pivotZ) / WaterDrag.TicksPerSecond) : (0, 0);
            motion.X += heldX;
            motion.Z += heldZ;
        }
    }

    /// <summary>A mooring stops her set away from it, along the unit vector from the mooring to her.</summary>
    public void Hold(double outwardX, double outwardZ)
    {
        lock (gate)
        {
            var outward = x * outwardX + z * outwardZ;
            if (outward <= 0) return;

            x -= outward * outwardX;
            z -= outward * outwardZ;
            heldX -= outward * outwardX / WaterDrag.TicksPerSecond;
            heldZ -= outward * outwardZ / WaterDrag.TicksPerSecond;
        }
    }

    /// <summary>A pull back along the unit vector from the mooring to her takes up to this much of her set away from it, m/s.</summary>
    public double Slow(double outwardX, double outwardZ, double metresPerSecond)
    {
        lock (gate)
        {
            var cut = Math.Min(Math.Max(0, x * outwardX + z * outwardZ), metresPerSecond);
            x -= cut * outwardX;
            z -= cut * outwardZ;
            heldX -= cut * outwardX / WaterDrag.TicksPerSecond;
            heldZ -= cut * outwardZ / WaterDrag.TicksPerSecond;
            return cut;
        }
    }

    /// <summary>Her origin's velocity while she turns about a held bow, m/s; zero once she is let go.</summary>
    public void Pivot(double metresPerSecondX, double metresPerSecondZ)
    {
        lock (gate) (pivotX, pivotZ) = (metresPerSecondX, metresPerSecondZ);
    }

    /// <summary>This side takes her physics over with this set: her motion becomes the set, whatever the other side left in it.</summary>
    public void Take(double setX, double setZ, Vec3d motion)
    {
        lock (gate)
        {
            (x, z, pivotX, pivotZ) = (setX, setZ, 0, 0);
            motion.X = heldX = x / WaterDrag.TicksPerSecond;
            motion.Z = heldZ = z / WaterDrag.TicksPerSecond;
        }
    }

    /// <summary>She stops being carried here and now: what her motion holds of the set goes with it.</summary>
    public void Drop(Vec3d motion)
    {
        lock (gate)
        {
            motion.X -= heldX;
            motion.Z -= heldZ;
            (x, z, pivotX, pivotZ, heldX, heldZ) = (0, 0, 0, 0, 0, 0);
        }
    }

    /// <summary>Ashore or handed over: no set. What her motion still holds of it is vanilla's to take off.</summary>
    public void Clear()
    {
        lock (gate) (x, z, pivotX, pivotZ, heldX, heldZ) = (0, 0, 0, 0, 0, 0);
    }
}
