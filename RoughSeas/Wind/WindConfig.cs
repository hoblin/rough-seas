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
    public string Aback = "";
    public float LogSeconds;
    public double JumpMetresPerSecond;
    public Dictionary<string, SailConfig> Sails = new();
    public CrewConfig Crew = new();
}

public class CrewConfig
{
    public double WalkMetresPerSecond;
    public double HaulMetresPerSecond;
}

public class SailConfig
{
    public double[][] Headings = [];
    public double[][] LiftAndDrag = [];
    public double FrictionDrag;
}
