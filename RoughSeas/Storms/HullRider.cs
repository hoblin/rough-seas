using RoughSeas.Vessels;

namespace RoughSeas.Storms;

/// <summary>
/// One hull on the drawn sea: the heave it is drawn at and the water it is carried by, read at that same heave. A hull
/// displaces its own mass, so the water's pressure on it grows with its mass as its inertia does, and a hull left to the
/// waves takes the water's velocity whatever it weighs. Its mass tells through its draft and its heave; what smooths the
/// sway is its size against the wave, its heading and its draft (<see cref="IDrawnSea.HullVelocityAt"/>).
/// </summary>
public sealed class HullRider(ResponseConfig config)
{
    private readonly HullResponse response = new(0, 0, config);
    private double followedAt = double.NaN;

    /// <summary>How far the hull floats over still water after the last <see cref="Follow"/>, in metres.</summary>
    public double Lift { get; private set; }

    /// <summary>The pose the hull takes at <paramref name="seconds"/>, on any steady clock of the caller's.</summary>
    /// <param name="yaw">As <see cref="IDrawnSea.PoseOf"/> takes it.</param>
    public SeaPose Follow(IDrawnSea sea, double x, double z, double yaw, RiddenHull hull, double seconds)
    {
        if (hull.HasDraft)
        {
            response.HeavePeriodSeconds = HullResponse.HeavePeriod(hull.Kg, hull.AddedKg, hull.KgPerMetre);
            response.RollPeriodSeconds = HullResponse.RollPeriod(hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, hull.DepthMetres);
        }
        var step = double.IsNaN(followedAt) ? 0 : seconds - followedAt;
        followedAt = seconds;
        var pose = response.Follow(sea.PoseOf(x, z, yaw, hull.LengthMetres, hull.BeamMetres), step);
        Lift = pose.Lift;
        return pose;
    }

    /// <summary>The water's velocity over the hull's underwater body at its heave now, in metres a second of the waves' clock.</summary>
    public (double X, double Z) Water(IDrawnSea sea, double x, double z, double yaw, RiddenHull hull) =>
        sea.HullVelocityAt(x, z, Lift, hull.DraftMetres, yaw, hull.LengthMetres, hull.BeamMetres);

    /// <summary>How the water's velocity over the hull's underwater body changes across a metre at its heave now, per second of the waves' clock.</summary>
    public FlowGradient Gradient(IDrawnSea sea, double x, double z, double yaw, RiddenHull hull) =>
        sea.HullGradientAt(x, z, Lift, hull.DraftMetres, yaw, hull.LengthMetres, hull.BeamMetres);

    /// <summary>Takes the hull out of the waves: it comes to rest and is next met afresh.</summary>
    public void Settle()
    {
        response.Follow(default, 0);
        (followedAt, Lift) = (double.NaN, 0);
    }
}

/// <summary>What the drawn sea meets of a hull now. Without a draft it floats as vanilla draws it, on the surface, following the water as it lies.</summary>
public readonly record struct RiddenHull(double LengthMetres, double BeamMetres, bool HasDraft = false, double DraftMetres = 0, double DepthMetres = 0,
    double Kg = 0, double AddedKg = 0, double KgPerMetre = 0)
{
    public static RiddenHull Of(double lengthMetres, double beamMetres, IBuoyant? buoyant) => buoyant is { HasDraft: true }
        ? new(lengthMetres, beamMetres, true, buoyant.Afloat.DraftMetres, buoyant.DepthMetres, buoyant.Aboard.Total, buoyant.AddedKg, buoyant.KgPerMetre)
        : new(lengthMetres, beamMetres);
}
