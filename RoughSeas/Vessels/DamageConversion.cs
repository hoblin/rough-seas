using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

/// <summary>Turns the game's damage points into the energy a hull is struck with, by where the damage came from.</summary>
public class DamageConversion
{
    public Dictionary<EnumDamageSource, double> JoulesPerDamage = new();
    public double DefaultJoulesPerDamage;
    public HashSet<EnumDamageSource> BroadSources = [];
    public double CutShare;
    public Dictionary<Blow, double> Variation = new();

    public double Joules(DamageSource source, float damage) =>
        damage * JoulesPerDamage.GetValueOrDefault(source.Source, DefaultJoulesPerDamage);

    public Blow BlowOf(DamageSource source) =>
        source.Type == EnumDamageType.SlashingAttack ? Blow.Cut
        : BroadSources.Contains(source.Source) ? Blow.Broad
        : Blow.Point;
}
