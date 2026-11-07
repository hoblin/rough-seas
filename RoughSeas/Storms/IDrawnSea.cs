namespace RoughSeas.Storms;

/// <summary>
/// The sea surface a client draws now, the one our own vessels ride. Reach it on a client with
/// <c>api.ModLoader.GetModSystem&lt;StormsModSystem&gt;().DrawnSea</c>; the server builds the same around a body in the water
/// (<see cref="SwellAround"/>). Positions are absolute block coordinates.
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

    /// <summary>The pose of a hull of <paramref name="length"/> by <paramref name="width"/> blocks centred at a position, sampled at its ends and sides.</summary>
    /// <param name="yaw">Its heading in radians as its renderer turns it: the entity's <c>Pos.Yaw</c> plus its shape's <c>rotateY * GameMath.DEG2RAD</c>; its length runs along the shape's X axis.</param>
    SeaPose PoseOf(double x, double z, double yaw, double length, double width);
}
