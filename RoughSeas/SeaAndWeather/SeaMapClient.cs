using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

/// <summary>The sea map a client reads: the snapshots the server sends it, and those stored with the map regions it loads.</summary>
public class SeaMapClient(SeaWeatherSystem sea, ICoreClientAPI capi)
    : SeaMap(sea.Config, sea.Clock, () => capi.World.BlockAccessor.RegionSize)
{
    public void Start()
    {
        capi.Network.GetChannel(SeaWeatherSystem.Channel).SetMessageHandler<SeaMapSnapshot>(Receive);
        capi.Event.MapRegionLoaded += (_, region) => Load(region);
        capi.Event.MapRegionUnloaded += (coord, _) => Forget(coord.X, coord.Y);
    }

    private void Load(IMapRegion region)
    {
        var data = region.GetModdata(SeaMapSnapshot.ModdataKey);
        if (data == null) return;
        try
        {
            Receive(SerializerUtil.Deserialize<SeaMapSnapshot>(data));
        }
        catch (Exception exception)
        {
            sea.Log("unreadable sea map in a map region: {0}", exception.Message);
        }
    }
}
