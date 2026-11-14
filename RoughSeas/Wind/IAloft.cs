using System;
using System.Collections.Generic;

namespace RoughSeas.Wind;

public interface IAloft
{
    bool Rolled { get; }

    IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air, double seconds);
}
