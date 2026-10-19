using System;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

public sealed class LashedHull(Func<ITreeAttribute> tree, Action changed) : HullModel(tree, changed), IKnockable, IMendable
{
    private const string LashingKey = "lashing";

    public override float Condition => Tree?.GetFloat(LashingKey, 1) ?? 1;

    public Knock Take(HullPlace place, double loss)
    {
        SetLashing(Condition - loss);
        return Condition > 0 ? Knock.Harmed : Knock.BrokenUp;
    }

    public bool Needs(HullRepair repair, HullPlace place) => Condition < 1;

    public string Mend(HullRepair repair, HullPlace place)
    {
        float before = Condition;
        SetLashing(before + repair.Restores);
        return $"lashings mended: {before:F2} -> {Condition:F2}";
    }

    public override void Describe(StringBuilder info) =>
        info.AppendLine(Lang.Get("roughseas:vessel-lashing", (int)Math.Round(Condition * 100)));

    public override void Adopt(float integrity) => SetLashing(integrity);

    private void SetLashing(double lashing)
    {
        Tree.SetFloat(LashingKey, (float)Math.Clamp(lashing, 0, 1));
        Changed();
    }
}
