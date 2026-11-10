using System;
using System.Collections.Generic;

namespace RoughSeas.Water;

/// <summary>
/// The front of the sheet a broken wave throws up the shore, thrown at U = √(2gR) so that it climbs the run-up R, as
/// Shen &amp; Meyer's swash meets the measured run-up. Crossing a block, it climbs the step to the block's top only while its
/// velocity head reaches over it, and loses speed to bed friction over the lens behind it, whose mean depth is
/// (x_s / t)² / 27g by Shen &amp; Meyer: the slower the front has come, the thinner the lens it drags, so friction takes
/// more of it the further it spreads. The bed drinks the lens by Darcy. A block is wetted once the front climbs onto it; the
/// front stops there when it can't climb, or inside it when the bed has drunk the lens or its wave's period is out
/// and the next bore runs over it.
/// </summary>
public readonly record struct SheetFront(double SpeedSquared, double Metres, double Seconds, double Soaked, double Period, double Friction, double LensRatio)
{
    private const double Gravity = WaveTheory.Gravity;
    private const double LargestSpeedShare = 4;
    private const int ThrowSteps = 12;

    public static SheetFront Thrown(double runUpMetres, double periodSeconds, SheetConfig config) =>
        new(2 * Gravity * Math.Max(runUpMetres, 0), 0, 0, 0, periodSeconds, config.UprushFriction, config.LensDepthRatio);

    /// <summary>
    /// A front thrown up this <paramref name="profile"/> so that, losing speed to friction and soak on the way, it climbs to <paramref name="runUpMetres"/>: the measured run-up
    /// is where a real sheet stops with its friction spent. The climb is to the foot of the highest tread it reaches, so a flat tread asks for no more speed, and a shore that ends lower than the run-up is climbed to its top.
    /// </summary>
    public static SheetFront Thrown(double runUpMetres, double periodSeconds, SheetConfig config, IReadOnlyList<double> profile, BedConfig bed)
    {
        var free = Thrown(runUpMetres, periodSeconds, config);
        var highest = -1;
        for (var i = 0; i < profile.Count && profile[i] <= runUpMetres; i++) highest = i;
        if (highest < 0) return free;
        while (highest > 0 && profile[highest - 1] >= profile[highest]) highest--;
        if (Climbs(free, profile, bed, highest)) return free;

        double low = 1, high = LargestSpeedShare;
        if (!Climbs(Faster(free, high), profile, bed, highest)) return Faster(free, high);
        for (var step = 0; step < ThrowSteps; step++)
        {
            var middle = (low + high) / 2;
            if (Climbs(Faster(free, middle), profile, bed, highest)) high = middle;
            else low = middle;
        }
        return Faster(free, high);
    }

    private static SheetFront Faster(SheetFront front, double share) => front with { SpeedSquared = front.SpeedSquared * share * share };

    private static bool Climbs(SheetFront front, IReadOnlyList<double> profile, BedConfig bed, int highest)
    {
        var reached = front;
        for (var i = 0; i <= highest; i++)
        {
            if (reached.Cross(profile[i] - (i > 0 ? profile[i - 1] : 0), bed) is not { } next) return false;
            reached = next;
        }
        return true;
    }

    /// <summary>When a front thrown to <paramref name="runUp"/> up a slope of this <paramref name="rise"/> reaches <paramref name="distance"/>, as <c>seasurface.vsh</c> draws it.</summary>
    public static double Arrival(double runUp, double rise, double distance)
    {
        var speed = Math.Sqrt(2 * Gravity * runUp);
        var sine = distance > 0 ? rise / Math.Sqrt(distance * distance + rise * rise) : 1;
        return sine > 0.0001 ? (speed - Math.Sqrt(Math.Max(0, speed * speed - 2 * Gravity * sine * distance))) / (Gravity * sine) : distance / speed;
    }

    public double Head => SpeedSquared / (2 * Gravity);

    public double Depth => (Seconds > 0 ? Metres * Metres / (Seconds * Seconds) : SpeedSquared) / (LensRatio * Gravity) - Soaked;

    public SheetFront? Cross(double rise, BedConfig bed)
    {
        if (SpeedSquared <= 0 || rise >= Head || Depth <= 0) return null;
        var speedSquared = SpeedSquared - 2 * Gravity * rise;
        var loss = Friction / Depth;
        var crossing = (Math.Exp(loss) - 1) / (loss * Math.Sqrt(speedSquared));
        var next = this with
        {
            SpeedSquared = speedSquared * Math.Exp(-2 * loss),
            Metres = Metres + 1,
            Seconds = Seconds + crossing,
            Soaked = Soaked + bed.Conductivity * crossing,
        };
        return next.Depth <= 0 || next.Seconds > Period ? this with { SpeedSquared = 0 } : next;
    }
}
