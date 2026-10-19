using System;

namespace RoughSeas.Vessels;

/// <summary>
/// A hull's knocks, read from vanilla's own collision: physics zeroes each axis of the motion a collision blocked,
/// so the motion kept on that axis a step earlier is the speed lost into the obstacle. Motion is in blocks per
/// 1/60 s, as <c>Pos.Motion</c> holds it; speeds are in m/s.
/// </summary>
public class Impact(double give, long guardMs)
{
    private const double TicksPerSecond = 60;

    private double keptX, keptZ;
    private long? struckAt;

    public Knocked? Step(double motionX, double motionZ, long milliseconds)
    {
        double lostX = motionX == 0 ? keptX : 0;
        double lostZ = motionZ == 0 ? keptZ : 0;
        (keptX, keptZ) = (motionX, motionZ);

        double speedLost = Math.Sqrt(lostX * lostX + lostZ * lostZ) * TicksPerSecond;
        if (speedLost <= give || milliseconds - struckAt < guardMs) return null;

        struckAt = milliseconds;
        return new Knocked(speedLost, lostX * TicksPerSecond, lostZ * TicksPerSecond);
    }

    public void Forget() => (keptX, keptZ) = (0, 0);

    public double Joules(double kilograms, double speedLost) => 0.5 * kilograms * (speedLost * speedLost - give * give);
}

public readonly record struct Knocked(double SpeedLost, double TowardX, double TowardZ);
