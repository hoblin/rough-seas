namespace RoughSeas.Person;

/// <summary>
/// A body in the drawn sea, measured as vanilla's player physics measures one in the blocks' water: swimming while its
/// chest (the feet plus vanilla's <c>SwimmingOffsetY</c>) lies under the surface, its feet in the water while they do.
/// Heights are absolute block coordinates.
/// </summary>
public readonly record struct Immersion(double Surface, double Feet, double Chest)
{
    /// <summary>Whether the body swims: vanilla's <c>Entity.Swimming</c>, set where the chest is in water.</summary>
    public bool Swimming => Chest < Surface;

    /// <summary>Whether the feet are in the water: vanilla's <c>Entity.FeetInLiquid</c>, which gravity and drag go by.</summary>
    public bool FeetIn => Feet < Surface;

    /// <summary>How far the chest is under the surface, negative above it.</summary>
    public double Depth => Surface - Chest;
}
