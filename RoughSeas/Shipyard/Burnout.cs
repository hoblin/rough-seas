using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A hull hollowed out of a blank by fire and the axe, one hollowing at a time: each takes clay along the rim (the
/// first fire needs none), a fire that burns until its char is deep enough, and a hewing that cuts the char out. Its
/// state lives in the vessel's watched <c>roughseas:build</c> tree; a vessel with none is finished.
/// </summary>
public sealed class Burnout(Func<ITreeAttribute> tree, Action changed, BurnoutConfig config)
{
    private const string LevelKey = "level";
    private const string ClayedKey = "clayed";
    private const string LitKey = "lit";
    private const string CharredKey = "charred";
    private const string BurntKey = "burntHours";
    private const double MillimetresPerMetre = 1000;

    private ITreeAttribute Tree => tree();

    public int Hollowings => config.Hollowings;

    public int Level => Tree?.GetInt(LevelKey, Hollowings) ?? Hollowings;

    public bool Finished => Level >= Hollowings;

    public double Hollowed => (double)Level / Hollowings;

    public bool Clayed => Tree?.GetBool(ClayedKey) == true;

    public bool Lit => Tree?.GetBool(LitKey) == true;

    public bool Charred => Tree?.GetBool(CharredKey) == true;

    public double HoursBurnt => Tree?.GetDouble(BurntKey) ?? 0;

    public double HoursPerFire => config.HollowDepthMetres / Hollowings * MillimetresPerMetre / config.CharMillimetresPerHour;

    public bool NeedsClay => Level > 0;

    public static void Begin(ITreeAttribute tree)
    {
        tree.SetInt(LevelKey, 0);
        tree.SetBool(LitKey, true);
    }

    public string? ClayRefusal() =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : !NeedsClay ? "shipyard-no-clay-yet"
        : Clayed ? "shipyard-clayed-already"
        : null;

    public string? LightRefusal() =>
        Lit ? "shipyard-burning"
        : Charred ? "shipyard-hew-first"
        : NeedsClay && !Clayed ? "shipyard-clay-first"
        : null;

    public string? HewRefusal() =>
        Lit ? "shipyard-burning"
        : !Charred ? "shipyard-burn-first"
        : null;

    public void Clay()
    {
        Tree.SetBool(ClayedKey, true);
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
        Tree.SetBool(ClayedKey, false);
        Tree.SetBool(CharredKey, true);
        Tree.SetDouble(BurntKey, 0);
        changed();
        return true;
    }

    public void Hew()
    {
        Tree.SetInt(LevelKey, Level + 1);
        Tree.SetBool(CharredKey, false);
        changed();
    }
}

public sealed class BurnoutConfig
{
    public int Hollowings = 1;
    public double HollowDepthMetres;
    public double RimMetres;
    public double CharMillimetresPerHour = 1;
    public string Clay = "";
    public float HewSaturation;
}
