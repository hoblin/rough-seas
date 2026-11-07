using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>
/// Draws the player with each limb in its own colour and no clothes, so a screenshot shows where every arm and leg is:
/// arms red and orange on the left, blue and cyan on the right; legs green and lime on the left, purple and pink on the
/// right; the upper torso yellow, the lower grey. A face's side is shaded, so a turn shows too.
/// </summary>
public sealed class LimbColours(ICoreClientAPI capi)
{
    private static readonly AssetLocation Skin = new("agentbridge", "textures/limbs.png");

    private EntityBehaviorTexturedClothing? painting;

    public string Show(bool on)
    {
        var player = capi.World.Player.Entity;
        var clothing = player.GetBehavior<EntityBehaviorTexturedClothing>();
        if (on && painting == null)
        {
            clothing.OnReloadSkin += Paint;
            painting = clothing;
        }
        else if (!on && painting != null)
        {
            painting.OnReloadSkin -= Paint;
            painting = null;
        }
        clothing.hideClothing = on;
        player.MarkShapeModified();
        return on ? "Limbs in colour" : "Limbs as worn";
    }

    private void Paint(LoadedTexture atlas, TextureAtlasPosition skin, int subId)
    {
        var texture = new LoadedTexture(capi);
        capi.Render.GetOrLoadTexture(Skin, ref texture);
        var size = capi.EntityTextureAtlas.Size;
        capi.EntityTextureAtlas.RenderTextureIntoAtlas(skin.atlasTextureId, texture, 0, 0, texture.Width, texture.Height,
            skin.x1 * size.Width, skin.y1 * size.Height, -1);
    }
}
