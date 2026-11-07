using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoughSeas.Vessels;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

/// <summary>
/// How a vessel's first laths are laid out on the ground before binding ties them into her blank where they lie. The
/// laths laid together either make the layout or they don't: stray ones never become a boat.
/// </summary>
public abstract class LathLayout
{
    public static LathLayout? Of(JToken? layout) => layout?.Value<string>("kind") switch
    {
        "lattice" => layout.ToObject<LatticeLayout>(),
        "pair" => layout.ToObject<PairLayout>(),
        _ => null
    };

    /// <summary>The blank <paramref name="laths"/> lay out, when they are all of one wood, each at least <paramref name="metres"/> long.</summary>
    public LaidOut? Find(IReadOnlyList<LaidLath> laths, double metres) =>
        laths.Count > 0 && laths.All(lath => lath.Wood == laths[0].Wood && lath.Metres >= metres) ? Shaped(laths) : null;

    protected abstract LaidOut? Shaped(IReadOnlyList<LaidLath> laths);
}

/// <summary>
/// Laths laid flat along and across, each crossing every lath across it, as a coracle's bottom was begun: so many each
/// way, at the spacing her model draws them, within a tolerance.
/// </summary>
public sealed class LatticeLayout : LathLayout
{
    [JsonProperty] public int Along;
    [JsonProperty] public int Across;
    [JsonProperty] public double AlongSpacingMetres;
    [JsonProperty] public double AcrossSpacingMetres;
    [JsonProperty] public double ToleranceMetres;

    protected override LaidOut? Shaped(IReadOnlyList<LaidLath> laths)
    {
        var alongX = laths.Where(lath => lath.Along == EnumAxis.X).ToArray();
        var alongZ = laths.Where(lath => lath.Along == EnumAxis.Z).ToArray();
        if (!alongX.All(lath => alongZ.All(lath.Crosses))) return null;

        var (along, across) = Fits(alongX, alongZ) ? (alongX, alongZ) : Fits(alongZ, alongX) ? (alongZ, alongX) : default;
        if (along == null || across == null) return null;

        return new LaidOut(laths[0].Wood, alongZ.Average(lath => lath.X), alongX.Average(lath => lath.Z), along[0].Along, [.. along, .. across]);
    }

    private bool Fits(LaidLath[] along, LaidLath[] across) =>
        along.Length == Along && across.Length == Across && Spaced(along, AlongSpacingMetres) && Spaced(across, AcrossSpacingMetres)
        && Centred(along, across) && Centred(across, along);

    private bool Centred(LaidLath[] laths, LaidLath[] across)
    {
        double OwnAxis(LaidLath lath) => laths[0].Along == EnumAxis.X ? lath.X : lath.Z;
        double middle = across.Average(OwnAxis);
        return laths.All(lath => Math.Abs(OwnAxis(lath) - middle) <= ToleranceMetres);
    }

    private bool Spaced(LaidLath[] laths, double spacing)
    {
        var apart = laths.Select(lath => lath.Along == EnumAxis.X ? lath.Z : lath.X).Order().ToArray();
        double middle = apart.Average();
        return apart.Select((at, n) => at - middle - (n - (apart.Length - 1) / 2.0) * spacing).All(off => Math.Abs(off) <= ToleranceMetres);
    }
}

/// <summary>Two long laths laid side by side at the vessel's beam, whose ends binding draws together into her gunwale.</summary>
public sealed class PairLayout : LathLayout
{
    [JsonProperty] public double BeamMetres;
    [JsonProperty] public double ToleranceMetres;

    protected override LaidOut? Shaped(IReadOnlyList<LaidLath> laths)
    {
        if (laths is not [var a, var b] || a.Along != b.Along) return null;

        var (apart, offset) = a.Along == EnumAxis.X ? (Math.Abs(a.Z - b.Z), Math.Abs(a.X - b.X)) : (Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
        if (Math.Abs(apart - BeamMetres) > ToleranceMetres || offset > ToleranceMetres) return null;

        return new LaidOut(a.Wood, (a.X + b.X) / 2, (a.Z + b.Z) / 2, a.Along, [a, b]);
    }
}

/// <summary>A lath lying on the ground, centred at <see cref="X"/>, <see cref="Z"/> in the world, its length along one axis.</summary>
public sealed record LaidLath(Wood Wood, double X, double Z, EnumAxis Along, double Metres)
{
    public bool Crosses(LaidLath other)
    {
        var (along, across) = Along == EnumAxis.X ? (this, other) : (other, this);
        return along.Along != across.Along
            && Math.Abs(across.X - along.X) <= along.Metres / 2
            && Math.Abs(along.Z - across.Z) <= across.Metres / 2;
    }
}

/// <summary>Laths laid out into a vessel's blank: her middle on the ground, the axis she lies along, and the laths she is bound from.</summary>
public sealed record LaidOut(Wood Wood, double X, double Z, EnumAxis Along, LaidLath[] Laths);
