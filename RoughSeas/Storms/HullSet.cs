using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Storms;

/// <summary>
/// A hull's set: the velocity the water and her lines have given her, on top of her own way through the water, a
/// rower's or a sail's. It is the waves' water at her place and her own velocity beside it, which forces change. It
/// lives in her motion and goes through vanilla's physics there: each move sets her motion so that after vanilla's water
/// drag and swim drag she carries the set whole and the rest of her motion as vanilla would have left it alone. She
/// displaces her own mass, so she takes the water's change and push whole; her drag is her hull's.
/// </summary>
public sealed class HullSet : BodyInWater
{
    private const double DisplacesHerMass = 1;

    private double pivotX;
    private double pivotZ;
    private double heldX;
    private double heldZ;
    private double madeX;
    private double madeZ;
    private double madeSeconds;
    private (double X, double Z) way;

    /// <summary>What her motion holds of the set now, blocks per tick.</summary>
    public (double X, double Z) Held
    {
        get
        {
            lock (gate) return (heldX, heldZ);
        }
    }

    /// <summary>
    /// Her own way through the water, m/s: what vanilla's moves since the last call made of her motion besides the set,
    /// after their drags, on whichever side and at whatever rate they ran; as before when none ran.
    /// </summary>
    public (double X, double Z) Way()
    {
        lock (gate)
        {
            if (madeSeconds > 0) (way, madeX, madeZ, madeSeconds) = ((madeX / madeSeconds, madeZ / madeSeconds), 0, 0, 0);
            return way;
        }
    }

    /// <summary>The water moves her for <paramref name="seconds"/>.</summary>
    /// <returns>The water's acceleration of her besides its drag, m/s²: the waves' own change at her place and their push.</returns>
    public (double X, double Z) Settle(HullDrag drag, WaterOnHull water, double yaw, double seconds)
    {
        lock (gate)
        {
            var wash = Meet(water, Through(water, yaw), DisplacesHerMass, seconds);
            var (relativeX, relativeZ) = drag.Settle(water.CurrentX - ownX, water.CurrentZ - ownZ, yaw, seconds, water.WayAlong, Pushed.X, Pushed.Z);
            ownX = water.CurrentX - relativeX;
            ownZ = water.CurrentZ - relativeZ;
            return wash;
        }
    }

    /// <summary>As <see cref="Settle"/> for a hull whose drag isn't known: she comes toward the current by this share of the difference.</summary>
    public (double X, double Z) Relax(WaterOnHull water, double yaw, double share, double seconds)
    {
        lock (gate)
        {
            var wash = Meet(water, Through(water, yaw), DisplacesHerMass, seconds);
            ownX += Pushed.X * seconds + (water.CurrentX - ownX) * share;
            ownZ += Pushed.Z * seconds + (water.CurrentZ - ownZ) * share;
            return wash;
        }
    }

    /// <summary>The side that moves her says what her own set is, m/s.</summary>
    public void Reported(double ownX, double ownZ)
    {
        lock (gate) (this.ownX, this.ownZ) = (ownX, ownZ);
    }

    public void Add(double metresPerSecondX, double metresPerSecondZ)
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

    private (double X, double Z) Through(WaterOnHull water, double yaw)
    {
        var (wayX, wayZ) = HullShape.ToWorld(water.WayAlong, 0, yaw);
        return (ownX - water.CurrentX + wayX, ownZ - water.CurrentZ + wayZ);
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
            madeX += restX / rest * stepFactor;
            madeZ += restZ / rest * stepFactor;
            madeSeconds += stepFactor / WaterDrag.TicksPerSecond;
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
            if (motion.X == 0) (ownX, pivotX, heldX, madeX) = (-waveX, 0, 0, 0);
            if (motion.Z == 0) (ownZ, pivotZ, heldZ, madeZ) = (-waveZ, 0, 0, 0);
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
            (madeX, madeZ, madeSeconds, way) = (0, 0, 0, default);
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
            (madeX, madeZ, madeSeconds, way) = (0, 0, 0, default);
        }
    }

    /// <summary>Ashore or handed over: no set. What her motion still holds of it is vanilla's to take off.</summary>
    public void Clear()
    {
        lock (gate)
        {
            (ownX, ownZ, waveX, waveZ, pivotX, pivotZ, heldX, heldZ, met) = (0, 0, 0, 0, 0, 0, 0, 0, false);
            (madeX, madeZ, madeSeconds, way) = (0, 0, 0, default);
        }
    }
}
