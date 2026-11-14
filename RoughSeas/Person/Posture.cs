using System;

namespace RoughSeas.Person;

/// <summary>
/// How a body lies in the water: upright, or along a unit axis while it swims. Its drag follows the cross-flow
/// principle (Hoerner 1965, Fluid-Dynamic Drag, ch. 3): the velocity through the water along the axis meets the
/// length's drag area, whole as soon as the body is in, and the part across it the side's, by the share of the body
/// under water; each against the water the body carries that way.
/// </summary>
public readonly record struct Posture(double X, double Y, double Z, bool Prone)
{
    public static readonly Posture Upright = new(0, 1, 0, false);

    public static Posture Along(double x, double y, double z)
    {
        var length = Math.Sqrt(x * x + y * y + z * z);
        return length > 0 ? new Posture(x / length, y / length, z / length, true) : Upright;
    }

    /// <summary>Lying along <paramref name="yaw"/> as vanilla turns a player's body, nose down by <paramref name="pitch"/>.</summary>
    public static Posture Lying(double yaw, double pitch) =>
        Along(Math.Sin(yaw) * Math.Cos(pitch), -Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));

    /// <summary>The velocity <paramref name="through"/> the water, m/s, after <paramref name="seconds"/> of drag.</summary>
    public (double X, double Y, double Z) Dragged((double X, double Y, double Z) through, BodyConfig body, double density, double shareUnder,
        double alongKg, double acrossKg, double seconds)
    {
        var along = through.X * X + through.Y * Y + through.Z * Z;
        var (acrossX, acrossY, acrossZ) = (through.X - along * X, through.Y - along * Y, through.Z - along * Z);
        var across = Math.Sqrt(acrossX * acrossX + acrossY * acrossY + acrossZ * acrossZ);
        var alongAfter = Body.Dragged(along, density, Prone ? body.DragAreaSwimming : body.DragAreaAlong, alongKg, seconds);
        var acrossKept = across > 0 ? Body.Dragged(across, density, body.DragAreaAcross * shareUnder, acrossKg, seconds) / across : 1;
        return (alongAfter * X + acrossX * acrossKept, alongAfter * Y + acrossY * acrossKept, alongAfter * Z + acrossZ * acrossKept);
    }
}
