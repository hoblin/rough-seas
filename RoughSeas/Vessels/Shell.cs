using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

/// <summary>
/// What a hull's outside is made of, picked by the <c>kind</c> key of its <c>shell</c>: how a blow and an edge break
/// it where they land, what backs it there, and what a breaker's pressure bends.
/// </summary>
public abstract class Shell
{
    private static readonly Dictionary<string, Type> Kinds = new()
    {
        ["rigid"] = typeof(RigidShell),
        ["carved"] = typeof(CarvedShell),
        ["skin"] = typeof(SkinShell),
        ["bundle"] = typeof(BundleShell)
    };

    public string Material = "wood";
    public double Metres;
    /// <summary>How far what a breaker bends reaches between what holds it: its frames apart, or its own length.</summary>
    public double SpanMetres;
    public RigidShell? Backing;
    public double BackedShare;
    public double? BottomBackedShare;

    public static Shell? Of(JsonObject shell)
    {
        if (!shell.Exists) return null;

        var kind = shell["kind"].AsString("rigid");
        if (!Kinds.TryGetValue(kind, out var type))
        {
            throw new ArgumentException($"Rough Seas knows no shell kind {kind}; it knows {string.Join(", ", Kinds.Keys)}");
        }
        return (Shell)shell.Token.ToObject(type)!;
    }

    public Resistance Resists(double frameJoules, MaterialTable materials, HullConfig hull, double cutShare)
    {
        var hole = hull.CrackSquareMetres;
        return new(frameJoules, Holed(materials, hole), Cut(materials, hole, cutShare),
            BackingHoled(materials, hole), Backing?.Cut(materials, hole, cutShare) ?? 0,
            BackedShare, BottomBackedShare ?? BackedShare, Slammed(materials) * hull.SoakedStrengthShare);
    }

    public abstract double Holed(MaterialTable materials, double holeSquareMetres);

    public abstract double Slammed(MaterialTable materials);

    public double Bends(MaterialTable materials, double spanMetres, double momentFactor) =>
        Resistance.Bending(materials.ModulusOfRupture(Material), Metres, spanMetres, momentFactor);

    protected virtual double BackingHoled(MaterialTable materials, double holeSquareMetres) => Backing?.Holed(materials, holeSquareMetres) ?? 0;

    public virtual double Cut(MaterialTable materials, double holeSquareMetres, double cutShare) =>
        Resistance.Holing(materials.CutToughness(Material) ?? materials.Toughness(Material) * cutShare, Metres, holeSquareMetres);
}

/// <summary>
/// Planks, logs, bark: a blow breaks them where it lands, and a breaker bends them, and what lines them, between
/// the frames they are fastened to.
/// </summary>
public class RigidShell : Shell
{
    private const double FixedEnds = 1.0 / 12;

    public override double Holed(MaterialTable materials, double holeSquareMetres) =>
        Resistance.Holing(materials.Toughness(Material), Metres, holeSquareMetres);

    public override double Slammed(MaterialTable materials) =>
        Bends(materials, SpanMetres, FixedEnds) + (Backing?.Bends(materials, SpanMetres, FixedEnds) ?? 0);
}

/// <summary>A hull hollowed from one log: each side stands from the bottom with its gunwale free.</summary>
public class CarvedShell : RigidShell
{
    private const double Cantilever = 1.0 / 2;

    public override double Slammed(MaterialTable materials) => Bends(materials, SpanMetres, Cantilever);
}

/// <summary>
/// A hide stretched over laths: it springs back from a blow, which harms only a lath that backs it, and a breaker
/// presses it onto the laths, each bending under its share of the side.
/// </summary>
public class SkinShell : Shell
{
    private const double Propped = 1.0 / 8;

    public override double Holed(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;

    public override double Slammed(MaterialTable materials) =>
        Backing is null ? double.PositiveInfinity : Backing.Bends(materials, SpanMetres, Propped) * BackedShare;
}

/// <summary>
/// Bundled stems, as a broom is: each stem bends alone and no crack runs from one to the next, so a blow springs off
/// it, a breaker bends them and they spring back, and an edge parts only the stems it meets. It comes apart where an
/// edge cuts a lashing that binds it.
/// </summary>
public class BundleShell : Shell
{
    public override double Holed(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;

    public override double Cut(MaterialTable materials, double holeSquareMetres, double cutShare) => double.PositiveInfinity;

    protected override double BackingHoled(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;

    public override double Slammed(MaterialTable materials) => double.PositiveInfinity;
}
