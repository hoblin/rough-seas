using System;

namespace RoughSeas.Shipyard;

public readonly record struct Soaking(double Hours, bool Soaked, double Through)
{
    public Soaking After(double hours, bool inWater)
    {
        double soaked = Math.Clamp(Hours + (inWater ? hours : -hours), 0, Through);
        return this with { Hours = soaked, Soaked = soaked >= Through || Soaked && soaked > 0 };
    }
}
