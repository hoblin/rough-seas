using System;
using System.Collections.Generic;
using System.Linq;
using RoughSeas.Units;
using RoughSeas.Vessels;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A hull hollowed out of a blank by fire and the axe, one hollowing at a time: each takes clay along the rim, a
/// <see cref="LaidFire"/> that burns until its char is deep enough, and axe strokes that cut the char out. A fire
/// burning with the rim unguarded chars the rim too, and once that char reaches through the wall the blank is
/// spoiled. Its state lives in the vessel's watched <c>roughseas:build</c> tree; a vessel with none is finished.
/// </summary>
public sealed class Burnout
{
    /// <summary>The voxels one ball of clay covers, as vanilla's <c>BEClayForm.PutClay</c> gives them.</summary>
    public const int VoxelsPerClay = 25;

    private const string LevelKey = "level";
    private const string ClayKey = "clayVoxels";
    private const string LitKey = "lit";
    private const string CharredKey = "charred";
    private const string BurntKey = "burntHours";
    private const string StrokesKey = "strokes";
    private const string RimCharKey = "rimCharMetres";
    private const string BarkKey = "barkStripped";
    private const string ClayTypeKey = "clayType";
    private const double MillimetresPerMetre = 1000;
    private const double LitresPerCubicMetre = 1000;

    private readonly Func<ITreeAttribute> tree;
    private readonly Action changed;
    private readonly BurnoutConfig config;
    private readonly System.Func<int, int> rimVoxels;
    private readonly Func<double> holdLitres;

    public Burnout(Func<ITreeAttribute> tree, Action changed, BurnoutConfig config, System.Func<int, int> rimVoxels, Func<double> holdLitres)
    {
        (this.tree, this.changed, this.config, this.rimVoxels, this.holdLitres) = (tree, changed, config, rimVoxels, holdLitres);
        Fire = new LaidFire(tree, changed);
    }

    public LaidFire Fire { get; }

    private ITreeAttribute Tree => tree();

    public int Hollowings => config.Hollowings;

    public int Level => Tree?.GetInt(LevelKey, Hollowings) ?? Hollowings;

    public bool Finished => Level >= Hollowings;

    public int Sections => Math.Max(1, config.HewnSectionsMetres.Length);

    public int Stage => Charred ? Strokes * 2 * Sections / StrokesPerHewing : 0;

    public int SectionsCut => Math.Min(Stage, Sections);

    public int SectionsTrimmed => Math.Max(0, Stage - Sections);

    public double Hollowed => (Level + (double)SectionsCut / Sections) / Hollowings;

    public int ClayVoxels => Tree?.GetInt(ClayKey) ?? 0;

    /// <summary>How much of the way from the blank to the finished hull her outside is worked.</summary>
    public double Worked => (Level + (double)SectionsTrimmed / Sections) / Hollowings;

    public int RimVoxels => rimVoxels(Level);

    public int ClayBalls => (int)Math.Ceiling((double)RimVoxels / VoxelsPerClay);

    public int ClayBallsOn => (int)Math.Ceiling((double)ClayVoxels / VoxelsPerClay);

    public bool Clayed => ClayVoxels >= RimVoxels;

    /// <summary>The kind of clay on the rim, as its first ball was.</summary>
    public string? ClayType => Tree?.GetString(ClayTypeKey);

    public bool Lit => Tree?.GetBool(LitKey) == true;

    public bool Charred => Tree?.GetBool(CharredKey) == true;

    public double HoursBurnt => Tree?.GetDouble(BurntKey) ?? 0;

    /// <summary>
    /// How far the fire has burnt, from 0 while it is catching, its first <paramref name="catchingHours"/>, through
    /// <paramref name="phases"/> shares of the hours after.
    /// </summary>
    public int BurningPhase(int phases, double catchingHours) =>
        HoursBurnt < catchingHours ? 0 : Math.Clamp((int)((HoursBurnt - catchingHours) / (HoursPerFire - catchingHours) * phases) + 1, 1, phases);

    public int BarkStripped => Tree?.GetInt(BarkKey) ?? 0;

    public int BarkPieces => config.BarkPieces;

    public bool Debarked => Level > 0 || BarkStripped >= BarkPieces;

    public double HoursPerFire => CharMetres * MillimetresPerMetre / config.CharMillimetresPerHour;

    public double RimCharMetres => Tree?.GetDouble(RimCharKey) ?? 0;

    public double WallMetres => config.WallMetres;

    public bool Spoiled => RimCharMetres >= config.WallMetres;

    public int Strokes => Tree?.GetInt(StrokesKey) ?? 0;

    public int StrokesPerHewing =>
        Math.Max(1, (int)Math.Ceiling(holdLitres() / Hollowings / (config.AxeEdgeMetres * CharMetres * config.HollowBeamMetres * LitresPerCubicMetre)));

    public double UnhewnLitres => config.UnhewnLitres * (1 - Worked);

    public double TopMetres => Toward(config.BlankDepthMetres, config.RimMetres);

    public double FloorMetres => Toward(config.BlankDepthMetres, config.RimMetres, Hollowed) - config.HollowDepthMetres * Hollowed;

    public double Toward(double blank, double finished) => Toward(blank, finished, Worked);

    private static double Toward(double blank, double finished, double share) => blank + (finished - blank) * share;

    /// <summary>Counted from 1 at her bow; her ends lie in the first and last.</summary>
    public int SectionAt(double alongMetres) => Math.Max(1, config.HewnSectionsMetres.Count(span => alongMetres >= span[0]));

