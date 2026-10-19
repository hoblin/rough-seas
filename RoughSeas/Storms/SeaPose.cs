namespace RoughSeas.Storms;

/// <summary>A hull's pose on the drawn sea; all zeros where no swell is drawn.</summary>
/// <param name="Hold">0–1: how much the drawn swell holds the hull against its own rocking, the weight to blend by.</param>
/// <param name="Lift">Metres the surface lifts it, added to its model's height.</param>
/// <param name="Roll">Radians about its length, as the renderer's <c>xangle</c>.</param>
/// <param name="Pitch">Radians about its width, as the renderer's <c>zangle</c>.</param>
public readonly record struct SeaPose(double Hold, double Lift, double Roll, double Pitch);
