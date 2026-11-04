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
/// <param name="CushionJoules">What the shell takes of a broad blow by crushing before anything behind it bears the rest, as a bundle of stems does.</param>
/// <param name="PlaceJoules">What breaks a framed hull where a broad blow lands: its shell opening there and the frame member behind it; 0 where the whole frame takes the blow.</param>
public readonly record struct Resistance(
    double FrameJoules,
    double ShellPoint,
    double ShellCut,
    double BackingPoint = 0,
    double BackingCut = 0,
    double BackedShare = 0,
    double BottomBackedShare = 0,
    double SlamPascals = double.PositiveInfinity,
    double CushionJoules = 0,
    double PlaceJoules = 0)
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
        _ when PlaceJoules > 0 => PlaceJoules,
        _ => FrameJoules
    };

    /// <summary>The blow's load over what resists it, after the shell's crushing took its share of a broad one.</summary>
    /// <param name="strength">How strong this piece of material happens to be, as a share of its measured strength.</param>
    public double Severity(double load, Blow blow, bool backed, double strength)
    {
        double resists = Against(blow, backed) * strength;
        double left = blow == Blow.Broad ? Math.Max(0, load - CushionJoules * strength) : load;
        return resists > 0 ? left / resists : 0;
    }

    private static double Yielding(double shell) => double.IsPositiveInfinity(shell) ? 0 : shell;

    public double Loss(double severity, Blow blow, bool byPlace) => blow switch
    {
        Blow.Broad when PlaceJoules > 0 => Place(Math.Min(1, severity * HullModel.FrameBroken), byPlace),
        Blow.Broad => severity,
        Blow.Slam => severity >= 1 ? Place(1, byPlace) : 0,
        _ => Place(Math.Min(1, severity), byPlace)
    };

    private static double Place(double loss, bool byPlace) => loss / (byPlace ? 1 : HullShape.Places.Length);

    public bool BreaksFrame(Blow blow, double severity) => blow == Blow.Broad &&
        (PlaceJoules > 0 ? (severity - 1 / HullModel.FrameBroken) * PlaceJoules >= FrameJoules : severity >= 1);
}
