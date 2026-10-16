using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

public sealed class HullCracks
{
    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly CrackConfig config;
    private IReadOnlyDictionary<string, TextureAtlasPosition> cracked = new Dictionary<string, TextureAtlasPosition>();

    public HullCracks(ICoreClientAPI capi, Entity entity, CrackConfig config)
    {
        this.capi = capi;
        this.entity = entity;
        this.config = config;
    }

    public bool Visible => cracked.Count > 0;

    public void Show(int stage)
    {
        // Inserting may grow the entity atlas, which only the main thread may do; tesselation can run elsewhere,
        // so it gets a finished map, never one being filled.
        var map = new Dictionary<string, TextureAtlasPosition>();
        if (stage > 0)
        {
            var overlays = config.Overlays[stage - 1].Select(overlay => new BlendedOverlayTexture { Base = overlay });
            foreach (var code in config.Textures)
            {
                if (!entity.Properties.Client.Textures.TryGetValue(code, out var texture)) continue;

                var crackedTexture = texture.Clone();
                crackedTexture.BlendedOverlays = [.. crackedTexture.BlendedOverlays ?? [], .. overlays];
                capi.EntityTextureAtlas.GetOrInsertTexture(crackedTexture, out _, out var position);
                map[code] = position;
            }
        }
        cracked = map;
    }

    public ITexPositionSource Over(ITexPositionSource defaultSource) => new CrackedSource(cracked, defaultSource);

    private sealed class CrackedSource(IReadOnlyDictionary<string, TextureAtlasPosition> cracked, ITexPositionSource defaultSource)
        : ITexPositionSource
    {
        public Size2i? AtlasSize => defaultSource.AtlasSize;

        public TextureAtlasPosition? this[string textureCode] =>
            cracked.TryGetValue(textureCode, out var position) ? position : defaultSource[textureCode];
    }
}
