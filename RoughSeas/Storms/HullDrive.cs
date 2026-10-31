using System;

namespace RoughSeas.Storms;

/// <summary>
/// How a hull of its own mass, size and draft is carried to and fro by the water the drawn waves move: one per hull,
/// stepped with the physics. The water acts on a free hull through its velocity relative to the hull's own
/// (Morison's equation in its relative form, the Maxey–Riley equation for a body that floats):
/// <code>
/// (m + m_a) dv/dt = ρV Du/Dt + m_a Du/Dt + ½ ρ C_D A |u − v| (u − v)
/// </code>
/// u is the water's velocity averaged over the hull's underwater body (<see cref="IDrawnSea.HullVelocityAt"/>), so a
/// hull long or broad against a wave, or deep in it, feels less of it. The first term is the water's pressure on the
/// hull (the Froude–Krylov force), the second the water the hull drags with it (added mass), the third the drag of its
/// underwater body. A floating hull displaces its own mass, ρV = m, so the water's pressure grows with the hull's mass
/// as its inertia does, and a hull left to the waves takes the averaged water's velocity whatever it weighs; its mass,
/// added mass and drag set how fast it comes back to it once something else has moved it.
/// <para>
/// The hull rides its own heave (<see cref="HullResponse"/>), and the water is read where it rides, so a hull that
/// follows the water nets the Stokes drift of the waves it feels, less what its size and draft average out.
/// </para>
/// </summary>
public sealed class HullDrive(DriveConfig config, ResponseConfig response, double waterDensity)
{
    private readonly HullResponse heave = new(0, 0, response);
    private Shape shape;
    private (double Surge, double Sway) added;
    private bool riding;

    /// <summary>The hull's velocity over the ground from the waves, in metres a second; on top of its way through the water.</summary>
    public (double X, double Z) Velocity { get; private set; }

    /// <summary>The water's velocity averaged over the hull at the last step, in metres a second.</summary>
    public (double X, double Z) Water { get; private set; }

    /// <summary>The hull's heave at the last step, in metres over still water.</summary>
    public double Lift { get; private set; }

    /// <summary>Its added mass at the last step, as shares of its own mass: along its length and across it.</summary>
    public (double Surge, double Sway) Added => added;

    /// <summary>The hull's velocity from the waves after <paramref name="seconds"/> more, at a position and heading.</summary>
    /// <param name="yaw">As <see cref="IDrawnSea.PoseOf"/> takes it.</param>
    public (double X, double Z) Step(IDrawnSea sea, double x, double z, double yaw, DrivenHull hull, double seconds)
    {
        if (!(seconds > 0)) return Velocity;
        var pose = sea.PoseOf(x, z, yaw, hull.LengthMetres, hull.BeamMetres);
        if (pose.Hold <= 0)
        {
            Settle();
            return Velocity;
        }

        heave.HeavePeriodSeconds = HullResponse.HeavePeriod(hull.DraftMetres, response);
        heave.RollPeriodSeconds = HullResponse.RollPeriod(hull.LengthMetres, hull.BeamMetres, hull.DraftMetres, hull.DepthMetres);
        Lift = heave.Follow(pose, seconds).Lift;
        var water = sea.HullVelocityAt(x, z, Lift, hull.DraftMetres, yaw, hull.LengthMetres, hull.BeamMetres);
        if (!riding)
        {
            // a hull first met is taken as long in this sea, moving with its water
            riding = true;
            (Water, Velocity) = (water, water);
            return Velocity;
        }

        // the pressure and the added mass give the hull the water's acceleration: (m + m_a) dv = (m + m_a) du
        var moved = (X: Velocity.X + water.X - Water.X, Z: Velocity.Z + water.Z - Water.Z);
        Water = water;
        Velocity = Dragged(moved, water, yaw, hull, seconds);
        return Velocity;
    }

    /// <summary>Takes the hull out of the waves: it is next met as long in the sea it then floats in.</summary>
    public void Settle()
    {
        riding = false;
        (Water, Velocity, Lift) = (default, default, 0);
    }

