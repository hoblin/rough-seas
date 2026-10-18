using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace RoughSeas.Vessels;

public class WoodsModSystem : ModSystem
{
    private const string WoodLists = "worldproperties/block/wood.json";
    private const string LogTextures = "logTextures";

    private static readonly AssetLocation NotWoodAsset = new("roughseas:config/vessels/notwood.json");
    private static readonly AssetLocation BarksAsset = new("roughseas:config/vessels/canoebarks.json");
    private static readonly AssetLocation RecipesAsset = new("roughseas:config/vessels/woodrecipes.json");

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    // After JSON patches (0.05), which may add woods to a list, and before types and recipes load from our lists (0.2, 1).
    public override double ExecuteOrder() => 0.15;

    public override void AssetsLoaded(ICoreAPI api)
    {
        var notWood = api.Assets.Get<Dictionary<string, string>>(NotWoodAsset);
        var woods = api.Assets.GetMany<StandardWorldProperty>(api.Logger, WoodLists)
            .SelectMany(list => list.Value.Variants.Select(variant => new Wood(list.Key.Domain, variant.Code.Path)))
            .Where(wood => !wood.Keys.Any(notWood.ContainsKey))
            .ToArray();
        var barkList = api.Assets.Get<Dictionary<string, string>>(BarksAsset);
        var barks = woods.Where(wood => wood.Keys.Any(barkList.ContainsKey)).ToArray();

        AddWorldProperty(api, "wood", woods);
        AddWorldProperty(api, "bark", barks);
        var template = api.Assets.Get<JArray>(RecipesAsset);
        foreach (var domain in woods.GroupBy(wood => wood.Domain))
        {
            var lists = new Dictionary<string, string[]>
            {
                ["{woods}"] = domain.Select(wood => wood.Species).ToArray(),
                ["{barks}"] = barks.Where(bark => bark.Domain == domain.Key).Select(bark => bark.Species).ToArray()
            };
            Add(api, $"recipes/grid/woods-{domain.Key}.json", new JArray(WoodRecipes.Expand(template, domain.First(), lists)));
        }

        Log("vessel woods: {0}", string.Join(", ", woods.Select(wood => wood.Code)));
        Log("canoe barks: {0}", string.Join(", ", barks.Select(bark => bark.Code)));
    }

    // The server packs these textures for its clients only after this phase, then drops its own copy.
    public override void AssetsFinalize(ICoreAPI api)
    {
        foreach (var item in api.World.Items.Where(item => item.Attributes?[LogTextures].Exists == true))
        {
            Dress(api.World, item.Textures ??= [], item.Attributes[LogTextures], Wood.Of(item.Variant), item.Code);
        }

        foreach (var entity in api.World.EntityTypes.Where(entity => entity.Attributes?[LogTextures].Exists == true))
        {
            Dress(api.World, entity.Client.Textures, entity.Attributes[LogTextures], Wood.Of(entity.Variant), entity.Code);
        }
    }

    private static void AddWorldProperty(ICoreAPI api, string code, IEnumerable<Wood> woods) =>
        Add(api, $"worldproperties/vessel/{code}.json", new JObject
        {
            ["code"] = code,
            ["variants"] = new JArray(woods.Select(wood => new JObject { ["code"] = wood.Code }))
        });

    private static void Add(ICoreAPI api, string path, JToken content)
    {
        var location = new AssetLocation("roughseas", path);
        // Marked patched so the server keeps its data when it unloads unpatched assets: it has no file to reload from.
        api.Assets.Add(location, new Asset(Encoding.UTF8.GetBytes(content.ToString()), location, null) { IsPatched = true });
    }

    private void Dress(IWorldAccessor world, IDictionary<string, CompositeTexture> textures, JsonObject faces, Wood? wood, AssetLocation code)
    {
        var log = wood == null ? null : world.GetBlock(wood.LyingLog);
        foreach (var (texture, face) in faces.AsObject<Dictionary<string, string>>([]))
        {
            if (log?.Textures?.TryGetValue(face, out var source) == true)
            {
                textures[texture] = source.Clone();
                continue;
            }

            Mod.Logger.Warning("{0} finds no log of its wood to take its {1} texture from", code, texture);
        }
    }

    private void Log(string format, params object[] args) => Mod.Logger.Debug("[vessels] " + string.Format(format, args));
}
