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
    public double ClothFriction;
    public float LogSeconds;
    public double JumpMetresPerSecond;
    public Dictionary<string, SailConfig> Sails = new();
    public CrewConfig Crew = new();
    public LineConfig Line = new();
}

public class CrewConfig
{
    public double WalkMetresPerSecond;
    public double HaulMetresPerSecond;
}

public class SailConfig
{
    public double BreakdownDegrees;
    public double SeparatedDegrees;
}

public class LineConfig
{
    public string Material = "";
    public double MeasuredMetres;
}
