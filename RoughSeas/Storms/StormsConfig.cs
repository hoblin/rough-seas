namespace RoughSeas.Storms;

public class StormsConfig
{
    public double TickSeconds = 0.5;
    public WavesConfig Waves = new();
    public BilgeConfig Bilge = new();
    public MooringConfig Mooring = new();
    public AshoreConfig Ashore = new();
    public LightningConfig Lightning = new();
}

public class WavesConfig
{
    public double MinHeight;
    public double PeriodSecondsPerRootMetre;
    public double WhitecapCoefficient;
    public double WhitecapExponent;
    public double BreakingDepthRatio;
    public double WaterDensity;
    public double BreakerTransfer;
    public double ShippedWaterFraction;
    public double AirDensity;
    public double WindageDragCoefficient;
    public double WaveDriftCoefficient;
    public int MaxDepthBlocks;
}

public class BilgeConfig
{
    public double RainMillimetresPerHour;
}

public class MooringConfig
{
    public double AnchoredExposure;
    public string StakeRope = "";
    public double StakeReachMetres;
    public double AnchorFullScope;
    public double AnchorDragMetres;
}

public class AshoreConfig
{
    public double MinHours;
    public double RollDelaySeconds;
    public int MaxSamples;
    public double HazardPerSquareMetreHour;
    public double SurfReachPerMetre;
    public int ShoreSearchBlocks;
    public int FineSearchBlocks;
    public int CoarseSearchStride;
    public double ImpactFactor;
}

public class LightningConfig
{
    public double CheckSeconds;
    public double StrikesPerMinute;
    public double MinPresence;
    public double MinCelsius;
    public double RangeBlocks;
    public double AttractBlocksPerMastMetre;
    public double TallestMastMetres;
}
