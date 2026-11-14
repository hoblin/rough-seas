using System;
using System.Collections.Generic;

namespace RoughSeas.Wind;

public interface IAloft
{
    bool Set { get; }

    IEnumerable<WindSurface> Surfaces(Func<WindSurface, Airflow> air, double seconds);
}

public interface IRigged
{
    IAloft Aloft { get; }

    void Trim();

    void Trimmed(double seconds);
}
