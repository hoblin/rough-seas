using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

public sealed class HullCracks : ITexPositionSource
{
    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly CrackConfig config;
    private readonly Dictionary<string, TextureAtlasPosition> cracked = new();
    private ITexPositionSource inner = null!;

    public HullCracks(ICoreClientAPI capi, Entity entity, CrackConfig config)
    {
        this.capi = capi;
        this.entity = entity;
        this.config = config;
    }

    public bool Visible => cracked.Count > 0;

    public Size2i? AtlasSize => inner.AtlasSize;

    public TextureAtlasPosition? this[string textureCode] =>
        cracked.TryGetValue(textureCode, out var position) ? position : inner[textureCode];

    public void Show(int stage)
    {
        // Inserting may grow the entity atlas, which only the main thread may do; tesselation can run elsewhere.
        cracked.Clear();
        if (stage == 0) return;

        var overlays = config.Overlays[stage - 1].Select(overlay => new BlendedOverlayTexture { Base = overlay });
        foreach (var code in config.Textures)
        {
            if (!entity.Properties.Client.Textures.TryGetValue(code, out var texture)) continue;

            var crackedTexture = texture.Clone();
            crackedTexture.BlendedOverlays = [.. crackedTexture.BlendedOverlays ?? [], .. overlays];
            capi.EntityTextureAtlas.GetOrInsertTexture(crackedTexture, out _, out var position);
            cracked[code] = position;
        }
    }

    public ITexPositionSource Over(ITexPositionSource defaultSource)
    {
        inner = defaultSource;
        return this;
    }
}
