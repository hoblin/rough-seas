using System;

namespace RoughSeas.Storms;

public sealed class HullResponse(double heavePeriodSeconds, double rollPeriodSeconds, ResponseConfig config)
{
    private const double Gravity = 9.81;
    private const double MaxStepShare = 0.05;
    private const int MaxSteps = 20;
    private const double MinMetacentricHeight = 0.02;

    private Mode lift, roll, pitch;

    public double HeavePeriodSeconds { get; set; } = heavePeriodSeconds;

    public double RollPeriodSeconds { get; set; } = rollPeriodSeconds;

    public static HullResponse Of(double lengthMetres, double beamMetres, double draftMetres, double depthMetres, ResponseConfig config) =>
        new(HeavePeriod(draftMetres, config), RollPeriod(lengthMetres, beamMetres, draftMetres, depthMetres), config);

    public static double HeavePeriod(double draftMetres, ResponseConfig config) =>
        2 * Math.PI * Math.Sqrt((1 + config.HeaveAddedMass) * Math.Max(draftMetres, 0) / Gravity);

    public static double RollPeriod(double lengthMetres, double beamMetres, double draftMetres, double depthMetres)
    {
        double draft = Math.Max(draftMetres, 0.01);
        double metacentricHeight = Math.Max(MinMetacentricHeight, draft / 2 + beamMetres * beamMetres / (12 * draft) - depthMetres / 2);
        double coefficient = 0.373 + 0.023 * beamMetres / draft - 0.00043 * lengthMetres;
        return 2 * coefficient * beamMetres / Math.Sqrt(metacentricHeight);
    }

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
