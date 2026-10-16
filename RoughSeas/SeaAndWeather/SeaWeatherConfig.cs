namespace RoughSeas.SeaAndWeather;

public class SeaWeatherConfig
{
    public BeaufortForce[] Beaufort = [];
    public double[] SeaBuildHours = [0];
    public PrevailingWind[] PrevailingWinds = [];
    public BackgroundWindConfig Background = new();
    public StormTypeConfig[] Storms = [];
    public StormShapeConfig Storm = new();
    public VanillaConfig Vanilla = new();
    public CurrentsConfig Currents = new();
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
    public double LandBelowOceanity;
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
    public double MeanMps;
    public double SpreadMps;
    public double SpatialScale;
    public double TimeScaleHours;
    public double VeerDegrees;
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
    public double CloudSpeedPerMps;
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
