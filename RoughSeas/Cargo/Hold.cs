using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Cargo;

public sealed class HoldConfig
{
    [JsonProperty] public string SelectionBox = "";
    [JsonProperty] public int QuantitySlots = 8;
}

public readonly record struct Pile(MeshData? Mesh, Vec3f At, ItemStack Stack, double Fill);

/// <summary>
/// One hold of a vessel: an inventory kept in her watched attributes, opened by a click on its own selection box,
/// whose largest stacks lie at its places on the floor.
/// </summary>
public sealed class Hold
{
    private const int InventoryPackets = 1000;
    private const int ClearOfAccessorySlots = 512;

    private readonly Entity entity;
    private readonly string key;
    private readonly int packetOffset;
    private readonly Vec3f[] places;
    private readonly InWorldContainer container;
    private GuiDialogCreatureContents? dialog;

    public Hold(Entity entity, int index, HoldConfig config, float[][] placesMetres)
    {
        this.entity = entity;
        Box = config.SelectionBox;
        key = $"roughseas:hold{index}";
        packetOffset = (ClearOfAccessorySlots + index) << CollectibleBehaviorHeldBag.PacketIdBitShift;
        places = placesMetres.Select(at => new Vec3f(at)).ToArray();
        Inventory = new HoldInventory(config.QuantitySlots, $"roughseashold{index}-{entity.EntityId}", entity.Api);
        container = new InWorldContainer(() => Inventory, key);
    }

    public string Box { get; }

    public HoldInventory Inventory { get; }

    public void Initialize(Action? changed)
    {
        container.Init(entity.Api, () => entity.Pos.AsBlockPos, Store);
        Load();
        if (entity.Api.Side == EnumAppSide.Server)
            Inventory.SlotModified += _ => Store();
        else
            entity.WatchedAttributes.RegisterModifiedListener(key, () =>
            {
                Load();
                changed?.Invoke();
            });
    }

    public void Load() => container.FromTreeAttributes(entity.WatchedAttributes, entity.World);

    public void Store()
    {
        container.ToTreeAttributes(entity.WatchedAttributes);
        entity.WatchedAttributes.MarkPathDirty(key);
        entity.World.BlockAccessor.GetChunkAtBlockPos(entity.Pos.AsBlockPos)?.MarkModified();
    }

    public void LoadMappings(IWorldAccessor world, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports) =>
        container.OnLoadCollectibleMappings(world, oldBlockIdMapping, oldItemIdMapping, 0, resolveImports);

    public void StoreMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping) =>
        container.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

    public void Open(EntityPlayer player)
    {
        var opener = entity.World.PlayerByUid(player.PlayerUID);
        opener.InventoryManager.OpenInventory(Inventory);
        if (entity.Api is not ICoreClientAPI capi || dialog != null) return;

        dialog = new GuiDialogCreatureContents(Inventory, entity, capi, "roughseas:cargo-hold") { packetIdOffset = packetOffset };
        if (dialog.TryOpen()) capi.Network.SendPacketClient(Inventory.Open(opener));
        dialog.OnClosed += () =>
        {
            dialog?.Dispose();
            dialog = null;
        };
    }

    public bool Receive(IServerPlayer player, int packetid, byte[] data, string reason)
    {
        int packet = packetid - packetOffset;
        if (packet is < 0 or >= InventoryPackets) return false;

        if (new Entity.CachedAccessPerms(entity, player).IsInteractingPlayerAllowedTo(EnumBlockAccessFlags.None, true, reason))
            Inventory.InvNetworkUtil.HandleClientPacket(player, packet, data);
        else
            Inventory.InvNetworkUtil.SendInventoryRollback(player, packet, data);
        return true;
    }

    public void Close() => dialog?.TryClose();

    public void DropAll() => Inventory.DropAll(entity.Pos.XYZ);

    public void Take(ItemSlot offered)
    {
        while (!offered.Empty && Inventory.GetBestSuitedSlot(offered, null, []).slot is { } berth)
            if (Put(offered, berth) == 0) break;
    }

    public void TakeAll(ItemSlot offered)
    {
        Take(offered);
        while (!offered.Empty) Put(offered, Inventory.Spare());
    }

    private int Put(ItemSlot offered, ItemSlot berth) =>
        offered.TryPutInto(entity.World, berth, Math.Min(offered.StackSize, offered.Itemstack!.Collectible.MaxStackSize - berth.StackSize));

    public Pile[] Piles(GroundPile piles)
    {
        var lading = Inventory
            .Select((slot, index) => (slot, index))
            .Where(each => !each.slot.Empty)
            .Select(each => new Lading(each.index, each.slot.StackSize, each.slot.Itemstack!.Collectible.MaxStackSize))
            .ToList();
        double fill = Stowage.Fill(lading, Inventory.Count);
        var drawn = lading.Where(stack => piles.Draws(Inventory[stack.Slot].Itemstack!)).ToList();
        return Stowage.Lay(drawn, places.Select(at => new double[] { at.X, at.Y, at.Z }).ToList())
            .Select(stowed =>
            {
                var stack = Inventory[stowed.Slot].Itemstack!;
                return new Pile(piles.Of(stack, fill), places[stowed.Place], stack, fill);
            })
            .ToArray();
    }
}
