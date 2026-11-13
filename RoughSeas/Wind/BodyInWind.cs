using System;
using System.Collections.Generic;
using RoughSeas.Vessels;

namespace RoughSeas.Wind;

public abstract class BodyInWind(WindConfig config)
{
    private const double LowestMetres = 0.1;

    protected abstract (double Along, double Across) Pivot { get; }

    protected abstract IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air);

    public WindLoad Load((double X, double Z) wind, (double X, double Z) velocity, double yaw, double waterlineMetres)
    {
        Airflow AirOn(WindSurface surface)
        {
            var share = Math.Pow(Math.Max(LowestMetres, surface.HeightMetres - waterlineMetres) / config.ReferenceHeightMetres, config.GradientExponent);
            var (along, across) = HullShape.ToHull(wind.X * share - velocity.X, wind.Z * share - velocity.Z, yaw);
            return new Airflow(along, across);
        }

        var halfDensity = config.AirKgPerCubicMetre / 2;
        var pivot = new HullPoint(Pivot.Along, 0, Pivot.Across);
        var load = default(WindLoad);
        foreach (var surface in Surfaces(AirOn))
        foreach (var push in surface.Pushes(AirOn(surface), halfDensity))
        {
            var (x, z) = HullShape.ToWorld(push.Force.Along, push.Force.Across, yaw);
            load += new WindLoad(x, z, ((push.At - pivot).Cross(push.Force) + push.Couple).Height);
        }
        return load;
    }
}
