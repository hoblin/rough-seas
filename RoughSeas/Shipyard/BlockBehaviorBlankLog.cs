using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Shipyard;

public class BlockBehaviorBlankLog(Block block) : BlockBehavior(block), IIgnitable
{
    public const string Code = "roughseas:blanklog";

    private const float IgniteSeconds = 3;

    private string vessel = "";

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        vessel = properties["vessel"].AsString("");
    }

    public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
    {
        if (Row(byEntity.World, pos) == null) return EnumIgniteState.NotIgnitable;

        return secondsIgniting > IgniteSeconds ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
    }

    public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
    {
        if (Row(byEntity.World, pos) is not { } row) return;

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Side == EnumAppSide.Server) Lay(byEntity, row);
    }

    public EnumIgniteState OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting) => EnumIgniteState.NotIgnitable;

    public static LyingLog? LogOf(Block block) =>
        block.Variant["wood"] is { } species
            ? block.Variant["rotation"] switch
            {
                "we" => new LyingLog(new Wood(block.Code.Domain, species), EnumAxis.X),
                "ns" => new LyingLog(new Wood(block.Code.Domain, species), EnumAxis.Z),
                _ => null
            }
            : null;

    private LogRow? Row(IWorldAccessor world, BlockPos pos)
    {
        var accessor = world.BlockAccessor;
        return LogRow.Find(pos,
            at => accessor.GetBlock(at) is var log && log.HasBehavior<BlockBehaviorBlankLog>() ? LogOf(log) : null,
            at => accessor.GetBlock(at, BlockLayersAccess.Fluid) is { } fluid && fluid.IsLiquid() && fluid.LiquidCode?.Contains("water") == true);
    }

    private void Lay(EntityAgent byEntity, LogRow row)
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
        shipyard.Log(blank, "laid by {0} from five {1} logs, water to the {2}: the fire is lit", byEntity.GetName(), row.Wood.Code, row.Water.Code);
    }
}