    public bool Trimmed(int section) => section > Sections - SectionsTrimmed;

    public bool Cut(int section) => section <= SectionsCut;

    public float SaturationPerStroke => config.HewSaturation / StrokesPerHewing;

    private double CharMetres => config.HollowDepthMetres / Hollowings;

    /// <summary>The voxels a layer one voxel thick takes over the outline's elements, seen from above.</summary>
    public static int TopVoxels(ShapeElement outline) => (int)Math.Round(TopArea(outline));

    /// <summary>A build saved by the hull it had before keeps where it stood: a rim clayed to the last ball by its rim
    /// then, each level's in <paramref name="rims"/>, stays clayed, and a hewing keeps its share of
    /// <paramref name="strokes"/> a hewing, rounded up so no section it cut comes back.</summary>
    public void KeepProgress(int[] rims, int strokes)
    {
        if (Tree is not { } tree) return;

        if (Level < rims.Length && ClayVoxels > 0 && ClayVoxels >= rims[Level]) tree.SetInt(ClayKey, RimVoxels);
        if (strokes > 0 && Strokes > 0) tree.SetInt(StrokesKey, (int)Math.Ceiling((double)Strokes * StrokesPerHewing / strokes));
    }

    /// <summary>The pieces of a level's clay not on the rim yet, in the order the balls cover it, and those over a
    /// section the hewing has trimmed.</summary>
    public IEnumerable<string> Unclayed(ShapeElement clay)
    {
        double coated = 0;
        foreach (var piece in clay.Children ?? [])
        {
            if (coated >= ClayVoxels || Trimmed(SectionAt(Voxels.ToMetres(piece.From[0]) - HullShape.ModelOriginMetres))) yield return piece.Name;
            coated += TopArea(piece);
        }
    }

    private static double TopArea(ShapeElement element) =>
        (element is { From: { } from, To: { } to } ? Math.Abs((to[0] - from[0]) * (to[2] - from[2])) : 0) + (element.Children?.Sum(TopArea) ?? 0);

    public static void Begin(ITreeAttribute tree) => tree.SetInt(LevelKey, 0);

    public string? ClayRefusal() => Busy() ?? (Clayed ? "shipyard-clayed-already" : null);

    public string? FirewoodRefusal() => Busy() ?? Fire.FirewoodRefusal();

    public string? KindlingRefusal() => Busy() ?? Fire.KindlingRefusal();

    public string? LightRefusal() => Busy() ?? (!Fire.Laid ? "shipyard-lay-fire-first" : null);

    public string? HewRefusal() =>
        Spoiled ? "shipyard-spoiled"
        : Lit ? "shipyard-burning"
        : !Debarked ? null
        : !Charred ? "shipyard-burn-first"
        : null;

    public void Clay(string? type = null)
    {
        if (ClayVoxels == 0 && type != null) Tree.SetString(ClayTypeKey, type);
        Tree.SetInt(ClayKey, Math.Min(RimVoxels, ClayVoxels + VoxelsPerClay));
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
        if (!Clayed) Tree.SetDouble(RimCharKey, RimCharMetres + (Math.Min(burnt, HoursPerFire) - HoursBurnt) * config.CharMillimetresPerHour / MillimetresPerMetre);
        if (burnt < HoursPerFire)
        {
            bool newHour = Math.Floor(burnt) > Math.Floor(HoursBurnt);
            Tree.SetDouble(BurntKey, burnt);
            if (newHour) changed();
            return false;
        }

        Tree.SetBool(LitKey, false);
        Fire.Spend();
        Tree.SetBool(CharredKey, true);
        Tree.SetDouble(BurntKey, 0);
        changed();
        return true;
    }

    /// <returns>Whether this stroke finished the work it was part of: the bark's last piece, or the last of the char.</returns>
    public bool Stroke()
    {
        if (!Debarked)
        {
            Tree.SetInt(BarkKey, BarkStripped + 1);
            changed();
            return Debarked;
        }

        if (Strokes + 1 < StrokesPerHewing)
        {
            Tree.SetInt(StrokesKey, Strokes + 1);
            changed();
            return false;
        }

        Tree.SetInt(StrokesKey, 0);
        Tree.SetInt(LevelKey, Level + 1);
        Tree.SetInt(ClayKey, 0);
        Tree.RemoveAttribute(ClayTypeKey);
        Tree.SetBool(CharredKey, false);
        changed();
        return true;
    }

    private string? Busy() =>
        Spoiled ? "shipyard-spoiled"
        : Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : !Debarked ? "shipyard-debark-first"
        : null;
}

public sealed class BurnoutConfig
{
    public int Hollowings = 1;
    public double HollowDepthMetres;
    public double RimMetres;
    public double CharMillimetresPerHour = 1;
    public string Clay = "";
    public double AxeEdgeMetres = 1;
    public double HollowBeamMetres = 1;
    public double WallMetres = 1;
    public double SecondsPerStroke = 1;
    public float HewSaturation;
    public double BlankDepthMetres;
    public double BlankWaterplaneSquareMetres;
    public double UnhewnLitres;
    public int BarkPieces;
    public double HollowLengthMetres;
    public double[][] HewnSectionsMetres = [];
    public BoxMetres[] HullBoxesMetres = [];
    public int[] RimVoxelsBefore = [];
    public int StrokesPerHewingBefore;
}

public sealed class BoxMetres
{
    public double[] From = [];
    public double[] To = [];
}
