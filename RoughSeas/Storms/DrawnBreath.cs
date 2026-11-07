using System;

namespace RoughSeas.Storms;

/// <summary>What a breath check finds at an eye under the drawn sea.</summary>
public enum BreathVerdict
{
    /// <summary>Not in the water: vanilla's check stands.</summary>
    Dry,

    /// <summary>Aboard a vessel, which its client lifts onto the drawn sea with its crew: vanilla's check stands.</summary>
    Aboard,

    /// <summary>No swell drawn over the eye, no water under it within the swell's reach, or a solid block between: vanilla's check stands.</summary>
    NoSurface,

    /// <summary>The eye is in air and above the drawn surface: vanilla's check stands.</summary>
    Above,

    /// <summary>A crest covers the eye, or the eye is under water and stays under it: out of air.</summary>
    Under,

    /// <summary>The eye's block holds water, but a trough has drawn the surface below the eye: breathing.</summary>
    Bared,
}

/// <summary>Judges an eye's breath by the drawn surface over its water column (<see cref="DrawnWater"/>).</summary>
public static class DrawnBreath
{
    internal static BreathVerdict Judge<TColumn>(DrawnWater water, TColumn column, double x, double eyeY, double z) where TColumn : IWaterColumn
    {
        if (water.SurfaceAt(column, x, eyeY, z) is not { } surface) return BreathVerdict.NoSurface;
        if (surface > eyeY) return BreathVerdict.Under;
        var at = (int)Math.Floor(eyeY);
        // an eye in a solid block suffocates as vanilla has it, water or not
        return column.Water(at) && !column.Solid(at) ? BreathVerdict.Bared : BreathVerdict.Above;
    }

    /// <summary>Whether the eye has air by the verdict, or null where vanilla's check stands.</summary>
    public static bool? HasAir(BreathVerdict verdict) => verdict switch
    {
        BreathVerdict.Under => false,
        BreathVerdict.Bared => true,
        _ => null,
    };
}
