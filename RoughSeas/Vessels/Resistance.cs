using System;

namespace RoughSeas.Vessels;

public enum Blow
{
    Broad,
    Point,
    Cut
}

public readonly record struct Resistance(double FrameJoules, double PlaceJoules, double CutShare)
{
    public static double Holing(double toughness, double shellMetres, double holeSquareMetres) =>
        toughness * 2 * Math.Sqrt(Math.PI * holeSquareMetres) * shellMetres;

    public double Against(Blow blow) => blow switch
    {
        Blow.Point => PlaceJoules,
        Blow.Cut => PlaceJoules * CutShare,
        _ => FrameJoules
    };

    public static double Loss(double severity, Blow blow, bool byPlace) =>
        blow == Blow.Broad ? severity : Math.Min(1, severity) / (byPlace ? 1 : HullShape.Places.Length);

    public static bool BreaksFrame(Blow blow, double loss) => blow == Blow.Broad && loss >= 1;
}
