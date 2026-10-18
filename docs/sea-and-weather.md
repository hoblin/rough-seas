# Sea and weather

Read before working on wind, rain, waves, storms, currents or the sea map. The code is in `RoughSeas/SeaAndWeather/`; what draws the sea is in `RoughSeas/Storms/`.

## The shape

The server owns the sea and weather; a client reads a map of it and computes nothing.

```
server                                         client
SeaModel (ISeaModel, ISeaSource)               SeaMapClient (ISeaSource)
├─ SeaWeatherModel: storms, background wind,   ├─ snapshots from the region's moddata
│  showers, currents                           │  and from the roughseas:seamap channel
├─ OceanBasins: the ocean map + calibration    └─ interpolated in space and in time
├─ WaterBody: fetch and depth upwind
└─ WindSea: the waves those raise                SurfaceClock → SurfaceField + SeaSurface
SeaMapServer ── snapshot per region ─────────→  the water shader, vessels, floating items
```

`SeaWeatherSystem` (`ISeaWeather`) is the one door on both sides: `At` and `CurrentAt` answer from `Source`, which is the model on the server and the received map on a client. `Model` is the server's alone and throws on a client.

## The map

- `SeaMapServer` ticks every 250 ms. It refreshes the map regions within one of each player's, each every `map.updateSeconds`, and stops for the tick once `map.tickBudgetMilliseconds` are spent.
- A snapshot (`SeaMapSnapshot`) holds a grid of `map.cellBlocks` cells: wind, storm presence, rain, wave height and period, current, water temperature. Each field is an Int16 at its own scale, with explicit protobuf tags and a version.
- It goes into the map region's moddata (`roughseas:seamap`), so a client that loads the region has it at once, and over the channel to the players near it.
- `SeaMapClient` blends a cell with its neighbours bilinearly, and eases from the shown values to a new snapshot over the time between two. A region older than `map.staleHours` counts as no data.

## What the model stands on

- **The ocean map is the world's own.** A built region's `OceanMap`; where nothing is built yet, `GenMaps.oceanGen` read through `GeneratedOcean` (small tiles, cached). That layer is not thread-safe, so `OceanGenLock` locks its every call, vanilla's chunk thread included. Never build a second layer.
- **The map's numbers mean what the worldgen makes them mean.** `OceanCalibration` learns from the built world where sea starts on it: salt water at sea level, column by column, only in loaded chunks, each column once, by balanced error. It settles at `ocean.calibrationSamples`, is saved with the world (`roughseas:oceancalibration`, versioned), and the model forgets what it brewed from the default.
- **Climate is vanilla's.** A storm's birth comes from seed, worldgen climate, latitude, season and time. Its strength follows the season's north-south temperature gradient, which `SeaWeatherSystem.SeasonalCelsius` takes from the game's own climate chain (`TriggerOnGetClimate`, as `GetClimateAt` runs it), so a mod that moves temperatures moves the storms. Its expression (rain, snow, thunder) reads the live temperature.
- **Storms follow measurements, per day and area.** Each type is born at `birthsPerMillionKm2PerDay`, turned into a chance per cell and slot through the horizontal scale; a day is a real day and only the calendar is the game's. Every storm number in `seaweather.json` carries its source; the wiki's *The science behind it* gathers them. `/bridge climate` checks the result over years of storms at a grid of points.

## Waves

- Young & Verhagen (1996), as CEM II-2 gives it, for every depth, with the depth's period cap Tp ≤ 9.78√(d/g).
- Fetch is the straight line upwind: over the built world in `waves.fetchStepBlocks` steps while it is built, over the ocean map in `waves.openSeaStepBlocks` beyond, up to `waves.maxFetchBlocks`. It is scaled by the world's horizontal compression, `waves.realPolarEquatorMetres` over the world's `polarEquatorDistance` (×200 by default). Depth is not scaled.
- Depth is worldgen's seabed where water still reaches it, and sounded block by block where the world changed. Beyond the built world it is the built depth measured so far.
- Duration: the wind's mean over each of `seaBuildHours` becomes an equivalent fetch (CEM II-2-38), and the highest sea of those windows stands. A squall raises a young short sea; a storm builds one over hours.
- Above `waves.kneeMetres` a soft knee bends the height toward `waves.ceilingMetres`.

## How it meets vanilla

- **Wind.** `VanillaRegions` (server, every 2 s, while vanilla's `autoChangePatterns` is on) has `RegionWinds` lease each region our Beaufort pattern (`config/windpatterns/beaufort.json`, `roughseas-force0`–`12`) at the region's exact strength. A pattern someone else set keeps the region until it ends. `VanillaWind` runs after vanilla's `OnGetWindSpeed` and turns vanilla's answer to the map's direction and finer detail.
- **Rain.** `VanillaRain` writes into `OnGetClimate` after vanilla's handler, and steps aside for `OverridePrecipitation`.
- **Events.** Vanilla's roll runs. Our transpiler makes it read current rain and pick by each event's own conditions. `StormEvents` pins a storm type's event where a storm covers a region and its conditions hold.
- **Order.** `SeaWeatherSystem.ExecuteOrder` is 1: after vanilla's weather (0.1), before the Devastation's effects (2).
- **Patch priorities.** A postfix that sets a value runs first (`Priority.High`), so other mods' postfixes adjust ours. Patches that only read or adjust run late (`Priority.Low`), the transpiler too. The ocean layer's lock is outermost.
- **Units.** Vanilla's wind 1.0 is 25 m/s (`vanilla.unitsPerMps`), checked against its `storm` and `mediumbreeze` patterns.

## Drawing it

- `SurfaceClock` aims every `surface.aimSeconds`: `SurfaceField` samples the waves around the player into a 48 × 48 texture, `seaHeightMap`, and `SeaSurface` sets the wave trains from the highest of them.
- A cell holds its waves as a share of the height the surface draws, up to twice it. The shader, vessels and floating items read the same share (`SurfaceField.Factor`), so a boat rides the wave drawn under it.
- Our sampler sits in vanilla's shared `vertexwarp.vsh`, so it is in every chunk program. Vanilla numbers each program's samplers by declaration, so ours binds one past them (`textureLocations.Count`). A fixed unit overwrote a vanilla texture in the leaf pass and drew distant trees black.

## Rules that keep it whole

- Every decision logs a debug line: `[seas]` for the model, the map and vanilla's regions, `[storms]` for the sea's effects.
- Every number is in JSON: `seaweather.json`, `storms.json`, `windpatterns/beaufort.json`.
- What we save is versioned. A world opens without the mod: its regions fall back to vanilla's patterns, and vanilla re-rolls them.
- Read a block's water from its fluid layer (`BlockLayersAccess.Fluid`), and its chunk only once `GetChunkAtBlockPos` finds it: an unloaded chunk reads as air.
