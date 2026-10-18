Rough Seas gives the world weather of its own, but it doesn't replace the game's weather. It drives it. Wind, rain and weather events still come out of vanilla's own machinery, so a mod written for plain Vintage Story works next to Rough Seas without a line about it. This page is for that mod's author: what reaches you through vanilla, what we change there, and what to leave alone.

![Rough Seas lies under vanilla: the world's ocean map and climate at the bottom, Rough Seas' map on the server above them, vanilla's weather machinery driven by it, and other mods reading vanilla as usual at the top; clients read the map the server sends](images/diagrams/under-vanilla.svg)

## Read the weather as you would in vanilla

- **Wind.** `BlockAccessor.GetWindSpeedAt(pos)` returns the wind there, as vanilla does, through the same `OnGetWindSpeed` event. With Rough Seas it is a full horizontal vector: X toward east, Z toward south. Vanilla fills X alone. If your mod reads X as the strength, read the vector's length instead.
- **Units.** 1.0 is 25 m/s: a storm on the Beaufort scale. Vanilla's own `storm` pattern is 1.0 and its `mediumbreeze` 0.3; ours land on the same scale. A hurricane goes past vanilla's cap of 1.5 above sea level.
- **Rain.** `GetClimateAt(pos).Rainfall` and `RainCloudOverlay`, and `WeatherSystemBase.GetPrecipitation(pos)`. Our rain enters through `OnGetClimate`, right after vanilla's own handler, where vanilla writes its rain.
- **Temperature.** Untouched. We read it. A mod that shifts the temperature shifts where our storms snow and where they thunder.

## Weather events fire by their own conditions

Vanilla's event roll runs as usual. Rough Seas fixes two things in it:

- It reads the current rain instead of the rain worldgen laid down.
- It picks by each event's own conditions (`weightFunction`, `minTemp`, `minRain` and so on), not by its bare weight.

So an event of yours plays wherever its conditions hold against our rain and the live temperature: hail in a cold downpour, thunder in a warm storm, nothing with lightning under a dry, calm sea. There is no list of other mods' events in our code. Better and Improved Weather's events work this way today.

Where a storm covers a region and the storm's own event can play there (`heavythunder` or `lightthunder`), we hold that event for as long as the storm does. Vanilla's calm event outweighs all the others, so without the hold a storm would rarely thunder.

## Region wind patterns

Each map region carries one of our 13 wind patterns, `roughseas-force0` to `roughseas-force12`, one per Beaufort force. They hold the region's wind at its exact strength, renewed every game hour.

**If you set a region's pattern yourself,** with `SetWindPattern` or `/weather setw`, the region is yours until that pattern's `ActiveUntilTotalHours`; then our map takes it back. One limit: when a region is first visited, or after the server restarts, we can't tell your pattern from vanilla's random pick, and we take the region at once.

Turning off `autoChangePatterns` (`/weather acp`) stops us driving regions at all, as it stops vanilla's own rolls.

## Levers we follow

- **`OverridePrecipitation`** (`/weather setprecip`) dries or soaks our weather as it does vanilla's. A mod that dries the world gets a dry world.
- **Handlers.** Our `OnGetWindSpeed` and `OnGetClimate` handlers run after vanilla's and before the Devastation's. Our ModSystem's `ExecuteOrder` is 1. A handler of yours that runs later adjusts our answer.
- **Harmony.** Our postfixes that set a value run first (`Priority.High`), so a postfix of yours at the default priority adjusts ours instead of being overwritten. Everything else of ours runs late (`Priority.Low`):
  - a transpiler on `WeatherSimulationRegion.RandomWeatherEvent`;
  - postfixes on `GetPrecipitation`, `LoadLerp`, `pgetBlendedCloudBrightness` and the cloud renderers' `CloudTick`;
  - a prefix and postfix pair around `DefaultShaderUniforms.Update` and `EntityBoat.OnRenderFrame`, which hands them the wind's strength as X.

## The ocean map

Rough Seas reads the world's own ocean layer, `GenMaps.oceanGen`, for the sea beyond what is built. It never builds a second one. The layer isn't thread-safe, so we lock its `GenLayer`, a prefix first and a finalizer last. Your calls to it take the same lock, from any thread.

What the layer's numbers mean is learned from the built world: where salt water actually lies at sea level. So a worldgen mod that reshapes the oceans needs nothing from us; the sea follows its map.

## Reading our sea from code

```csharp
var sea = api.ModLoader.GetModSystem<SeaWeatherSystem>();
SeaState state = sea.At(x, z);       // wind (m/s, toward east and south), Beaufort force,
                                     // wave height and period, storm presence
SeaCurrent current = sea.CurrentAt(x, z);
```

It answers on both sides. The server computes the state. A client reads the map the server sends of the regions around its player, so far from players a client knows nothing. Wave height is the significant height of the sea there, from its wind, fetch and depth. This interface is young and may still change; tell us on the [issue tracker](https://github.com/hoblin/rough-seas/issues) if you build on it.

## What not to touch

| Name | What it is |
| --- | --- |
| `roughseas:seamap` | the map region moddata, and the network channel that carries the map to clients |
| `roughseas:oceancalibration` | save data: what the world's ocean map means |
| `roughseas-force*` | our wind patterns; setting one by hand works, but the next lease renews it |

## Adding and removing Rough Seas

The mod can join an existing world, and leave one. Without it the world opens cleanly: regions keep our last wind pattern until it runs out, then vanilla rolls its own.
