using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RoughSeas.Vessels;

public sealed class HullDamage
{
    private readonly ICoreClientAPI capi;
    private readonly Entity entity;
    private readonly HullDamageConfig config;
    private IReadOnlyDictionary<string, TextureAtlasPosition> damaged = new Dictionary<string, TextureAtlasPosition>();

    public HullDamage(ICoreClientAPI capi, Entity entity, HullDamageConfig config)
    {
        this.capi = capi;
        this.entity = entity;
        this.config = config;
    }

    public bool Visible => damaged.Count > 0;

    public void Show(int stage)
    {
        // Inserting may grow the entity atlas, which only the main thread may do; tesselation can run elsewhere,
        // so it gets a finished map, never one being filled.
        var map = new Dictionary<string, TextureAtlasPosition>();
        if (stage > 0)
        {
            var overlay = new BlendedOverlayTexture { Base = config.Overlay(stage) };
            foreach (var code in config.Textures)
            {
                if (!entity.Properties.Client.Textures.TryGetValue(code, out var texture)) continue;

                var damagedTexture = texture.Clone();
                damagedTexture.BlendedOverlays = [.. damagedTexture.BlendedOverlays ?? [], overlay];
                capi.EntityTextureAtlas.GetOrInsertTexture(damagedTexture, out _, out var position);
                map[code] = position;
            }
        }
        damaged = map;
    }

    public ITexPositionSource Over(ITexPositionSource defaultSource) => new DamagedSource(damaged, defaultSource);

    private sealed class DamagedSource(IReadOnlyDictionary<string, TextureAtlasPosition> damaged, ITexPositionSource defaultSource)
        : ITexPositionSource
    {
        public Size2i? AtlasSize => defaultSource.AtlasSize;

        public TextureAtlasPosition? this[string textureCode] =>
            damaged.TryGetValue(textureCode, out var position) ? position : defaultSource[textureCode];
    }
}
