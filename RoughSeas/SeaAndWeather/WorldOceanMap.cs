using System;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace RoughSeas.SeaAndWeather;

public interface IOceanMap
{
    int At(int pixelX, int pixelZ);
}

public class WorldOceanMap(ICoreServerAPI sapi, IOceanMap unbuilt) : IOceanMap
{
    public static readonly int PixelSize = TerraGenConfig.oceanMapScale;

    public int At(int pixelX, int pixelZ)
    {
        var perRegion = sapi.WorldManager.RegionSize / PixelSize;
        var regionX = (int)Math.Floor((double)pixelX / perRegion);
        var regionZ = (int)Math.Floor((double)pixelZ / perRegion);
        var map = sapi.WorldManager.GetMapRegion(regionX, regionZ)?.OceanMap;
        return map is { Data.Length: > 0 } && map.InnerSize == perRegion
            ? map.GetUnpaddedInt(pixelX - regionX * perRegion, pixelZ - regionZ * perRegion)
            : unbuilt.At(pixelX, pixelZ);
    }
}
