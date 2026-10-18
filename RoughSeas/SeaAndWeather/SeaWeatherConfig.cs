using System;

namespace RoughSeas.SeaAndWeather;

public class SeaWeatherConfig
{
    public BeaufortForce[] Beaufort = [];
    public double[] SeaBuildHours = [0];
    public PrevailingWind[] PrevailingWinds = [];
    public BackgroundWindConfig Background = new();
    public ShowersConfig Showers = new();
    public StormTypeConfig[] Storms = [];
    public StormShapeConfig Storm = new();
    public VanillaConfig Vanilla = new();
    public CurrentsConfig Currents = new();
    public OceanConfig Ocean = new();
    public WavesConfig Waves = new();
    public SeaMapConfig Map = new();
}

public class OceanConfig
{
    public int DefaultSeaFrom;
    public int CalibrationSamples;
}

public class WavesConfig
{
    public double RealPolarEquatorMetres;
    public double KneeMetres;
    public double CeilingMetres;
    public int FetchStepBlocks;
    public int OpenSeaStepBlocks;
    public int MaxFetchBlocks;
    public int FetchDirections;
    public double FetchCacheHours;
    public double OpenSeaDepthMetres;
    public int SoundingBlocks;
}

public class SeaMapConfig
{
    public int CellBlocks;
    public double UpdateSeconds;
    public double StaleHours;
    public double TickBudgetMilliseconds;
}

public class CurrentsConfig
{
    public double GyreDegrees;
    public double BoundaryFraction;
    public double WesternBoundaryMps;
    public double InteriorMps;
    public double WesternBoundaryCelsius;
    public double EasternBoundaryCelsius;
    public double SeawaterFreezingCelsius;
    public int BasinReachBlocks;
    public double StokesDriftPerMps;
}

public class BeaufortForce
{
    public int Force;
    public string Name = "";
    public double MinMps;
}

public class PrevailingWind
{
    public double MaxLatitude;
    public double NorthFromDegrees;
    public double SouthFromDegrees;
}

public class BackgroundWindConfig
{
    public double LandMeanMps;
    public double SeaMeanMps;
    public double SeaRampBlocks;
    public double SpreadFraction;
    public double SpatialScale;
    public double TimeScaleHours;
    public double VeerDegrees;
}

public class ShowersConfig
{
    public double SpatialScale;
    public double TimeScaleHours;
    public double DryThreshold;
    public double WetThreshold;
    public double Span;
    public double WettestRain;
}

public class StormTypeConfig
{
    public string Code = "";
    public double CellKm;
    public double SlotHours;
    public double BirthsPerMillionKm2PerDay;
    public Window? Latitude;
    public Window? Temperature;
    public double RainfallExponent;
    public double[] MonthlyWeight = [];
    public ValueRange RadiusKm = new();
    public Spread PeakMps = new();
    public GradientStrength? StrengthByGradient;
    public ValueRange LifetimeHours = new();
    public ValueRange SpeedKmPerHour = new();
    public bool Cyclonic;
    public double EyeFraction;
    public double RimFraction;
    public double Rain;
    public double OverLand;
    public string? WeatherEvent;
}

public class StormShapeConfig
{
    public double GradientSpanKm;
    public double RiseFraction;
    public double FallFraction;
    public double TrackSpreadDegrees;
    public double InflowDegrees;
    public double WeatherEventThreshold;
}

public class VanillaConfig
{
    public double UnitsPerMps;
    public double RegionRefreshHours;
    public double WindLeaseHours;
    public double WindResendStrength;
    public double CloudSpeedPerMps;
    public double StormCloudBrightness;
}

public class ValueRange
{
    public double Min;
    public double Max;

    public double Lerp(double t) => Min + (Max - Min) * t;
}

public class GradientStrength
{
    public double ReferenceCelsiusPer1000Km;
    public double Exponent;
}

public class Spread
{
    public double Mean;
    public double Deviation;
    public double Min;
    public double Max;

    public double Draw(double first, double second) =>
        Math.Clamp(Mean + Deviation * Math.Sqrt(-2 * Math.Log(1 - first)) * Math.Cos(2 * Math.PI * second), Min, Max);
}

public class Window
{
    public double Min;
    public double Optimum;
    public double Max;

    public double Weight(double value)
    {
        if (value <= Min || value >= Max) return 0;
        return value < Optimum
            ? (value - Min) / (Optimum - Min)
            : (Max - value) / (Max - Optimum);
    }
}
