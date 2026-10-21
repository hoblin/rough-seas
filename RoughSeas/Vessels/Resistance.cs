using System;

namespace RoughSeas.Vessels;

public enum Blow
{
    Broad,
    Point,
    Cut,
    Slam
}

/// <param name="ShellPoint">The shell's own holing work; infinite where it yields to a blow, as a skin or a bundle does, and then only what backs it can break.</param>
/// <param name="BackedShare">Of the side's area, the share a lath or sheathing backs; <paramref name="BottomBackedShare"/> the bottom's.</param>
/// <param name="SlamPascals">The breaker's pressure the soaked planking bends to breaking under; infinite where nothing does.</param>
public readonly record struct Resistance(
    double FrameJoules,
    double ShellPoint,
    double ShellCut,
    double BackingPoint = 0,
    double BackingCut = 0,
    double BackedShare = 0,
    double BottomBackedShare = 0,
    double SlamPascals = double.PositiveInfinity)
{
    public static Resistance Whole(double frameJoules) => new(frameJoules, frameJoules, frameJoules);

    /// <summary>The even pressure that bends a strip to its modulus of rupture.</summary>
    /// <param name="momentFactor">The strip's greatest moment over pressure times span squared: 1/12 held at both ends, 1/8 propped, 1/2 standing free.</param>
    public static double Bending(double modulusOfRupture, double metres, double spanMetres, double momentFactor) =>
        modulusOfRupture * metres * metres / (6 * momentFactor * spanMetres * spanMetres);

    public static double Holing(double toughness, double shellMetres, double holeSquareMetres) =>
        toughness * 2 * Math.Sqrt(Math.PI * holeSquareMetres) * shellMetres;

    public bool Backed(HullPlace place, double draw) => draw < (place == HullPlace.Bottom ? BottomBackedShare : BackedShare);

    public double Against(Blow blow, bool backed) => blow switch
    {
        Blow.Point when backed => Yielding(ShellPoint) + BackingPoint,
        Blow.Point => ShellPoint,
        Blow.Cut when backed => Yielding(ShellCut) + BackingCut,
        Blow.Cut => ShellCut,
        Blow.Slam => SlamPascals,
        _ => FrameJoules
    };

    private static double Yielding(double shell) => double.IsPositiveInfinity(shell) ? 0 : shell;

    public static double Loss(double severity, Blow blow, bool byPlace) => blow switch
    {
        Blow.Broad => severity,
        Blow.Slam => severity >= 1 ? Place(1, byPlace) : 0,
        _ => Place(Math.Min(1, severity), byPlace)
    };

    private static double Place(double loss, bool byPlace) => loss / (byPlace ? 1 : HullShape.Places.Length);

    public static bool BreaksFrame(Blow blow, double loss) => blow == Blow.Broad && loss >= 1;
}
