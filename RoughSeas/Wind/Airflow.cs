using System;

namespace RoughSeas.Wind;

public readonly record struct Airflow(double Along, double Across)
{
    public double Speed => Math.Sqrt(Along * Along + Across * Across);

    public double FromBow => Speed > 0 ? Math.Acos(Math.Clamp(Along / Speed, -1, 1)) : 0;

    public int Leeward => Across < 0 ? -1 : 1;
}

public readonly record struct Push(double Along, double Across, double AtAlong, double AtAcross);

public readonly record struct WindLoad(double X, double Z, double NewtonMetres)
{
    public static WindLoad operator +(WindLoad a, WindLoad b) => new(a.X + b.X, a.Z + b.Z, a.NewtonMetres + b.NewtonMetres);
}
