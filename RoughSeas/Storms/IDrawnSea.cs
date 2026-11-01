namespace RoughSeas.Storms;

/// <summary>
/// The sea surface a client draws now, the one our own vessels ride; on a client alone.
/// Reach it with <c>api.ModLoader.GetModSystem&lt;StormsModSystem&gt;().DrawnSea</c>. Positions are absolute block coordinates.
/// </summary>
public interface IDrawnSea
{
    /// <summary>Whether any swell is drawn now.</summary>
    bool Swell { get; }

    /// <summary>Whether swell is drawn at a position now: none outside the waves' field or where the sea is calm, even while <see cref="Swell"/> holds elsewhere.</summary>
    bool SwellAt(double x, double z);

    /// <summary>The time the waves run on, in seconds: the world's clock at vanilla's pace, the same on every client, still while the world's clock stands.</summary>
    double Seconds { get; }

    /// <summary>The surface's height above still water at a position now, in metres.</summary>
    double HeightAt(double x, double z);

    /// <summary>
    /// The water's horizontal velocity at a position now, in metres a second, as the drawn waves move it: forward under
    /// a crest, back in a trough. <paramref name="depth"/> is in metres under the drawn surface, and the velocity is
    /// averaged over a square <paramref name="side"/> metres across centred there, as a hull of that size feels it.
    /// </summary>
    (double X, double Z) OrbitalVelocityAt(double x, double z, double depth = 0, double side = 0);

    /// <summary>
    /// The water's horizontal velocity at a position now, in metres a second, averaged over the underwater body of a
    /// hull centred there, as the water's pressure on it sums: <paramref name="lift"/> is how far the hull floats over
    /// still water and <paramref name="draft"/> how deep it reaches under that, in metres; <paramref name="yaw"/>,
    /// <paramref name="length"/> and <paramref name="beam"/> are as <see cref="PoseOf"/> takes them.
    /// </summary>
    (double X, double Z) HullVelocityAt(double x, double z, double lift, double draft, double yaw, double length, double beam);

    /// <summary>The pose of a hull of <paramref name="length"/> by <paramref name="width"/> blocks centred at a position, sampled at its ends and sides.</summary>
    /// <param name="yaw">Its heading in radians as its renderer turns it: the entity's <c>Pos.Yaw</c> plus its shape's <c>rotateY * GameMath.DEG2RAD</c>; its length runs along the shape's X axis.</param>
    SeaPose PoseOf(double x, double z, double yaw, double length, double width);
}
