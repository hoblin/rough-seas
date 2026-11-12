using RoughSeas.Water;

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

    /// <summary>The waves at a position now, over the bed under it: the one answer to how high they stand, where they run and whether they break there.</summary>
    WaveReading WaveHere(double x, double z);

    /// <summary>
    /// The water's horizontal velocity at a position now, in metres per second of <see cref="Seconds"/>, not of the
    /// tick, averaged over the underwater body of a
    /// hull centred there, as the water's pressure on it sums: <paramref name="lift"/> is how far the hull floats over
    /// still water and <paramref name="draft"/> how deep it reaches under that, in metres; <paramref name="yaw"/>,
    /// <paramref name="length"/> and <paramref name="beam"/> are as <see cref="PoseOf"/> takes them.
    /// </summary>
    (double X, double Z) HullVelocityAt(double x, double z, double lift, double draft, double yaw, double length, double beam);

    /// <summary>
    /// How the water's velocity <see cref="HullVelocityAt"/> reads changes from one metre to the next over the same body,
    /// per second of <see cref="Seconds"/>. A body moving through the water at a velocity r is pushed by −(r·∇)u on top of
    /// the water's own change: down a wave's front face when it runs with the wave.
    /// </summary>
    FlowGradient HullGradientAt(double x, double z, double lift, double draft, double yaw, double length, double beam);

    /// <summary>The water's vertical velocity at a position now, in metres per second of <see cref="Seconds"/>, <paramref name="below"/> metres under the drawn surface: rising ahead of a crest, sinking behind it.</summary>
    double RiseVelocityAt(double x, double z, double below = 0);

    /// <summary>The water's vertical acceleration at a position now, in metres per second squared of <see cref="Seconds"/>, <paramref name="below"/> metres under the drawn surface: downward under a crest, upward in a trough.</summary>
    double RiseAccelerationAt(double x, double z, double below = 0);

    /// <summary>The pose of a hull of <paramref name="length"/> by <paramref name="width"/> blocks centred at a position, sampled at its ends and sides.</summary>
    /// <param name="yaw">Its heading in radians as its renderer turns it: the entity's <c>Pos.Yaw</c> plus its shape's <c>rotateY * GameMath.DEG2RAD</c>; its length runs along the shape's X axis.</param>
    SeaPose PoseOf(double x, double z, double yaw, double length, double width);
}
