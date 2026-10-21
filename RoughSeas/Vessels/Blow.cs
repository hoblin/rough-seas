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
}
