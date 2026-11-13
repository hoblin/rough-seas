using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Cargo;

/// <summary>
/// A vessel's holds: inventories, each opened by a click on its own selection box, whose largest stacks lie on her
/// floor as vanilla's ground piles at a fraction of their size.
/// </summary>
public sealed class EntityBehaviorCargo : EntityBehavior
{
    public const string Code = "roughseas:cargo";

    private Hold[] holds = [];
    private bool dropContentsOnDeath;
    private GroundPile? piles;
    private CargoRenderer? renderer;
    private WorldInteraction[]? help;

    public EntityBehaviorCargo(Entity entity) : base(entity)
    {
    }

    public bool Empty => holds.All(hold => hold.Inventory.Empty);

    public override string PropertyName() => Code;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        holds = attributes["holds"].AsObject<HoldConfig[]>()!.Select((config, index) => new Hold(entity, index, config)).ToArray();
        dropContentsOnDeath = attributes.IsTrue("dropContentsOnDeath");
        foreach (var hold in holds) hold.Initialize();
        if (entity.Api.Side == EnumAppSide.Server) StrayContainers.Stow(entity, properties, Stow, entity.Api.ModLoader.GetModSystem<CargoModSystem>());

        if (entity.Api is not ICoreClientAPI capi) return;
        piles = new GroundPile(capi);
        renderer = new CargoRenderer(capi, entity, attributes["pileScale"].AsFloat());
        foreach (var hold in holds)
            entity.WatchedAttributes.RegisterModifiedListener(hold.Key, () =>
            {
                hold.Load();
                Lay();
            });
        Lay();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (mode != EnumInteractMode.Interact || byEntity is not EntityPlayer player || byEntity.Controls.ShiftKey || byEntity.Controls.CtrlKey) return;
        if (Aimed(player) is not { } hold) return;

        handled = EnumHandling.PreventSubsequent;
        hold.Open(player);
    }

    public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
    {
        if (holds.Any(hold => hold.Receive(player, packetid, data, Code))) handled = EnumHandling.PreventSubsequent;
    }

    public override bool TryGiveItemStack(ItemStack itemstack, ref EnumHandling handling)
    {
        int offeredSize = itemstack.StackSize;
        var offered = new DummySlot(itemstack);
        foreach (var hold in holds) hold.Stow(offered, spare: false);
        handling = EnumHandling.PreventSubsequent;
        return itemstack.StackSize < offeredSize;
    }

    public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (Aimed(player.Entity) == null) return [];
        return help ??= [new WorldInteraction { ActionLangCode = "blockhelp-open", MouseButton = EnumMouseButton.Right }];
    }

    public void Stow(ItemStack stack)
    {
        var offered = new DummySlot(stack);
        foreach (var hold in holds) hold.Stow(offered, spare: false);
        holds[^1].Stow(offered, spare: true);
    }

    public void DropAll()
    {
        foreach (var hold in holds) hold.Inventory.DropAll(entity.Pos.XYZ);
    }

    public override void OnEntityDeath(DamageSource damageSourceForDeath)
    {
        base.OnEntityDeath(damageSourceForDeath);
        if (dropContentsOnDeath) DropAll();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        base.OnEntityDespawn(despawn);
        foreach (var hold in holds) hold.Close();
        renderer?.Dispose();
    }

    public override bool ShouldEarlyLoadCollectibleMappings() => true;

    public override bool TryEarlyLoadCollectibleMappings(IWorldAccessor worldForCollectibleResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports, EntityProperties entityProperties, JsonObject behaviorConfig)
    {
        OnLoadCollectibleMappings(worldForCollectibleResolve, oldBlockIdMapping, oldItemIdMapping, resolveImports);
        return true;
    }

    public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports)
    {
        foreach (var hold in holds) hold.Container.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, 0, resolveImports);
    }

    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
        foreach (var hold in holds) hold.Container.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
    }

    public override void ToBytes(bool forClient)
    {
        foreach (var hold in holds) hold.Store();
    }

    public override void FromBytes(bool isSync)
    {
        foreach (var hold in holds) hold.Load();
    }

    private Hold? Aimed(EntityPlayer player)
    {
        int index = player.EntitySelection?.SelectionBoxIndex ?? 0;
        var boxes = entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes;
        if (boxes == null || index <= 0 || index > boxes.Length) return null;
        var code = boxes[index - 1].AttachPoint.Code;
        return holds.FirstOrDefault(hold => hold.Box == code);
    }

    private void Lay()
    {
        if (piles == null || renderer == null) return;

        var log = entity.Api.ModLoader.GetModSystem<CargoModSystem>();
        var laid = holds.SelectMany(hold => hold.Piles(piles)).ToArray();
        foreach (var pile in laid)
            log.Log(entity, "{0:P0} full: {1} lies at {2}", pile.Fill, pile.Stack.Collectible.Code, pile.At);
        renderer.Show(laid.Where(pile => pile.Mesh != null).Select(pile => (pile.Mesh!, pile.At)));
    }
}
