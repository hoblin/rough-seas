using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Vessels;

/// <summary>
/// A hull's knocks, measured as vanilla measures a fall: one happens on the step a collision first blocks an axis of
/// the hull's motion, and its speed is how fast the hull really moved along that axis just before, from its change of
/// position. A steered hull's motion is what her rider asks of her, rewritten every frame, so a hull held against a
/// rock strikes it once, not each time she is asked to move on. Speeds are in m/s.
/// </summary>
public class Impact(double give, long guardMs)
{
    private const long WindowMs = 100;
    private const double MillisecondsPerSecond = 1000;

    private readonly Queue<(long Ms, double X, double Z)> track = new();
    private bool blockedX, blockedZ;
    private long? struckAt;

    public Knocked? Step(double motionX, double motionZ, double x, double z, long milliseconds)
    {
        bool hitX = motionX == 0 && !blockedX, hitZ = motionZ == 0 && !blockedZ;
        (blockedX, blockedZ) = (motionX == 0, motionZ == 0);
        var (speedX, speedZ) = Speed();
        track.Enqueue((milliseconds, x, z));
        while (track.Count > 1 && milliseconds - track.Peek().Ms > WindowMs) track.Dequeue();

        double lostX = hitX ? speedX : 0, lostZ = hitZ ? speedZ : 0;
        double speedLost = Math.Sqrt(lostX * lostX + lostZ * lostZ);
        if (speedLost <= give || milliseconds - struckAt < guardMs) return null;

        struckAt = milliseconds;
        return new Knocked(speedLost, lostX, lostZ);
    }

    public void Forget()
    {
        track.Clear();
        (blockedX, blockedZ) = (false, false);
    }

    public double Joules(double kilograms, double speedLost) => 0.5 * kilograms * (speedLost * speedLost - give * give);

    private (double X, double Z) Speed()
    {
        if (track.Count < 2) return (0, 0);
        var (since, fromX, fromZ) = track.Peek();
        var (until, toX, toZ) = track.Last();
        double seconds = (until - since) / MillisecondsPerSecond;
        return seconds > 0 ? ((toX - fromX) / seconds, (toZ - fromZ) / seconds) : (0, 0);
    }
}

/// <param name="Against">The vessel rammed; 0 for the ground.</param>
/// <param name="TheirKg">What the vessel rammed weighed with all aboard her.</param>
/// <param name="Outboard">Whether it met only what stands off this hull, not the hull itself.</param>
/// <param name="TheirOutboard">Whether it met only what stands off the vessel rammed.</param>
public readonly record struct Knocked(double SpeedLost, double TowardX, double TowardZ, long Against = 0, double TheirKg = 0,
    bool Outboard = false, bool TheirOutboard = false)
{
    public Knocked Mirrored(long against, double theirKg) =>
        this with { TowardX = -TowardX, TowardZ = -TowardZ, Against = against, TheirKg = theirKg, Outboard = TheirOutboard, TheirOutboard = Outboard };
}
