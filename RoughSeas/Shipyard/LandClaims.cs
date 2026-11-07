using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RoughSeas.Shipyard;

public static class LandClaims
{
    /// <summary>Whether <paramref name="byEntity"/> may break every one of <paramref name="blocks"/>: vanilla's test that has no side effects, alike on both sides.</summary>
    public static bool MayBreak(EntityAgent byEntity, IEnumerable<BlockPos> blocks)
    {
        var player = (byEntity as EntityPlayer)?.Player;
        return blocks.All(at => byEntity.World.Claims.TestAccess(player, at, EnumBlockAccessFlags.BuildOrBreak) == EnumWorldAccessResponse.Granted);
    }
}
