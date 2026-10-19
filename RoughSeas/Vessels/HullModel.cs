using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

/// <summary>
/// What a vessel's hull is made like, picked by the <c>model</c> key of its <c>roughseas:vesselintegrity</c>: how it
/// takes a knock, time afloat and mending, through the interfaces it implements. Its state lives in the vessel's
/// watched <c>roughseas:vessel</c> tree.
/// </summary>
public abstract class HullModel(Func<ITreeAttribute> tree, Action changed)
{
    private static readonly Dictionary<string, Func<Func<ITreeAttribute>, Action, JsonObject, HullConfig, HullModel>> Models = new()
    {
        ["planked"] = (tree, changed, attributes, config) => new PlankedHull(tree, changed, attributes, config),
        ["lashed"] = (tree, changed, _, _) => new LashedHull(tree, changed),
        ["reed"] = (tree, changed, attributes, _) => new ReedHull(tree, changed, attributes)
    };

    public static HullModel For(string code, Func<ITreeAttribute> tree, Action changed, JsonObject attributes, HullConfig config) =>
        Models.TryGetValue(code, out var model)
            ? model(tree, changed, attributes, config)
            : throw new ArgumentException($"Rough Seas knows no hull model {code}; it knows {string.Join(", ", Models.Keys)}");

    public abstract float Condition { get; }

    public virtual double SoakedKg => 0;

    public virtual int Stage(HullPlace place) => StageOf(1 - Condition);

    public virtual double Inflow(double seconds, double hours, Afloat afloat, double depthMetres) => 0;

    public abstract void Describe(StringBuilder info);

    public abstract void Adopt(float legacyIntegrity);

    protected ITreeAttribute Tree => tree();

    protected void Changed() => changed();

    protected static int StageOf(double damage) => damage switch
    {
        < 0.1 => 0,
        < 0.25 => 1,
        < 0.5 => 2,
        < 0.75 => 3,
        _ => 4
    };
}

public enum Knock
{
    Harmed,
    BrokenUp
}

/// <summary>A hull a knock harms: <paramref name="loss"/> is the harm the roll against its capacity dealt, out of a whole hull.</summary>
public interface IKnockable
{
    Knock Take(HullPlace place, double loss);
}

/// <summary>A hull that changes with time: <paramref name="sun"/> is the daylight it dries in ashore, 0 in shade, rain or water.</summary>
public interface IAfloat
{
    void Pass(double days, bool afloat, double sun);
}

/// <summary>A hull mended by hand; the patch goes on the place looked at.</summary>
public interface IMendable
{
    bool Needs(HullRepair repair, HullPlace place);

    string Mend(HullRepair repair, HullPlace place);
}

/// <summary>How a vessel floats now, from its full mass by Archimedes; refreshed twice a second on both sides.</summary>
public interface IBuoyant
{
    Afloat Afloat { get; }

    double SettlingMetres { get; }

    Aboard Aboard { get; }

    double ReferenceKg { get; }
}
