using RoughSeas.Propulsion;

namespace RoughSeas.Person;

/// <summary>A swimmer's arms: a blade slipping back through the water on a fixed power, sized by the stroke measured in a pool.</summary>
public sealed class SwimmingArms(SwimmingConfig config, double poolDensity)
{
    private readonly double watts = config.UsefulWatts + config.WastedWatts;
    private readonly double dragArea = BladeDragArea(config, poolDensity);
    private readonly StrokeConfig blade = new() { DriveShare = 1, DragCoefficient = 1 };
    private BladeStroke? stroke;
    private double strokeDensity;

    public double Thrust(double metresPerSecond, double density)
    {
        if (stroke == null || strokeDensity != density) (stroke, strokeDensity) = (new BladeStroke("swimming", blade, dragArea, density), density);
        return stroke.Thrust(metresPerSecond, watts);
    }

    private static double BladeDragArea(SwimmingConfig config, double density)
    {
        var (useful, wasted, speed) = (config.UsefulWatts, config.WastedWatts, config.MetresPerSecond);
        var thrust = useful / speed;
        var slip = speed * wasted / useful;
        return 2 * thrust / (density * slip * slip);
    }
}
