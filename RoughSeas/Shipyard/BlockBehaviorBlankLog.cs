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

    private string vessel = "";

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        vessel = properties["vessel"].AsString("");
    }

    public bool TryLay(EntityAgent byEntity, BlockPos pos, IReadOnlyCollection<BlockPos> fire)
    {
        if (Row(byEntity, pos, fire) is not { } row) return false;

        if (byEntity.World.Side == EnumAppSide.Server) Lay(byEntity, row, fire);
        return true;
    }

    public static LyingLog? LogOf(Block block) =>
        block.Variant["wood"] is { } species
            ? block.Variant["rotation"] switch
            {
                "we" => new LyingLog(new Wood(block.Code.Domain, species), EnumAxis.X),
                "ns" => new LyingLog(new Wood(block.Code.Domain, species), EnumAxis.Z),
                _ => null
            }
            : null;

    private static LogRow? Row(EntityAgent byEntity, BlockPos pos, IReadOnlyCollection<BlockPos> fire)
    {
        var world = byEntity.World;
        var accessor = world.BlockAccessor;
        var row = LogRow.Find(pos,
            at => accessor.GetBlock(at) is var log && log.HasBehavior<BlockBehaviorBlankLog>() ? LogOf(log) : null,
            at => accessor.GetBlock(at, BlockLayersAccess.Fluid) is { } fluid && fluid.IsLiquid() && fluid.LiquidCode?.Contains("water") == true);
        var player = (byEntity as EntityPlayer)?.Player;
        return row?.Blocks.Concat(fire).All(at => world.Claims.TestAccess(player, at, EnumBlockAccessFlags.BuildOrBreak) == EnumWorldAccessResponse.Granted) == true ? row : null;
    }

    private void Lay(EntityAgent byEntity, LogRow row, IReadOnlyCollection<BlockPos> fire)
    {
        var world = byEntity.World;
        var shipyard = world.Api.ModLoader.GetModSystem<ShipyardModSystem>();
        var type = world.GetEntityType(new AssetLocation($"{vessel}-{row.Wood.Code}"));
        if (type == null)
        {
            shipyard.Log("no {0} of {1} to lay from logs", vessel, row.Wood.Code);
            return;
        }

        foreach (var pos in fire.Concat(row.Blocks)) world.BlockAccessor.SetBlock(0, pos);
        var blank = world.ClassRegistry.CreateEntity(type);
        blank.Pos.SetPos(row.Centre.ToVec3d().Add(0.5, 0, 0.5));
        blank.Pos.Yaw = row.Along == EnumAxis.X ? GameMath.PIHALF : 0;
        EntityBehaviorBurnout.Begin(blank, world.Api.ModLoader.GetModSystem<SeaWeatherSystem>().Clock.Hours);
        world.SpawnEntity(blank);
        shipyard.Log(blank, "laid by {0} from {1} {2} logs, water to the {3}: the charcoal pit of {4} blocks on the middle log takes the row", byEntity.GetName(),
            LogRow.Length, row.Wood.Code, row.Water.Code, fire.Count);
    }
}
