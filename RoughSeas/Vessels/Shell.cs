using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

/// <summary>
/// What a hull's outside is made of, picked by the <c>kind</c> key of its <c>shell</c>: how a blow and an edge break
/// it where they land, and what backs it there.
/// </summary>
public abstract class Shell
{
    private static readonly Dictionary<string, Type> Kinds = new()
    {
        ["rigid"] = typeof(RigidShell),
        ["skin"] = typeof(SkinShell)
    };

    public string Material = "wood";
    public double Metres;
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

    public Resistance Resists(double frameJoules, MaterialTable materials, double holeSquareMetres, double cutShare) =>
        new(frameJoules, Holed(materials, holeSquareMetres), Cut(materials, holeSquareMetres, cutShare),
            Backing?.Holed(materials, holeSquareMetres) ?? 0, Backing?.Cut(materials, holeSquareMetres, cutShare) ?? 0,
            BackedShare, BottomBackedShare ?? BackedShare);

    public abstract double Holed(MaterialTable materials, double holeSquareMetres);

    public double Cut(MaterialTable materials, double holeSquareMetres, double cutShare) =>
        Resistance.Holing(materials.CutToughness(Material) ?? materials.Toughness(Material) * cutShare, Metres, holeSquareMetres);
}

/// <summary>Planks, logs, bark: a blow breaks them where it lands.</summary>
public class RigidShell : Shell
{
    public override double Holed(MaterialTable materials, double holeSquareMetres) =>
        Resistance.Holing(materials.Toughness(Material), Metres, holeSquareMetres);
}

/// <summary>A hide stretched over laths: it springs back from a blow, which harms only a lath that backs it.</summary>
public class SkinShell : Shell
{
    public override double Holed(MaterialTable materials, double holeSquareMetres) => double.PositiveInfinity;
}
