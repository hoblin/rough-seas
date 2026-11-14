using RoughSeas.Water;

namespace RoughSeas.Person;

/// <summary>A swimmer's arms: a blade slipping back through the water on a fixed power, sized by the stroke measured in a pool.</summary>
public sealed class SwimmingArms(SwimmingConfig config, double poolDensity)
{
    private readonly double watts = config.UsefulWatts + config.WastedWatts;
    private readonly double inPool = NewtonsPerSlipSquared(config);

    public double Thrust(double metresPerSecond, double density) =>
        SlippingBlade.Thrust(inPool * density / poolDensity, metresPerSecond, watts);

    private static double NewtonsPerSlipSquared(SwimmingConfig config)
    {
        var (useful, wasted, speed) = (config.UsefulWatts, config.WastedWatts, config.MetresPerSecond);
        var slip = speed * wasted / useful;
        return useful / speed / (slip * slip);
    }
}
