using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Soundscape;

public readonly record struct Listener(Vec3d Pos, bool AtSea, SeaState Sea, double WaveHeight, Foam Foam)
{
    public static Listener? Of(ICoreClientAPI capi, SeaWeatherSystem sea, StormsModSystem storms, int probeBlocks)
    {
        if (capi.World.Player?.Entity?.Pos is not { } pos) return null;

        var state = sea.At(pos.X, pos.Z);
        var surface = storms.Surface;
        return new Listener(pos.XYZ, OverWater(capi.World.BlockAccessor, pos.AsBlockPos, probeBlocks), state,
            surface?.Level ?? state.WaveHeight, surface?.Foam ?? default);
    }

    private static bool OverWater(IBlockAccessor accessor, BlockPos pos, int probeBlocks)
    {
        for (var i = 0; i <= probeBlocks; i++, pos.Down())
        {
            if (accessor.GetBlock(pos, BlockLayersAccess.Fluid).IsLiquid()) return true;
            if (accessor.GetBlock(pos, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
        }
        return false;
    }
}
