using System;

namespace RoughSeas.Storms;

/// <summary>
/// How a hull of its own mass answers the drawn sea: one per hull, fed each frame with what
/// <see cref="IDrawnSea.PoseOf"/> gives it. A hull with periods of 0 follows the water as it lies.
/// </summary>
/// <example><code>
/// var response = HullResponse.Of(length, beam, draft, depth, storms.Config.Response);
/// var pose = response.Follow(storms.DrawnSea.PoseOf(x, z, yaw, length, beam), frameSeconds);
/// </code></example>
public sealed class HullResponse(double heavePeriodSeconds, double rollPeriodSeconds, ResponseConfig config)
{
    private const double Gravity = 9.81;
    private const double MaxStepShare = 0.05;
    private const int MaxSteps = 20;
    private const double MinMetacentricHeight = 0.02;

    private Mode lift, roll, pitch;

    public double HeavePeriodSeconds { get; set; } = heavePeriodSeconds;

    public double RollPeriodSeconds { get; set; } = rollPeriodSeconds;

    /// <summary>The response of a hull of these dimensions in metres, floating at this draft.</summary>
    public static HullResponse Of(double lengthMetres, double beamMetres, double draftMetres, double depthMetres, ResponseConfig config) =>
        new(HeavePeriod(draftMetres, config), RollPeriod(lengthMetres, beamMetres, draftMetres, depthMetres), config);

    /// <summary>Seconds: 2π √((M + A₃₃) / (ρ g A_w)), which for a prismatic hull is a function of its draft alone.</summary>
    public static double HeavePeriod(double draftMetres, ResponseConfig config) =>
        2 * Math.PI * Math.Sqrt((1 + config.HeaveAddedMass) * Math.Max(draftMetres, 0) / Gravity);

    /// <summary>
    /// Seconds, by 46 CFR 28.575: 2·C·B / √GM, with GM of a box hull whose centre of gravity sits at half its depth.
    /// </summary>
    public static double RollPeriod(double lengthMetres, double beamMetres, double draftMetres, double depthMetres)
    {
        double draft = Math.Max(draftMetres, 0.01);
        double metacentricHeight = Math.Max(MinMetacentricHeight, draft / 2 + beamMetres * beamMetres / (12 * draft) - depthMetres / 2);
        double coefficient = 0.373 + 0.023 * beamMetres / draft - 0.00043 * lengthMetres;
        return 2 * coefficient * beamMetres / Math.Sqrt(metacentricHeight);
    }

    /// <summary>
    /// The pose the hull takes this frame, <paramref name="seconds"/> after the last, chasing the water's pose as a
    /// damped oscillator of its own periods. Where no swell is drawn it comes to rest at once.
    /// </summary>
    public SeaPose Follow(SeaPose pose, double seconds)
    {
        if (pose.Hold <= 0 || seconds <= 0)
        {
            if (pose.Hold <= 0) (lift, roll, pitch) = (default, default, default);
            return pose with { Lift = lift.At, Roll = roll.At, Pitch = pitch.At };
        }

        double shortest = Math.Min(HeavePeriodSeconds, RollPeriodSeconds);
        int steps = shortest > 0 ? Math.Clamp((int)Math.Ceiling(seconds / (shortest * MaxStepShare)), 1, MaxSteps) : 1;
        double step = seconds / steps;
        for (int i = 0; i < steps; i++)
        {
            lift = lift.Toward(pose.Lift, HeavePeriodSeconds, config.HeaveDamping, step);
            roll = roll.Toward(pose.Roll, RollPeriodSeconds, config.RollDamping, step);
            pitch = pitch.Toward(pose.Pitch, HeavePeriodSeconds, config.HeaveDamping, step);
        }
        return pose with { Lift = lift.At, Roll = roll.At, Pitch = pitch.At };
    }

    private readonly record struct Mode(double At, double Speed)
    {
        public Mode Toward(double target, double periodSeconds, double damping, double seconds)
        {
            if (periodSeconds <= 0) return new Mode(target, 0);

            double frequency = 2 * Math.PI / periodSeconds;
            double speed = Speed + (frequency * frequency * (target - At) - 2 * damping * frequency * Speed) * seconds;
            return new Mode(At + speed * seconds, speed);
        }
    }
}

public class ResponseConfig
{
    public double HeaveAddedMass;
    public double HeaveDamping;
    public double RollDamping;
}
