using System;

namespace RoughSeas.Storms;

public class StormsConfig
{
    public double TickSeconds = 0.5;
    public WavesConfig Waves = new();
    public BilgeConfig Bilge = new();
    public SwampingConfig Swamping = new();
    public MooringConfig Mooring = new();
    public AshoreConfig Ashore = new();
    public LightningConfig Lightning = new();
    public SurfaceConfig Surface = new();
}

public class SurfaceConfig
{
    public SurfaceTrainConfig[] Trains = [];
    public double Steepness;
    public double MinLengthBlocks;
    public double SettleSeconds;
    public double TurnDegrees;
    public double Stretch;
    public double AimSeconds;
    public WhitecapsConfig[] Whitecaps = [];
}

public class SurfaceTrainConfig
{
    public double AngleDegrees;
    public double LengthFactor;
    public double Energy;
}

public class WhitecapsConfig
{
    public int Force;
    public double Caps;
    public double Streaks;
    public double Spray;
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
    public double CrestRatio;
    public double BreakingCrestRatio;
    public double AirDensity;
    public double WindageDragCoefficient;
    public double WaveDriftCoefficient;
    public int MaxDepthBlocks;
}

public class BilgeConfig
{
    public double RainMillimetresPerHour;
}

public class SwampingConfig
{
    public double SpeedExponent;
    public double FreeSurfaceRoll;
    public double FreeSurfaceCapsize;

    /// <summary>The share of her speed a vessel of this dry mass, in kilograms, keeps with this much water aboard.</summary>
    public double SpeedFactor(double dryMass, double litres) => Math.Pow(dryMass / (dryMass + litres), SpeedExponent);
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
