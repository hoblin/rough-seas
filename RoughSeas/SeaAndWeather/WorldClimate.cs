using System;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// Worldgen climate at any position, loaded or not: GenMaps' climate noise, without its wobble, rebuilt from the
/// world config, so its settings must follow GenMaps.initWorldGen.
/// </summary>
public class WorldClimate
{
    private const int CellSize = 2048;
    private static readonly double BlocksPerNoiseUnit = TerraGenConfig.climateMapScale * TerraGenConfig.climateMapSubScale;

    private readonly NoiseClimate noise;
    private readonly ConcurrentDictionary<(int CellX, int CellZ), (double Celsius, double Rainfall)> cells = new();

    public WorldClimate(long seed, ITreeAttribute worldConfig, int mapSizeZ)
    {
        noise = Noise(seed, worldConfig, mapSizeZ);
    }

    public (double Celsius, double Rainfall) At(double x, double z) =>
        cells.GetOrAdd(((int)Math.Floor(x / CellSize), (int)Math.Floor(z / CellSize)), Sample);

    public double RainfallAt(double x, double z)
    {
        var cellX = x / CellSize - 0.5;
        var cellZ = z / CellSize - 0.5;
        var westX = (int)Math.Floor(cellX);
        var northZ = (int)Math.Floor(cellZ);
        double Rainfall(int dx, int dz) => cells.GetOrAdd((westX + dx, northZ + dz), static (cell, climate) => climate.Sample(cell), this).Rainfall;
        return GameMath.BiLerp(Rainfall(0, 0), Rainfall(1, 0), Rainfall(0, 1), Rainfall(1, 1), cellX - westX, cellZ - northZ);
    }

    private (double Celsius, double Rainfall) Sample((int CellX, int CellZ) cell)
    {
        int packed;
        lock (noise)
        {
            packed = noise.GetLerpedClimateAt((cell.CellX + 0.5) * CellSize / BlocksPerNoiseUnit, (cell.CellZ + 0.5) * CellSize / BlocksPerNoiseUnit);
        }
        return (
            Climate.GetScaledAdjustedTemperatureFloat((packed >> 16) & 0xff, 0),
            ((packed >> 8) & 0xff) / 255.0
        );
    }

    private static NoiseClimate Noise(long seed, ITreeAttribute worldConfig, int mapSizeZ)
    {
        NoiseClimate noise = worldConfig.GetString("worldClimate", "realistic") == "realistic"
            ? Realistic(seed, worldConfig, mapSizeZ)
            : new NoiseClimatePatchy(seed);
        noise.rainMul = worldConfig.GetString("globalPrecipitation", "1").ToFloat(1);
        noise.tempMul = worldConfig.GetString("globalTemperature", "1").ToFloat(1);
        return noise;
    }

    private static NoiseClimateRealistic Realistic(long seed, ITreeAttribute worldConfig, int mapSizeZ)
    {
        var (spawnMinTemp, spawnMaxTemp) = worldConfig.GetString("startingClimate") switch
        {
            "hot" => (28, 32),
            "warm" => (19, 23),
            "cool" => (-5, 1),
            "icy" => (-15, -10),
            _ => (6, 14)
        };
        return new NoiseClimateRealistic(
            seed,
            (double)mapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale,
            worldConfig.GetString("polarEquatorDistance", "50000").ToInt(50000),
            spawnMinTemp,
            spawnMaxTemp)
        {
            GeologicActivityStrength = worldConfig.GetString("geologicActivity").ToFloat(0.05f)
        };
    }
}
