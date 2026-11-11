using System.Collections.Generic;

namespace RoughSeas.Person;

public class PersonConfig
{
    public BodyConfig Body = new();
    public CarriedConfig Carried = new();
    public BreakingConfig Breaking = new();
}

public class BodyConfig
{
    public double HeightMetres;
    public double MassKg;
    public double AgeYears;
    public double FatShare;
    public double[] Frc = [];
    public BodySegmentConfig[] Segments = [];
    public double FatDensity;
    public double LeanDensity;
    public double TidalLitres;
    public double BreathSeconds;
    public double AtmospherePascals;
    public double DragAreaAlong;
    public double DragAreaAcross;
}

public class BodySegmentConfig
{
    public string Name = "";
    public double From;
    public double To;
    public double MassShare;
    public double Density;
    public bool Lungs;
}

public class CarriedConfig
{
    public double ArmourKgPerWalkSpeed;
    public double DensityKgPerLitre;
    public Dictionary<string, double> ToolKg = [];
}

public class BreakingConfig
{
    public double Aeration;
    public double AerationAfterPeriod;
    public double PlumeDepthPerHeight;
    public double RollerSpeedRatio;
    public double RollerSeconds;
}
