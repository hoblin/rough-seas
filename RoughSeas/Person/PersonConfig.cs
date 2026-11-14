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
    public double AtmospherePascals;
    public double DragAreaAlong;
    public double DragAreaAcross;
    public double DragAreaSwimming;
    public SwimmingConfig Swimming = new();
    public LegsConfig Legs = new();
}

public class LegsConfig
{
    public double JumpMetres;
    public double PushMetres;
}

public class SwimmingConfig
{
    public double UsefulWatts;
    public double WastedWatts;
    public double MetresPerSecond;
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
    public string ArmourMaterial = "";
    public Dictionary<string, double> Densities = [];
    public Dictionary<string, string> Alike = [];
    public string Handle = "";
    public Dictionary<string, ToolConfig> Tools = [];
}

public class ToolConfig
{
    public PartConfig Head = new();
    public PartConfig? Haft;
}

public class PartConfig
{
    public double Kg;
    public string Of = "";
}

public class BreakingConfig
{
    public double Aeration;
    public double AerationAfterPeriod;
    public double PlumeDepthPerHeight;
    public double RollerSpeedRatio;
    public double RollerSeconds;
}
