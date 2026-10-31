using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace RoughSeas.Vessels;

/// <summary>
/// Ends that stand up by their own stiffness and sag as the hull soaks. Each end is drawn at several sags, from dry
/// to saturated, as the children <c>{end}Droop{n}</c> of its group; she shows the one her soak has reached.
/// </summary>
public class EntityBehaviorDroop(Entity entity) : EntityBehavior(entity)
{
    public const string Code = "roughseas:droop";

    private const string SagPrefix = "Droop";

    private string[] ends = [];
    private int sags;
    private int shown;

    public override string PropertyName() => Code;

    public static int SagOf(double soaked, int sags) => sags <= 1 ? 0 : (int)Math.Round(Math.Clamp(soaked, 0, 1) * (sags - 1));

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        ends = attributes["ends"].AsObject<string[]>([]);
        sags = ends.Length == 0 ? 0 : Sags(properties, ends[0]);
        entity.WatchedAttributes.RegisterModifiedListener(EntityBehaviorVesselIntegrity.TreeKey, OnSoaked);
    }

    public override void AfterInitialized(bool onFirstSpawn) => shown = Sag();

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        int sag = Sag();
        willDeleteElements = (willDeleteElements ?? []).Append(ends.SelectMany(end =>
            Enumerable.Range(0, sags).Where(n => n != sag).Select(n => $"{end}{SagPrefix}{n}")).ToArray());
    }

    private int Sag() => SagOf((entity.GetBehavior<EntityBehaviorVesselIntegrity>()?.Model as ISoaking)?.Soaked ?? 0, sags);

    private void OnSoaked()
    {
        int sag = Sag();
        if (sag == shown) return;

        shown = sag;
        entity.MarkShapeModified();
    }

    private int Sags(EntityProperties properties, string end)
    {
        if (properties.Client?.Shape?.Base is not { } shape) return 0;

        var path = shape.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        return ObjectCacheUtil.GetOrCreate(entity.Api, $"{Code}/sags/{path}/{end}",
            () => Shape.TryGet(entity.Api, path)?.GetElementByName(end)?.Children?.Length ?? 0);
    }
}
