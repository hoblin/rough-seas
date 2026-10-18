namespace RoughSeas.SeaAndWeather;

public class SeaWeatherConfig
{
    public BeaufortForce[] Beaufort = [];
    public double[] SeaBuildHours = [0];
    public double SeaBuildShare;
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
}

public class SeaMapConfig
{
    public int CellBlocks;
    public double UpdateSeconds;
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
    public double WaveHeight;
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
    public int CellSize;
    public double SlotHours;
    public double Chance;
    public TemperatureWindow Temperature = new();
    public double RainfallExponent;
    public double[] MonthlyWeight = [];
    public ValueRange Radius = new();
    public ValueRange PeakMps = new();
    public ValueRange LifetimeHours = new();
    public ValueRange SpeedBlocksPerHour = new();
    public bool Cyclonic;
    public double EyeFraction;
    public double Rain;
    public double OverLand;
    public string? WeatherEvent;
}

public class StormShapeConfig
{
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

public class TemperatureWindow
{
    public double Min;
    public double Optimum;
    public double Max;

    public double Weight(double celsius)
    {
        if (celsius <= Min || celsius >= Max) return 0;
        return celsius < Optimum
            ? (celsius - Min) / (Optimum - Min)
            : (Max - celsius) / (Max - Optimum);
    }
}
