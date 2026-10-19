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
        ["cracking"] = (tree, changed, attributes, config) => new CrackingHull(tree, changed, attributes, config),
        ["lashed"] = (tree, changed, _, _) => new LashedHull(tree, changed),
        ["reed"] = (tree, changed, attributes, _) => new ReedHull(tree, changed, attributes)
    };

    public static HullModel For(string code, Func<ITreeAttribute> tree, Action changed, JsonObject attributes, HullConfig config)
    {
        if (!Models.TryGetValue(code, out var model))
        {
            throw new ArgumentException($"Rough Seas knows no hull model {code}; it knows {string.Join(", ", Models.Keys)}");
        }
        var hull = model(tree, changed, attributes, config);
        hull.Code = code;
        return hull;
    }

    /// <summary>The <c>model</c> key it was picked by: cracking, lashed or reed.</summary>
    public string Code { get; private set; } = "";

    public abstract float Condition { get; }

    public virtual double SoakedKg => 0;

    public virtual int Stage(HullPlace place) => StageOf(1 - Condition);

    public virtual double LitresPerSecond(Afloat afloat, double depthMetres) => 0;

    public virtual double Inflow(double seconds, double hours, Afloat afloat, double depthMetres) =>
        LitresPerSecond(afloat, depthMetres) * seconds;

    public abstract void Describe(StringBuilder info);

    public abstract void Adopt(float integrity);

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

public interface IKnockable
{
    /// <param name="loss">The harm the roll against its capacity dealt: out of a whole place, or of the whole hull for one without places.</param>
    Knock Take(HullPlace place, double loss);
}

public interface IAfloat
{
    /// <param name="sun">The daylight it dries in ashore; 0 in shade, rain or water.</param>
    void Pass(double days, bool afloat, double sun);
}

/// <summary>A hull mended by hand, at the place looked at where the model has places.</summary>
public interface IMendable
{
    bool Needs(HullRepair repair, HullPlace place);

    string Mend(HullRepair repair, HullPlace place);
}

/// <summary>
/// How a vessel floats now, from its full mass by Archimedes; refreshed twice a second on both sides. Without
/// <see cref="HasDraft"/> she floats as vanilla draws her and never sinks.
/// </summary>
public interface IBuoyant
{
    bool HasDraft { get; }

    double DepthMetres { get; }

    Afloat Afloat { get; }

    double SettlingMetres { get; }

    Aboard Aboard { get; }

    double ReferenceKg { get; }
}
