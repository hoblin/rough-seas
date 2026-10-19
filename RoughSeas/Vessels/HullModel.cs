using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

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

public interface IKnockable
{
    Knock Take(HullPlace place, double loss);
}

public interface IAfloat
{
    void Pass(double days, bool afloat, double sun);
}

public interface IMendable
{
    bool Needs(HullRepair repair, HullPlace place);

    string Mend(HullRepair repair, HullPlace place);
}

public interface IBuoyant
{
    Afloat Afloat { get; }

    double SettlingMetres { get; }

    Aboard Aboard { get; }

    double ReferenceKg { get; }
}
