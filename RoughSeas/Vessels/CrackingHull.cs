using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

public sealed class CrackingHull : HullModel, IKnockable, IAfloat, IMendable
{
    private const string CracksKey = "cracks";
    private const string GreaseKey = "greaseDays";
    private const string WetKey = "wetDays";
    private const double Gravity = 9.81;
    private const double LitresPerCubicMetre = 1000;

    private readonly HullConfig config;
    private readonly SeepConfig? seep;
    private readonly HideCover? designedCover;
    private Func<HideCover?> covered = () => null;

    public CrackingHull(Func<ITreeAttribute> tree, Action changed, JsonObject attributes, HullConfig config) : base(tree, changed)
    {
        this.config = config;
        seep = attributes["seep"].AsObject<SeepConfig?>(null);
        designedCover = attributes["cover"].AsObject<CoverConfig?>(null) is { } cover ? new HideCover(cover.Hides, cover.Seams) : null;
    }

    public HideCover? Cover => covered() ?? designedCover;

    public double WetDays => Tree?.GetDouble(WetKey) ?? 0;

    public override double SoakedKg => WetDays > 0 ? Cover?.SoakedKg(config.Hides) ?? 0 : 0;

    public void CoveredBy(Func<HideCover?> cover) => covered = cover;

    public override float Condition => (float)(1 - HullShape.Places.Sum(Crack) / HullShape.Places.Length);

    public double GreaseDays => seep == null ? 0 : Tree.GetDouble(GreaseKey, seep.GreaseDays);

    public static void Ungrease(ITreeAttribute tree) => tree.SetDouble(GreaseKey, 0);

    public override int Stage(HullPlace place) => Crack(place) is var crack and > 0 ? Math.Max(1, StageOf(crack)) : 0;

    public double Crack(HullPlace place) => Cracks?.GetFloat(place.Code()) ?? 0;

    public bool ByPlace => true;

    public Knock Take(HullPlace place, double loss)
    {
        SetCrack(place, Crack(place) + loss);
        return Condition <= 0 ? Knock.BrokenUp : Knock.Harmed;
    }

    public void Pass(double days, bool afloat, double sun, double waterCelsius = 20)
    {
        if (Cover != null) Weather(days, afloat, sun, waterCelsius);
        if (seep == null || !afloat || GreaseDays <= 0) return;

        Tree.SetDouble(GreaseKey, Math.Max(0, GreaseDays - days));
        Changed();
    }

    private void Weather(double days, bool afloat, double sun, double waterCelsius)
    {
        double wet = afloat ? WetDays + days : Math.Max(0, WetDays - days * sun);
        double rot = afloat ? Cover!.Rot(config.Hides, wet, days, waterCelsius) : 0;
        Tree.SetDouble(WetKey, wet);
        if (rot > 0)
        {
            var cracks = Tree.GetOrAddTreeAttribute(CracksKey);
            foreach (var place in HullShape.Places) cracks.SetFloat(place.Code(), (float)Math.Clamp(Crack(place) + rot, 0, 1));
        }
        Changed();
    }

    public override double Inflow(double seconds, double hours, Afloat afloat, double depthMetres) =>
        base.Inflow(seconds, hours, afloat, depthMetres) + (seep != null && GreaseDays <= 0 ? seep.LitresPerHourASeam * (Cover?.Seams ?? 1) * hours : 0);

    public override double LitresPerSecond(Afloat afloat, double depthMetres) => HullShape.Places.Sum(place => LitresPerSecond(place, afloat, depthMetres));

    public bool Needs(HullRepair repair, HullPlace place) =>
        repair.Greases ? seep != null && GreaseDays < seep.GreaseDays : Worst() is not null;

    public string Mend(HullRepair repair, HullPlace place)
    {
        if (repair.Greases)
        {
            double before = GreaseDays;
            Tree.SetDouble(GreaseKey, Math.Min(seep!.GreaseDays, before + repair.Restores * seep.GreaseDays));
            Changed();
            return $"greased: {before:F1} -> {GreaseDays:F1} days";
        }

        var mended = Crack(place) > 0 ? place : Worst()!.Value;
        double crack = Crack(mended);
        SetCrack(mended, crack - repair.Restores);
        return $"mended the {mended.Code()}: {crack:F2} -> {Crack(mended):F2}";
    }

    public override void Describe(StringBuilder info)
    {
        var cracked = HullShape.Places.Where(place => Stage(place) > 0).ToArray();
        if (cracked.Length == 0) info.AppendLine(Lang.Get("roughseas:vessel-hull-sound"));
        foreach (var place in cracked)
        {
            info.AppendLine(Lang.Get("roughseas:vessel-crack", Lang.Get($"roughseas:vessel-place-{place.Code()}"),
                Lang.Get($"roughseas:vessel-crack-{Stage(place)}")));
        }
        if (seep == null) return;

        int days = (int)Math.Round(GreaseDays);
        info.AppendLine(days > 0 ? Lang.Get("roughseas:vessel-grease-in", days) : Lang.Get("roughseas:vessel-grease-due"));
    }

    public override void Adopt(float integrity) { }

    public override void TurnEndForEnd(ITreeAttribute tree)
    {
        if (tree.GetTreeAttribute(CracksKey) is not { } cracks) return;

        var turned = new TreeAttribute();
        foreach (var place in HullShape.Places.Where(place => cracks.HasAttribute(place.Code())))
        {
            turned.SetFloat(place.EndForEnd().Code(), cracks.GetFloat(place.Code()));
        }
        tree[CracksKey] = turned;
    }

    public override double LitresPerSecond(HullPlace place, Afloat afloat, double depthMetres)
    {
        double head = Waterline(place, afloat) - Math.Max(CrackHeight(place, depthMetres), afloat.InsideMetres);
        if (head <= 0) return 0;

        double area = Crack(place) * config.CrackSquareMetres;
        return config.DischargeCoefficient * area * Math.Sqrt(2 * Gravity * head) * LitresPerCubicMetre;
    }

    // Level: heel will raise it on the lee side, where a breach above the waterline then starts to leak.
    private static double Waterline(HullPlace place, Afloat afloat) => afloat.DraftMetres;

    private double CrackHeight(HullPlace place, double depthMetres) => place == HullPlace.Bottom ? 0 : config.SideCrackShare * depthMetres;

    private HullPlace? Worst() =>
        HullShape.Places.Where(place => Crack(place) > 0).Select(place => (HullPlace?)place).MaxBy(place => Crack(place!.Value));

    private void SetCrack(HullPlace place, double crack)
    {
        var cracks = Tree.GetOrAddTreeAttribute(CracksKey);
        cracks.SetFloat(place.Code(), (float)Math.Clamp(crack, 0, 1));
        Changed();
    }

    private ITreeAttribute? Cracks => Tree?.GetTreeAttribute(CracksKey);
}

public class SeepConfig
{
    public double LitresPerHourASeam;
    public double GreaseDays;
}

public class CoverConfig
{
    public Dictionary<string, int> Hides = [];
    public int Seams;
}

public class HullConfig
{
    private const double SquareCentimetresPerSquareMetre = 10000;

    public double DischargeCoefficient;
    public double CrackSquareCentimetres;

    public double CrackSquareMetres => CrackSquareCentimetres / SquareCentimetresPerSquareMetre;
    public double SideCrackShare;
    public double SoakedStrengthShare;

    [Newtonsoft.Json.JsonIgnore]
    public HidesConfig Hides = new();
}
