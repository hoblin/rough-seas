using System.Collections.Generic;

namespace RoughSeas.Wind;

public class WindConfig
{
    public double AirKgPerCubicMetre;
    public double ReferenceHeightMetres;
    public double GradientExponent;
    public double BluffDrag;
    public double Camber;
    public double ClothKgPerSquareMetre;
    public float LogSeconds;
    public Dictionary<string, SailConfig> Sails = new();
}

public class SailConfig
{
    public double[][] Headings = [];
    public double[][] LiftAndDrag = [];
}
