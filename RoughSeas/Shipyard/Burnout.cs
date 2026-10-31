using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A hull hollowed out of a blank by fire and the axe, one hollowing at a time: each takes clay along the rim (the
/// first fire needs none), a fire laid as for a charcoal pit that burns until its char is deep enough, and axe strokes
/// that cut the char out. Its state lives in the vessel's watched <c>roughseas:build</c> tree; a vessel with none is
/// finished.
/// </summary>
public sealed class Burnout(Func<ITreeAttribute> tree, Action changed, BurnoutConfig config, int rimVoxels, Func<double> holdLitres)
{
    public const int VoxelsPerClay = 25;
    public const int FirepitFirewood = 4;

    private const string LevelKey = "level";
    private const string LegacyClayedKey = "clayed";
    private const string ClayKey = "clayVoxels";
    private const string LitKey = "lit";
    private const string CharredKey = "charred";
    private const string BurntKey = "burntHours";
    private const string PileKey = "firewood";
    private const string KindledKey = "kindled";
    private const string FirepitKey = "firepit";
    private const string StrokesKey = "strokes";
    private const double MillimetresPerMetre = 1000;
    private const double LitresPerCubicMetre = 1000;

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

    public int Firewood => Tree?.GetInt(PileKey) ?? 0;

    public int FirewoodPile => config.FirewoodPile;

    public bool Kindled => Tree?.GetBool(KindledKey) == true;

    public int Firepit => Tree?.GetInt(FirepitKey) ?? 0;

    public bool Laid => Firepit >= FirepitFirewood;

    public int Strokes => Tree?.GetInt(StrokesKey) ?? 0;

    public int StrokesPerHewing =>
        (int)Math.Ceiling(holdLitres() / Hollowings / (config.AxeEdgeMetres * config.AxeEdgeMetres * CharMetres * LitresPerCubicMetre));

    public float SaturationPerStroke => config.HewSaturation / StrokesPerHewing;

    private double CharMetres => config.HollowDepthMetres / Hollowings;

    public static int VoxelsOver(ShapeElement outline) => (int)Math.Round(TopArea(outline));

    private static double TopArea(ShapeElement element) =>
        Math.Abs((element.To[0] - element.From[0]) * (element.To[2] - element.From[2])) + (element.Children?.Sum(TopArea) ?? 0);

    public static void Begin(ITreeAttribute tree)
    {
        tree.SetInt(LevelKey, 0);
        tree.SetInt(FirepitKey, FirepitFirewood);
        tree.SetBool(LitKey, true);
    }

    public void Migrate()
    {
        if (Tree.GetBool(LegacyClayedKey)) Tree.SetInt(ClayKey, rimVoxels);
        if (HoursBurnt > 0 || Lit) Tree.SetInt(FirepitKey, FirepitFirewood);
        Tree.RemoveAttribute(LegacyClayedKey);
    }

    public string? ClayRefusal() =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : !NeedsClay ? "shipyard-no-clay-yet"
        : Clayed ? "shipyard-clayed-already"
        : null;

    public string? FirewoodRefusal(bool piles) =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : Laid ? "shipyard-laid-already"
        : Firewood < FirewoodPile ? piles ? null : "shipyard-pile-first"
        : !Kindled ? "shipyard-kindle-first"
        : null;

    public string? KindlingRefusal() =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : Kindled ? "shipyard-kindled-already"
        : Firewood < FirewoodPile ? "shipyard-pile-first"
        : null;

    public string? LightRefusal() =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : !Laid ? "shipyard-lay-fire-first"
        : NeedsClay && !Clayed ? "shipyard-clay-first"
        : null;

    public string? HewRefusal() =>
        Lit ? "shipyard-burning"
        : !Charred ? "shipyard-burn-first"
        : null;

    public void Clay()
    {
        Tree.SetInt(ClayKey, Math.Min(rimVoxels, ClayVoxels + VoxelsPerClay));
        changed();
    }

    /// <returns>How many of <paramref name="offered"/> the fire takes: onto the pile while it lacks any, else one onto the firepit.</returns>
    public int LayFirewood(int offered)
    {
        int taken = Firewood < FirewoodPile ? Math.Min(offered, FirewoodPile - Firewood) : 1;
        if (Firewood < FirewoodPile) Tree.SetInt(PileKey, Firewood + taken);
        else Tree.SetInt(FirepitKey, Firepit + 1);
        changed();
        return taken;
    }

    public void Kindle()
    {
        Tree.SetBool(KindledKey, true);
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
        Tree.SetInt(PileKey, 0);
        Tree.SetBool(KindledKey, false);
        Tree.SetInt(FirepitKey, 0);
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
