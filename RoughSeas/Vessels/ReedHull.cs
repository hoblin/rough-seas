using System;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

public sealed class ReedHull : HullModel, IAfloat, IKnockable
{
    private const string WaterKey = "waterKg";
    private const string RotKey = "rotKg";
    private const string TornKey = "torn";

    private readonly SoakConfig soak;

    public ReedHull(Func<ITreeAttribute> tree, Action changed, JsonObject attributes) : base(tree, changed)
    {
        soak = attributes["soak"].AsObject<SoakConfig>(new SoakConfig());
    }

    public override float Condition => (float)Math.Min(Dryness, 1 - Torn);

    public override double SoakedKg => Tree?.GetDouble(WaterKey) ?? 0;

    public double RotKg => Tree?.GetDouble(RotKey) ?? 0;

    public double Torn => Tree?.GetDouble(TornKey) ?? 0;

    public bool ByPlace => false;

    private double Dryness => soak.SaturationKg > 0 ? Math.Max(0, 1 - SoakedKg / soak.SaturationKg) : 1;

    public Knock Take(HullPlace place, double loss)
    {
        Tree.SetDouble(TornKey, Math.Min(1, Snapped(Torn + loss)));
        Changed();
        return Torn >= 1 ? Knock.BrokenUp : Knock.Harmed;
    }

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

    public override void Describe(StringBuilder info)
    {
        info.AppendLine(Lang.Get("roughseas:vessel-soaked", (int)Math.Round((1 - Dryness) * 100)));
        if (Torn > 0) info.AppendLine(Lang.Get("roughseas:vessel-torn", (int)Math.Round(Torn * 100)));
    }

    public override void Adopt(float integrity)
    {
        Tree.SetDouble(WaterKey, (1 - integrity) * soak.SaturationKg);
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
