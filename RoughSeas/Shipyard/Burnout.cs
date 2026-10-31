using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A hull hollowed out of a blank by fire and the axe, one hollowing at a time: each takes clay along the rim (the
/// first fire needs none), a <see cref="LaidFire"/> that burns until its char is deep enough, and axe strokes that cut
/// the char out. Its state lives in the vessel's watched <c>roughseas:build</c> tree; a vessel with none is finished.
/// </summary>
public sealed class Burnout
{
    /// <summary>The voxels one ball of clay covers, as vanilla's <c>BEClayForm.PutClay</c> gives them.</summary>
    public const int VoxelsPerClay = 25;

    private const string LevelKey = "level";
    private const string LegacyClayedKey = "clayed";
    private const string ClayKey = "clayVoxels";
    private const string LitKey = "lit";
    private const string CharredKey = "charred";
    private const string BurntKey = "burntHours";
    private const string StrokesKey = "strokes";
    private const double MillimetresPerMetre = 1000;
    private const double LitresPerCubicMetre = 1000;

    private readonly Func<ITreeAttribute> tree;
    private readonly Action changed;
    private readonly BurnoutConfig config;
    private readonly int rimVoxels;
    private readonly Func<double> holdLitres;

    public Burnout(Func<ITreeAttribute> tree, Action changed, BurnoutConfig config, int rimVoxels, Func<double> holdLitres)
    {
        (this.tree, this.changed, this.config, this.rimVoxels, this.holdLitres) = (tree, changed, config, rimVoxels, holdLitres);
        Fire = new LaidFire(tree, changed, config.FirewoodPile);
    }

    public LaidFire Fire { get; }

    private ITreeAttribute Tree => tree();

    public int Hollowings => config.Hollowings;

    public int Level => Tree?.GetInt(LevelKey, Hollowings) ?? Hollowings;

    public bool Finished => Level >= Hollowings;

    public double Hollowed => (double)Level / Hollowings;

    public int ClayVoxels => Tree?.GetInt(ClayKey) ?? 0;

    public int RimVoxels => rimVoxels;

    public int ClayBalls => (int)Math.Ceiling((double)rimVoxels / VoxelsPerClay);

    public int ClayBallsOn => (int)Math.Ceiling((double)ClayVoxels / VoxelsPerClay);

    public bool Clayed => ClayVoxels >= rimVoxels;

    public bool Lit => Tree?.GetBool(LitKey) == true;

    public bool Charred => Tree?.GetBool(CharredKey) == true;

    public double HoursBurnt => Tree?.GetDouble(BurntKey) ?? 0;

    public double HoursPerFire => CharMetres * MillimetresPerMetre / config.CharMillimetresPerHour;

    public bool NeedsClay => Level > 0;

    public int Strokes => Tree?.GetInt(StrokesKey) ?? 0;

    public int StrokesPerHewing =>
        Math.Max(1, (int)Math.Ceiling(holdLitres() / Hollowings / (config.AxeEdgeMetres * config.AxeEdgeMetres * CharMetres * LitresPerCubicMetre)));

    public float SaturationPerStroke => config.HewSaturation / StrokesPerHewing;

    private double CharMetres => config.HollowDepthMetres / Hollowings;

    /// <summary>The voxels a layer one voxel thick takes over the outline's elements, seen from above.</summary>
    public static int TopVoxels(ShapeElement outline) => (int)Math.Round(TopArea(outline));

    private static double TopArea(ShapeElement element) =>
        (element is { From: { } from, To: { } to } ? Math.Abs((to[0] - from[0]) * (to[2] - from[2])) : 0) + (element.Children?.Sum(TopArea) ?? 0);

    public static void Begin(ITreeAttribute tree)
    {
        tree.SetInt(LevelKey, 0);
        LaidFire.Lay(tree);
        tree.SetBool(LitKey, true);
    }

    public void Migrate()
    {
        if (Tree.GetBool(LegacyClayedKey)) Tree.SetInt(ClayKey, rimVoxels);
        if (HoursBurnt > 0 || Lit) LaidFire.Lay(Tree);
        Tree.RemoveAttribute(LegacyClayedKey);
    }

    public string? ClayRefusal() =>
        Busy() ?? (!NeedsClay ? "shipyard-no-clay-yet" : Clayed ? "shipyard-clayed-already" : null);

    public string? FirewoodRefusal(bool piles) => Busy() ?? Fire.FirewoodRefusal(piles);

    public string? KindlingRefusal() => Busy() ?? Fire.KindlingRefusal();

    public string? LightRefusal() =>
        Busy() ?? (!Fire.Laid ? "shipyard-lay-fire-first" : NeedsClay && !Clayed ? "shipyard-clay-first" : null);

    public string? HewRefusal() =>
        Lit ? "shipyard-burning"
        : !Charred ? "shipyard-burn-first"
        : null;

    public void Clay()
    {
        Tree.SetInt(ClayKey, Math.Min(rimVoxels, ClayVoxels + VoxelsPerClay));
        changed();
    }

    public void Light()
    {
        Tree.SetBool(LitKey, true);
        changed();
    }

    public void Douse()
    {
        Tree.SetBool(LitKey, false);
        changed();
    }

    public bool Burn(double hours)
    {
        if (!Lit || hours <= 0) return false;

        double burnt = HoursBurnt + hours;
        if (burnt < HoursPerFire)
        {
            bool newHour = Math.Floor(burnt) > Math.Floor(HoursBurnt);
            Tree.SetDouble(BurntKey, burnt);
            if (newHour) changed();
            return false;
        }

        Tree.SetBool(LitKey, false);
        Tree.SetInt(ClayKey, 0);
        Fire.Spend();
        Tree.SetBool(CharredKey, true);
        Tree.SetDouble(BurntKey, 0);
        changed();
        return true;
    }

    /// <returns>Whether this stroke cut out the last of the char.</returns>
    public bool Stroke()
    {
        if (Strokes + 1 < StrokesPerHewing)
        {
            Tree.SetInt(StrokesKey, Strokes + 1);
            changed();
            return false;
        }

        Tree.SetInt(StrokesKey, 0);
        Tree.SetInt(LevelKey, Level + 1);
        Tree.SetBool(CharredKey, false);
        changed();
        return true;
    }

    private string? Busy() =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : null;
}

public sealed class BurnoutConfig
{
    public int Hollowings = 1;
    public double HollowDepthMetres;
    public double RimMetres;
    public double CharMillimetresPerHour = 1;
    public string Clay = "";
    public int FirewoodPile = 1;
    public double AxeEdgeMetres = 1;
    public double SecondsPerStroke = 1;
    public float HewSaturation;
}
