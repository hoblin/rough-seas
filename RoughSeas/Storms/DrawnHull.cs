namespace RoughSeas.Storms;

/// <param name="Lift">Metres over where the server keeps her, added to her model's height.</param>
/// <param name="XAngle">The renderer's <c>xangle</c> she is drawn at, her roll; radians, as the next two.</param>
/// <param name="YAngle">The renderer's <c>yangle</c>.</param>
/// <param name="ZAngle">The renderer's <c>zangle</c>, her pitch.</param>
public readonly record struct DrawnHull(double Lift, float XAngle, float YAngle, float ZAngle);
