using System;
using System.Collections.Generic;

namespace RoughSeas.Storms;

public class StormsConfig
{
    public double TickSeconds = 0.5;
    public WavesConfig Waves = new();
    public BilgeConfig Bilge = new();
    public SwampingConfig Swamping = new();
    public ResponseConfig Response = new();
    public DriveConfig Drive = new();
    public MooringConfig Mooring = new();
    public AshoreConfig Ashore = new();
    public LightningConfig Lightning = new();
    public SurfaceConfig Surface = new();
    public EyeConfig Eye = new();
    public SwimConfig Swim = new();
    public BodyConfig Body = new();
}

public class SwimConfig
{
    public double LeastShare;
    public double StrokeLift;
    public double JumpLiftPerTick;
    public double JumpLiftAboveShare;
}

public class BodyConfig
{
    public string Build = "";
    public Dictionary<string, BodyBuildConfig> Builds = [];
    public double FatDensity;
    public double LeanDensity;
    public double TidalLitres;
    public double BreathSeconds;
    public double AddedMass;
    public double DragArea;
    public double SeawaterDensity;
    public double FreshwaterDensity;
    public double ArmourDensity;
    public BreakingConfig Breaking = new();

    public BodyBuildConfig BuildOf => Builds[Build];
}

public class BodyBuildConfig
{
    public double HeightMetres;
    public double MassKg;
    public double AgeYears;
    public double FatShare;
    public double[] Frc = [];
    public BodySegmentConfig[] Segments = [];
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

public class BreakingConfig
{
    public double Aeration;
    public double AerationAfterPeriod;
    public double PlumeDepthPerHeight;
    public double RollerSpeedRatio;
    public double RollerSeconds;
}

public class EyeConfig
{
    public double ShiftPerBlock;
    public double ShiftAboveBlocks;
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
    public double FullFoamMetres;
    public bool SurfFoam;
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
    public double WhitecapCoefficient;
    public double WhitecapExponent;
    public double BreakingDepthRatio;
    public double WaterDensity;
    public double BreakingSteepness;
    public double PlungingShareAtSea;
    public double[] PlungingIribarren = [];
    public double[] PlungeDistance = [];
    public int BedSlopeBlocks;
    public double SlamCoefficient;
    public double PlungingSpeedRatio;
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
    public bool Capsizes;

    /// <summary>The factor on the speed she makes at this reference mass that she makes at this mass, both in kilograms.</summary>
    public double SpeedFactor(double referenceKg, double kg) => Math.Pow(referenceKg / kg, SpeedExponent);
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
}

public class LightningConfig
{
    public double StrikesPerMinute;
    public double MinPresence;
    public double MinCelsius;
    public double RangeBlocks;
    public double AttractBlocksPerMastMetre;
    public double TallestMastMetres;
}
