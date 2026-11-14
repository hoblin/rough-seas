using System;

namespace RoughSeas.Wind;

public abstract class WindSurface
{
    public abstract double HeightMetres { get; }

    public abstract Push[] Pushes(Airflow air, double halfDensity, double seconds);
}

public sealed class BluffSurface(double sideSquareMetres, double frontSquareMetres, double drag, double centreAlong, double centreAcross, double heightMetres)
    : WindSurface
{
    public override double HeightMetres => heightMetres;

    public override Push[] Pushes(Airflow air, double halfDensity, double seconds) =>
    [
        new(new HullPoint(centreAlong, heightMetres, centreAcross), new HullPoint(
            halfDensity * drag * frontSquareMetres * air.Along * Math.Abs(air.Along), 0,
            halfDensity * drag * sideSquareMetres * air.Across * Math.Abs(air.Across)))
    ];
}
