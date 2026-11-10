namespace RoughSeas.Shore;

public class ShoreConfig
{
    public RunUpConfig RunUp = new();
    public SheetConfig Sheet = new();
    public BedConfig[] Beds = [];
    public BandConfig Band = new();
    public FacesConfig Faces = new();
}

public class FacesConfig
{
    public double ReflectionFactor = 0.167;
    public double ReflectionRoughnessRate = 3.2;
    public double ReflectionShape = 1.49;
    public double ReflectionShapeRoughness = 0.38;
    public double ReflectionShapeBase = 0.86;
    public double ToeDepthWaveHeights = 1.5;
    public double SmoothingWavelengths = 0.25;
    public int SmoothingMaxBlocks = 16;
    public int BedRampBlocks = 2;
    public int ReachBlocks = 96;
    public double TaperFrom = 0.75;
    public double ReflectedSlopeLimit = 0.577;
    public double ImpulsiveFactor = 0.011;
    public double ImpulsiveFreeboard = 2.2;
    public double ImpulsiveBreak = 1.35;
    public double ImpulsivePowerFactor = 0.0014;
    public double ImpulsivePower = 3;
    public double JetSpeedRatio = 2.14;
    public double FoamDecaySeconds = 3.1;
    public double SmallestParcelSide = 0.15;
    public double LargestParcelSide = 1;
    public int ParcelsPerMetre = 16;
    public int RadiusBlocks = 64;
    public int TickMilliseconds = 100;
    public double SprayScatterMetresPerSecond = 0.5;
    public double SprayLandingSeconds = 0.2;
    public double SprayFromFaceBlocks = 0.05;
}

public class RunUpConfig
{
    public double StockdonFactor = 1.1;
    public double SetupSlopeFactor = 0.35;
    public double IncidentSwashFactor = 0.563;
    public double InfragravitySwash = 0.004;
    public double DissipativeIribarren = 0.3;
    public double DissipativeFactor = 0.043;
    public double SandyMaxSlope = 0.143;
    public double EurotopFactor = 1.65;
    public double EurotopCeiling = 4.0;
    public double EurotopCeilingRoot = 1.5;
    public double SpectralPeriodRatio = 1.1;
    public double WallSlope = 1.0;
    public int SlopeSearchBlocks = 48;
}

public class SheetConfig
{
    public double UprushFriction = 0.007;
    public double LensDepthRatio = 27;
}

public class BedConfig
{
    public string Material = "stone";
    public double Conductivity;
    public double Roughness = 1;
    public bool Sandy;
}

public class BandConfig
{
    public int HaloBlocks = 96;
    public int HighestRiseBlocks = 16;
    public double RefreshMinutes = 10;
    public double TickBudgetMilliseconds = 6;
}
