using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

/// <summary>Turns the game's damage points into the energy a hull is struck with, by where the damage came from.</summary>
public class DamageConversion
{
    public Dictionary<EnumDamageSource, double> JoulesPerDamage = new();
    public double DefaultJoulesPerDamage;

    public double Joules(DamageSource source, float damage) =>
        damage * JoulesPerDamage.GetValueOrDefault(source.Source, DefaultJoulesPerDamage);
}
