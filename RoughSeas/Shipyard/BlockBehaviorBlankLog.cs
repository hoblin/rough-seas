using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Shipyard;

public class BlockBehaviorBlankLog(Block block) : BlockBehavior(block)
{
    public const string Code = "roughseas:blanklog";

    private const int Logs = 5;

    private string vessel = "";

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        vessel = properties["vessel"].AsString("");
    }

    public bool TryLay(EntityAgent byEntity, BlockPos pos)
    {
        if (Row(byEntity, pos) is not var (row, water)) return false;

        if (byEntity.World.Side == EnumAppSide.Server) Lay(byEntity, row, water);
        return true;
    }

    private static (LogRow, BlockFacing)? Row(EntityAgent byEntity, BlockPos pos)
    {
        var world = byEntity.World;
        var accessor = world.BlockAccessor;
        var row = LogRow.Find(pos, Logs, at => accessor.GetBlock(at) is var log && log.HasBehavior<BlockBehaviorBlankLog>() ? LyingLog.Of(log) : null);
        var water = row?.Water(at => Waterside.IsWater(accessor, at));
        return water != null && LandClaims.MayBreak(byEntity, row!.Blocks) ? (row, water) : null;
    }

    private void Lay(EntityAgent byEntity, LogRow row, BlockFacing water)
    {
        var world = byEntity.World;
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        var type = world.GetEntityType(new AssetLocation($"{vessel}-{row.Wood.Code}"));
        if (type == null)
        {
            shipyard.Log("no {0} of {1} to lay from logs", vessel, row.Wood.Code);
            return;
        }

        foreach (var pos in row.Blocks) world.BlockAccessor.SetBlock(0, pos);
        var blank = world.ClassRegistry.CreateEntity(type);
        blank.Pos.SetPos(row.Centre.ToVec3d().Add(0.5, 0, 0.5));
        blank.Pos.Yaw = row.Along == EnumAxis.X ? GameMath.PIHALF : 0;
        EntityBehaviorBurnout.Begin(blank, world.Api.ModLoader.GetModSystem<SeaWeatherSystem>().Clock.Hours);
        world.SpawnEntity(blank);
        shipyard.Log(blank, "laid by {0} from {1} {2} logs, water to the {3}: dry grass on the middle log, the base of her first fire", byEntity.GetName(),
            row.Length, row.Wood.Code, water.Code);
    }
}
