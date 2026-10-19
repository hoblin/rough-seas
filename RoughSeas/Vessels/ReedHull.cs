using System;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

public sealed class ReedHull : HullModel, IAfloat
{
    private const string WaterKey = "waterKg";
    private const string RotKey = "rotKg";

    private readonly SoakConfig soak;

    public ReedHull(Func<ITreeAttribute> tree, Action changed, JsonObject attributes) : base(tree, changed)
    {
        soak = attributes["soak"].AsObject<SoakConfig>(new SoakConfig());
    }

    public override float Condition => soak.SaturationKg > 0 ? (float)Math.Max(0, 1 - SoakedKg / soak.SaturationKg) : 1;

    public override double SoakedKg => Tree?.GetDouble(WaterKey) ?? 0;

    public double RotKg => Tree?.GetDouble(RotKey) ?? 0;

    public void Pass(double days, bool afloat, double sun)
    {
        double water = SoakedKg;
        if (afloat)
        {
            Tree.SetDouble(WaterKey, Math.Min(soak.SaturationKg, water + soak.KgPerDay * days));
        }
        else if (sun > 0 && water > RotKg)
        {
            double dried = Math.Min(water - RotKg, soak.DryKgPerDay * sun * days);
            Tree.SetDouble(WaterKey, water - dried * (1 - soak.RotShare));
            Tree.SetDouble(RotKey, RotKg + dried * soak.RotShare);
        }
        else return;

        Changed();
    }

    public override void Describe(StringBuilder info) =>
        info.AppendLine(Lang.Get("roughseas:vessel-soaked", (int)Math.Round((1 - Condition) * 100)));

    public override void Adopt(float legacyIntegrity)
    {
        Tree.SetDouble(WaterKey, (1 - legacyIntegrity) * soak.SaturationKg);
        Changed();
    }
}

public class SoakConfig
{
    public double KgPerDay;
    public double DryKgPerDay;
    public double RotShare;
    public double SaturationKg;
}
