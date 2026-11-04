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
    public FrameMember? Frame;

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
            BackedShare, BottomBackedShare ?? BackedShare, Slammed(materials) * hull.SoakedStrengthShare, Cushions(materials),
            Frame == null || frameJoules <= 0 ? null : Framed(Frame, materials, hole, hull.SoakedStrengthShare));
    }

    private FramedResistance Framed(FrameMember frame, MaterialTable materials, double hole, double soaked)
    {
        double opens = Opens(materials, hole), breaks = frame.Breaks(materials);
        return new(opens + breaks, opens + Gives(materials, 2 * frame.BreakingNewtons(materials, soaked)) + 2 * breaks, frame.Share, opens);
    }

    /// <summary>This shell with <paramref name="metres"/> of its own wood not yet cut out of the hold behind it: that wood backs the sides below the cut and the whole bottom.</summary>
    public Shell Uncut(double metres, double hollowed)
    {
        if (metres <= 0) return this;

        var solid = (Shell)MemberwiseClone();
        solid.Backing = new RigidShell { Material = Material, Metres = metres };
        solid.BackedShare = 1 - hollowed;
        solid.BottomBackedShare = 1;
        return solid;
    }

    /// <summary>This shell lined by <paramref name="material"/> over <paramref name="lined"/> of what its finished lining covers.</summary>
    public Shell Lined(string material, double lined)
    {
        if (Backing == null) return this;

        var shell = (Shell)MemberwiseClone();
        shell.Backing = lined > 0 ? new RigidShell { Material = material, Metres = Backing.Metres, SpanMetres = Backing.SpanMetres } : null;
        shell.BackedShare = BackedShare * lined;
        shell.BottomBackedShare = BottomBackedShare * lined;
        return shell;
    }

    /// <summary>This shell with <paramref name="laid"/> of its frame's members laid, of <paramref name="material"/>.</summary>
    public Shell Framed(string material, double laid)
    {
        if (Frame == null) return this;

        var shell = (Shell)MemberwiseClone();
        shell.Frame = laid > 0 ? Frame.Of(material, laid) : null;
        return shell;
    }

    public abstract double Holed(MaterialTable materials, double holeSquareMetres);

    public abstract double Slammed(MaterialTable materials);

    public double Bends(MaterialTable materials, double spanMetres, double momentFactor) =>
        Resistance.Bending(materials.ModulusOfRupture(Material), Metres, spanMetres, momentFactor);

    protected virtual double Cushions(MaterialTable materials) => 0;

    protected virtual double Opens(MaterialTable materials, double holeSquareMetres) =>
        Holed(materials, holeSquareMetres) + BackingHoled(materials, holeSquareMetres);

    protected virtual double Gives(MaterialTable materials, double newtons) => 0;

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
        Bends(materials, SpanMetres, FixedEnds) + (Backing?.Bends(materials, SpanMetres, FixedEnds) ?? 0) * BackedShare;
}

/// <summary>A hull hollowed from one log: each side stands from the bottom with its gunwale free.</summary>
public class CarvedShell : RigidShell
{
    private const double Cantilever = 1.0 / 2;

    public override double Slammed(MaterialTable materials) => Bends(materials, SpanMetres, Cantilever);
}

/// <summary>
/// A hide stretched over laths: it springs back from a blow, which harms only a lath that backs it, opens under no
/// knock but sags between two laths until its pull breaks them, and a breaker presses it onto the laths, each
/// bending under its share of the side.
/// </summary>
public class SkinShell : Shell
{
    private const double Propped = 1.0 / 8;

    public override double Holed(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;

    protected override double Opens(MaterialTable materials, double holeSquareMetres) => 0;

    protected override double Gives(MaterialTable materials, double newtons)
    {
        var (modulus, strain) = materials.Stretch(Material);
        if (modulus <= 0 || Frame is null) return 0;

        double gap = Frame.GapMetres, stiffness = 8 * modulus * Metres * gap / Math.Pow(gap, 3);
        double sag = Math.Min(Math.Cbrt(newtons / stiffness), gap * Math.Sqrt(strain / 2));
        return stiffness * Math.Pow(sag, 4) / 4;
    }

    public override double Slammed(MaterialTable materials) =>
        Backing is null ? double.PositiveInfinity : Backing.Bends(materials, SpanMetres, Propped) * BackedShare;
}

/// <summary>The lath or rib behind a shell: a broad blow where it lands breaks it by its impact bending work across its section.</summary>
public class FrameMember
{
    public string Material = "wood";
    public double Metres;
    public double WidthMetres;
    public double SpacingMetres;
    /// <summary>Its length between the members it crosses, where a skin's pull bends it.</summary>
    public double SpanMetres;

    public double Share => WidthMetres / SpacingMetres;

    public double GapMetres => SpacingMetres - WidthMetres;

    public FrameMember Of(string material, double laid)
    {
        var member = (FrameMember)MemberwiseClone();
        member.Material = material;
        member.SpacingMetres = SpacingMetres / laid;
        return member;
    }

    public double Breaks(MaterialTable materials) => materials.Toughness(Material) * Metres * WidthMetres;

    public double BreakingNewtons(MaterialTable materials, double soaked) =>
        2 * materials.ModulusOfRupture(Material) * soaked * WidthMetres * Metres * Metres / (3 * SpanMetres);
}

/// <summary>What breaks a framed hull where a broad blow lands: on a member, or between two, which it lands on by <paramref name="MemberShare"/>.</summary>
public readonly record struct FramedResistance(double OverMemberJoules, double BetweenMembersJoules, double MemberShare, double OpensJoules);

/// <summary>
/// Bundled stems, as a broom is: each stem bends alone and no crack runs from one to the next, so a blow springs off
/// it, a breaker bends them and they spring back, and an edge parts only the stems it meets. It comes apart where an
/// edge cuts a lashing that binds it. A broad blow crushes the stems it meets, across the bundle and along the metre of
/// it a block's face meets, and only what that crushing leaves loads the lashings.
/// </summary>
public class BundleShell : Shell
{
    private const double BlockMetres = 1;

    public override double Holed(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;

    public override double Cut(MaterialTable materials, double holeSquareMetres, double cutShare) => double.PositiveInfinity;

    protected override double BackingHoled(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;

    public override double Slammed(MaterialTable materials) => double.PositiveInfinity;

    protected override double Cushions(MaterialTable materials) =>
        materials.CrushJoulesPerCubicMetre(Material) * Math.PI * Metres * Metres / 4 * BlockMetres;
}