    /// <summary>
    /// The drag of the hull's underwater body on its velocity relative to the water, along and across it, against its
    /// mass and added mass, taken implicitly so that a light hull in a big sea stays stable.
    /// </summary>
    private (double X, double Z) Dragged((double X, double Z) hullVelocity, (double X, double Z) water, double yaw, DrivenHull hull,
        double seconds)
    {
        var (sin, cos) = Math.SinCos(yaw + Math.PI / 2);
        var (relativeX, relativeZ) = (water.X - hullVelocity.X, water.Z - hullVelocity.Z);
        var along = relativeX * cos - relativeZ * sin;
        var across = relativeX * sin + relativeZ * cos;
        var speed = Math.Sqrt(along * along + across * across);
        if (speed <= 0 || hull.MassKg <= 0) return hullVelocity;

        added = AddedMass(hull);
        var draft = Math.Max(hull.DraftMetres, 0);
        var surge = 0.5 * waterDensity * Friction(speed, hull.LengthMetres) * hull.LengthMetres * (hull.BeamMetres + 2 * draft);
        var sway = 0.5 * waterDensity * CrossFlow(hull.LengthMetres, draft) * hull.LengthMetres * draft;
        along /= 1 + surge * speed * seconds / (hull.MassKg * (1 + added.Surge));
        across /= 1 + sway * speed * seconds / (hull.MassKg * (1 + added.Sway));
        return (water.X - (along * cos + across * sin), water.Z - (-along * sin + across * cos));
    }

    /// <summary>The skin friction coefficient of the ITTC 1957 model–ship correlation line, on the wetted surface.</summary>
    public double Friction(double metresPerSecond, double lengthMetres)
    {
        var reynolds = Math.Max(config.MinReynolds, metresPerSecond * lengthMetres / config.KinematicViscosity);
        var log = Math.Log10(reynolds) - config.FrictionLogOffset;
        return config.FrictionCoefficient / (log * log);
    }

    /// <summary>The cross-flow drag coefficient on the hull's lateral area under water, by its length over its draft.</summary>
    public double CrossFlow(double lengthMetres, double draftMetres)
    {
        var slenderness = draftMetres > 0 ? lengthMetres / draftMetres : config.LengthOverDraft[1];
        return config.CrossFlowDrag + config.CrossFlowDragPerLengthOverDraft * Math.Clamp(slenderness, config.LengthOverDraft[0], config.LengthOverDraft[1]);
    }

    /// <summary>
    /// Added mass along and across the hull as shares of its displacement: an ellipsoid of the hull's half length, half
    /// beam and draft, mirrored in the surface as the water's surface holds still against a quick motion (Lamb 1932,
    /// §114 and §373): k = α / (2 − α), α = abc ∫ dλ / ((a² + λ) Δ). A slender hull drags little water along its length
    /// and about its own mass across it; a flat raft little either way.
    /// </summary>
    public (double Surge, double Sway) AddedMass(DrivenHull hull)
    {
        var next = new Shape(hull.LengthMetres / 2, hull.BeamMetres / 2, Math.Max(hull.DraftMetres, 0.01));
        if (next == shape) return added;
        shape = next;
        return added = (Coefficient(next.A, next.B, next.C), Coefficient(next.B, next.A, next.C));
    }

    private static double Coefficient(double along, double other, double down)
    {
        var alpha = Ellipsoid(along, other, down);
        return alpha / (2 - alpha);
    }

    /// <summary>abc ∫₀^∞ dλ / ((a² + λ) √((a² + λ)(b² + λ)(c² + λ))), over λ = s² t / (1 − t) by the midpoint rule.</summary>
    private static double Ellipsoid(double a, double b, double c)
    {
        const int steps = 4000;
        var scale = Math.Max(a, Math.Max(b, c));
        scale *= scale;
        var sum = 0.0;
        for (var i = 0; i < steps; i++)
        {
            var t = (i + 0.5) / steps;
            var lambda = scale * t / (1 - t);
            var dLambda = scale / ((1 - t) * (1 - t));
            sum += dLambda / ((a * a + lambda) * Math.Sqrt((a * a + lambda) * (b * b + lambda) * (c * c + lambda)));
        }
        return a * b * c * sum / steps;
    }

    private readonly record struct Shape(double A, double B, double C);
}

/// <summary>What the waves meet of a hull: its size, its draft and depth now, and its whole mass aboard.</summary>
public readonly record struct DrivenHull(double LengthMetres, double BeamMetres, double DraftMetres, double DepthMetres, double MassKg);

public class DriveConfig
{
    public double FrictionCoefficient;
    public double FrictionLogOffset;
    public double KinematicViscosity;
    public double MinReynolds;
    public double CrossFlowDrag;
    public double CrossFlowDragPerLengthOverDraft;
    public double[] LengthOverDraft = [];
}
