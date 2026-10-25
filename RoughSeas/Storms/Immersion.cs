using System;

namespace RoughSeas.Storms;

/// <summary>
/// A body in the drawn sea, measured as vanilla's player physics measures one in the blocks' water: swimming while
/// its chest (the feet plus vanilla's <c>SwimmingOffsetY</c>) lies under the surface, its feet in the water while they
/// do, and pushed by a stroke in proportion to how much of it is under. Heights are absolute block coordinates.
/// </summary>
public readonly record struct Immersion(double Surface, double Feet, double Chest)
{
    // vanilla's physics runs at 60 ticks a second and writes its per-tick pushes as dt * 60
    private const double TicksPerSecond = 60;

    /// <summary>Whether the body swims: vanilla's <c>Entity.Swimming</c>, set where the chest is in water.</summary>
    public bool Swimming => Chest < Surface;

    /// <summary>Whether the feet are in the water: vanilla's <c>Entity.FeetInLiquid</c>, which gravity and drag go by.</summary>
    public bool FeetIn => Feet < Surface;

    /// <summary>How far the chest is under the surface, negative above it.</summary>
    public double Depth => Surface - Chest;

    /// <summary>
    /// The share of the body under water that scales a stroke's lift, as <c>PModulePlayerInLiquid.HandleSwimming</c>
    /// takes it: the chest's depth up to a block, never less than <see cref="SwimConfig.LeastShare"/>.
    /// </summary>
    public double Share(SwimConfig swim) => Math.Min(1, Math.Clamp(Depth, 0, 1) + swim.LeastShare);

    /// <summary>
    /// The upward motion a stroke adds this tick at <paramref name="share"/> under water, as
    /// <c>PModulePlayerInLiquid.HandleSwimming</c> adds it: holding jump lifts the body slowly (while moving, only
    /// once enough of it is under), otherwise the stroke follows the controls' fly vector and the stroke's push.
    /// </summary>
    public static double Lift(SwimConfig swim, double share, Stroke stroke, double dt)
    {
        if (stroke.Jump)
        {
            return share > swim.JumpLiftAboveShare || !stroke.Moving ? swim.JumpLiftPerTick * share * dt * TicksPerSecond : 0;
        }
        return stroke.Upward * (1 + stroke.Push) * swim.StrokeLift * share;
    }
}

/// <summary>What a swimmer's controls ask of one tick's stroke: <c>EntityControls.Jump</c>, <c>TriesToMove</c>, <c>FlyVector.Y</c> and the module's <c>Push</c>.</summary>
public readonly record struct Stroke(bool Jump, bool Moving, double Upward, double Push);
