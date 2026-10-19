Rough Seas doesn't replace the game's weather; it drives it. Vanilla's weather system still answers every question about the wind and the rain. We set what it answers, from our model, through the same events and calls vanilla uses for itself. So read the weather as you would in vanilla, and you read ours. This page is for a mod that reads or changes the wind, the rain or the temperature.

## Read it as you would in vanilla

- **Wind.** `BlockAccessor.GetWindSpeedAt(pos)` returns the wind there, through the same `OnGetWindSpeed` event as in vanilla. Vanilla's wind has a strength and no direction, so it fills X alone. Ours has a direction, so we fill X toward east and Z toward south. If your mod reads X as the strength, read the vector's length instead; it is the same strength vanilla would give.
- **Units.** 1.0 is 25 m/s, a storm on the Beaufort scale; vanilla's own `storm` pattern blows at 1.0 and its `mediumbreeze` at 0.3, so ours land on the scale you already know. Above sea level vanilla caps the wind at 1.5; ours isn't capped there, so a hurricane goes past it.
- **Rain.** Our rain falls from our model's clouds, storms' included, and we write it into the same values vanilla writes its own: `GetClimateAt(pos)` gives the rain falling there as `Rainfall` and the cloud as `RainCloudOverlay`, and `WeatherSystemBase.GetPrecipitation(pos)` the rain alone.
- **Temperature.** Untouched: we read it. A mod that shifts the temperature shifts where our storms snow and where they thunder, and how strong our gales grow.

Wind direction, waves and currents have no place in vanilla; read them from our own code, on [Reading the sea](Reading-the-Sea).

## Changing it

The weather a player meets is whatever the last hand to touch it left. Where we set the weather, we take our turn early, so a mod that adjusts the weather after us adjusts ours. Where we only add to vanilla, we go last, so we never stand between vanilla and a mod that adjusts vanilla.

- **`OverridePrecipitation`** (`/weather setprecip`) dries or soaks our weather as it does vanilla's. A mod that dries the world gets a dry world.
- **Event handlers.** Our `OnGetWindSpeed` and `OnGetClimate` handlers run after vanilla's and before those of vanilla's Devastation: our ModSystem's `ExecuteOrder` is 1. Handlers run in the order their ModSystems start, so a handler of yours registered from a ModSystem with a higher `ExecuteOrder` adjusts our answer.
- **Harmony.** A postfix of ours that sets a value runs first (`Priority.High`), so a postfix of yours at the default priority adjusts our value instead of being overwritten by it. These set the rain and the clouds:
  - `WeatherSystemBase.GetPrecipitation`, both overloads;
  - `WeatherDataReaderBase.LoadLerp`, the rain where no climate is loaded;
  - `CloudRenderer.CloudTick` and `CloudRendererMap.CloudTick`, the clouds' drift with our wind.

  Everything else of ours runs late (`Priority.Low`), after yours:
  - a transpiler on `WeatherSimulationRegion.RandomWeatherEvent` ([Weather events](Weather-Events));
  - postfixes on `LoadLerp` and `pgetBlendedCloudBrightness` that darken a storm's clouds;
  - a prefix and postfix pair around `DefaultShaderUniforms.Update` and `EntityBoat.OnRenderFrame`, which read the wind's X as its strength: we hand them the vector's length for the call and put X back after. At the default priority, a prefix of yours on them runs before ours and sees the wind's X; a postfix of yours runs before ours and sees the length.

How each region's wind is set is on [Wind patterns](Wind-Patterns), and how weather events are picked on [Weather events](Weather-Events).
