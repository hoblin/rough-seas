Rough Seas gives the world weather of its own, but it doesn't replace the game's weather. It drives it. So read the weather as you would in vanilla, through vanilla's own calls, and you read ours. This page is for a mod that reads or changes the wind, the rain or the temperature.

## Read it as you would in vanilla

- **Wind.** `BlockAccessor.GetWindSpeedAt(pos)` returns the wind there, as vanilla does, through the same `OnGetWindSpeed` event. With Rough Seas it is a full horizontal vector: X toward east, Z toward south. Vanilla fills X alone. If your mod reads X as the strength, read the vector's length instead.
- **Units.** 1.0 is 25 m/s: a storm on the Beaufort scale. Vanilla's own `storm` pattern is 1.0 and its `mediumbreeze` 0.3; ours land on the same scale. A hurricane goes past vanilla's cap of 1.5 above sea level.
- **Rain.** `GetClimateAt(pos).Rainfall` and `RainCloudOverlay`, and `WeatherSystemBase.GetPrecipitation(pos)`. Our rain enters through `OnGetClimate`, right after vanilla's own handler, where vanilla writes its rain.
- **Temperature.** Untouched. We read it. A mod that shifts the temperature shifts where our storms snow and where they thunder.

Wind direction, waves and currents have no place in vanilla; they are read from our own code, in [Reading the sea](Reading-the-Sea).

## Levers we follow

- **`OverridePrecipitation`** (`/weather setprecip`) dries or soaks our weather as it does vanilla's. A mod that dries the world gets a dry world.
- **Handlers.** Our `OnGetWindSpeed` and `OnGetClimate` handlers run after vanilla's and before the Devastation's. Our ModSystem's `ExecuteOrder` is 1. A handler of yours that runs later adjusts our answer.
- **Harmony.** Our postfixes that set a value run first (`Priority.High`), so a postfix of yours at the default priority adjusts ours instead of being overwritten. Everything else of ours runs late (`Priority.Low`):
  - a transpiler on `WeatherSimulationRegion.RandomWeatherEvent`;
  - postfixes on `GetPrecipitation`, `LoadLerp`, `pgetBlendedCloudBrightness` and the cloud renderers' `CloudTick`;
  - a prefix and postfix pair around `DefaultShaderUniforms.Update` and `EntityBoat.OnRenderFrame`, which hands them the wind's strength as X.

How the region's wind is set is on [Wind patterns](Wind-Patterns), and how weather events are picked on [Weather events](Weather-Events).
