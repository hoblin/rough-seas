using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace AgentBridge;

public class ChunkSettle(ICoreServerAPI sapi, ILogger logger)
{
    private const int RadiusChunks = 6;
    private const int CheckMilliseconds = 500;
    private const double GiveUpSeconds = 180;

    private int flights;

    public int Watch(Vec3d position)
    {
        var flight = ++flights;
        var centreX = (int)position.X / GlobalConstants.ChunkSize;
        var centreZ = (int)position.Z / GlobalConstants.ChunkSize;
        var chunkY = sapi.World.SeaLevel / GlobalConstants.ChunkSize;
        var around = Around().ToArray();
        var total = around.Length;
        var watch = Stopwatch.StartNew();
        long listener = 0;
        listener = sapi.Event.RegisterGameTickListener(_ =>
        {
            var loaded = around.Count(offset => sapi.WorldManager.GetChunk(centreX + offset.X, chunkY, centreZ + offset.Z) != null);
            if (loaded == total)
            {
                logger.Debug("[bridge] flight {0}: chunks loaded, {1} columns within {2} chunks, after {3:F1} s", flight, total, RadiusChunks, watch.Elapsed.TotalSeconds);
            }
            else if (watch.Elapsed.TotalSeconds > GiveUpSeconds)
            {
                logger.Debug("[bridge] flight {0}: chunks still loading after {1:F0} s, {2} of {3} columns", flight, GiveUpSeconds, loaded, total);
            }
            else return;
            sapi.Event.UnregisterGameTickListener(listener);
        }, CheckMilliseconds);
        return flight;
    }

    private static IEnumerable<(int X, int Z)> Around()
    {
        for (var dx = -RadiusChunks; dx <= RadiusChunks; dx++)
        for (var dz = -RadiusChunks; dz <= RadiusChunks; dz++)
        {
            if (dx * dx + dz * dz <= RadiusChunks * RadiusChunks) yield return (dx, dz);
        }
    }
}
