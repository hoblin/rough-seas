using System;

namespace RoughSeas.Vessels;

public enum Blow
{
    Broad,
    Point,
    Cut
}

/// <param name="ShellPoint">The shell's own holing work; infinite for a skin, which springs back from a blow.</param>
/// <param name="BackedShare">Of the side's area, the share a lath or sheathing backs; <paramref name="BottomBackedShare"/> the bottom's.</param>
public readonly record struct Resistance(
    double FrameJoules,
    double ShellPoint,
    double ShellCut,
    double BackingPoint = 0,
    double BackingCut = 0,
    double BackedShare = 0,
    double BottomBackedShare = 0)
{
    public static Resistance Whole(double frameJoules) => new(frameJoules, frameJoules, frameJoules);

    public static double Holing(double toughness, double shellMetres, double holeSquareMetres) =>
        toughness * 2 * Math.Sqrt(Math.PI * holeSquareMetres) * shellMetres;

    public bool Backed(HullPlace place, double draw) => draw < (place == HullPlace.Bottom ? BottomBackedShare : BackedShare);

    public double Against(Blow blow, bool backed) => blow switch
    {
        Blow.Point when backed => (double.IsPositiveInfinity(ShellPoint) ? 0 : ShellPoint) + BackingPoint,
        Blow.Point => ShellPoint,
        Blow.Cut => ShellCut + (backed ? BackingCut : 0),
        _ => FrameJoules
    };

    public static double Loss(double severity, Blow blow, bool byPlace) =>
        blow == Blow.Broad ? severity : Math.Min(1, severity) / (byPlace ? 1 : HullShape.Places.Length);

    public static bool BreaksFrame(Blow blow, double loss) => blow == Blow.Broad && loss >= 1;
}
