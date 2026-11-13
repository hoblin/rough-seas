using System;
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
    [JsonProperty] public float[][] PlacesMetres = [];
}

public sealed class Hold
{
    private const int InventoryPackets = 1000;

    private readonly Entity entity;
    private readonly string key;
    private readonly int packetOffset;
    private readonly Vec3f[] places;
    private readonly InWorldContainer container;
    private GuiDialogCreatureContents? dialog;

    public Hold(Entity entity, int index, HoldConfig config)
    {
        this.entity = entity;
        Box = config.SelectionBox;
        key = $"roughseas:hold{index}";
        // Clear of the offsets vanilla's held bags take in rideableaccessories, one per slot shifted by 11 bits.
        packetOffset = (index + 1) << 20;
        places = config.PlacesMetres.Select(at => new Vec3f(at)).ToArray();
        Inventory = new HoldInventory(config.QuantitySlots, $"roughseashold{index}-{entity.EntityId}", entity.Api);
        container = new InWorldContainer(() => Inventory, key);
    }

    public string Box { get; }

    public HoldInventory Inventory { get; }

    public InWorldContainer Container => container;

    public string Key => key;

    public void Initialize()
    {
        container.Init(entity.Api, () => entity.Pos.AsBlockPos, Store);
        Load();
        if (entity.Api.Side == EnumAppSide.Server)
            Inventory.SlotModified += _ =>
            {
                Inventory.Settle();
                Store();
            };
    }

    public void Load() => container.FromTreeAttributes(entity.WatchedAttributes, entity.World);

    public void Store()
    {
        container.ToTreeAttributes(entity.WatchedAttributes);
        entity.WatchedAttributes.MarkPathDirty(key);
        entity.World.BlockAccessor.GetChunkAtBlockPos(entity.Pos.AsBlockPos)?.MarkModified();
    }

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

    public void Stow(ItemSlot offered, bool spare)
    {
        while (!offered.Empty && Inventory.GetBestSuitedSlot(offered, null, []).slot is { } berth)
            if (Put(offered, berth) == 0) break;
        while (spare && !offered.Empty) Put(offered, Inventory.Spare());
    }

    private int Put(ItemSlot offered, ItemSlot berth) =>
        offered.TryPutInto(entity.World, berth, Math.Min(offered.StackSize, offered.Itemstack!.Collectible.MaxStackSize - berth.StackSize));

    public (MeshData? Mesh, Vec3f At, ItemStack Stack, double Fill)[] Piles(GroundPile piles)
    {
        var loads = Inventory
            .Select((slot, index) => (slot, index))
            .Where(each => !each.slot.Empty)
            .Select(each => new Load(each.index, each.slot.StackSize, each.slot.Itemstack!.Collectible.MaxStackSize))
            .ToList();
        double fill = Stowage.Fill(loads, Inventory.Count);
        var drawn = loads.Where(load => piles.Draws(Inventory[load.Slot].Itemstack!)).ToList();
        return Stowage.Lay(drawn, places.Select(at => new double[] { at.X, at.Y, at.Z }).ToList())
            .Select(stowed =>
            {
                var stack = Inventory[stowed.Slot].Itemstack!;
                return (piles.Of(stack, fill), places[stowed.Place], stack, fill);
            })
            .ToArray();
    }
}
