namespace RoughSeas.SeaAndWeather;

public readonly record struct WaveGeneration(double TowardRadians, double Period)
{
    public bool Raised => Period > 0;
}
