Vanilla has no place for the wind's direction, for waves or for currents, so Rough Seas publishes them from its own code. This page is for a mod that reads them: a ship that sails by our wind, a helm that shows the sea, a fish that swims with the current.

## Reaching it

```csharp
var sea = api.ModLoader.GetModSystem<SeaWeatherSystem>();
SeaState state = sea.At(x, z);
SeaCurrent current = sea.CurrentAt(x, z);
```

`SeaWeatherSystem` lives in `RoughSeas.SeaAndWeather` and implements `ISeaWeather`. Compile against `RoughSeas.dll` from our release, referenced but not shipped in your mod (`<Private>false</Private>`). If Rough Seas is optional for you, check `api.ModLoader.IsModEnabled("roughseas")` first and keep every call into our types in a class of its own, so the game never loads them without us.

Positions are absolute block coordinates. The answer is for now, on either side.

## What it answers

`ISeaWeather`:

| Member | What it is |
| --- | --- |
| `At(x, z)` | the sea state there, a `SeaState` |
| `CurrentAt(x, z)` | the current and the water's temperature there, a `SeaCurrent` |
| `DangerousOceans` | the world's [Dangerous oceans](World-Settings#dangerous-oceans) setting |
| `NowHours` | the game time the state is read at, in total game hours |

`SeaState`:

| Field | Unit | What it is |
| --- | --- | --- |
| `WindX`, `WindZ` | m/s | the wind, toward east and toward south |
| `WindSpeed` | m/s | its strength |
| `WindFromDegrees` | ° | where it blows from: 0 north, 90 east |
| `Beaufort` | 0–12 | its force on the [Beaufort scale](Wind-and-Waves#the-beaufort-scale) |
| `WaveHeight` | m | the significant height of the wind sea, from its wind, fetch and depth |
| `WavePeriod` | s | its peak period |
| `StormPresence` | 0–1 | how much of a storm is here, all storms together |
| `StormCode` | | the strongest storm's type (`squall`, `gale`, `hurricane`), or null; on the server only |

`SeaCurrent`:

| Field | Unit | What it is |
| --- | --- | --- |
| `X`, `Z` | m/s | the current, toward east and toward south |
| `Speed` | m/s | its strength |
| `TowardDegrees` | ° | where it flows to: 0 north, 90 east |
| `WaterCelsius` | °C | the sea's surface temperature |
| `WarmthCelsius` | °C | how much the current warms the water, cooling below zero; on the server only, null on a client |

## What the numbers mean

- **Waves are the sea, not a wave.** `WaveHeight` is the significant height: the mean of the highest third. Each wave that reaches a point is drawn from it, most lower, one in a hundred half again as high ([Every wave is different](Wind-and-Waves#every-wave-is-different)). The waves run with the wind; there is no swell of another direction.
- **The wind is in m/s here.** Vanilla's `GetWindSpeedAt` gives the same wind on its own scale, 1.0 for 25 m/s ([Reading the weather](Reading-the-Weather)).
- **The state doesn't know about danger.** Wind, waves and currents are there whether or not the oceans are dangerous. A mod that harms something by them reads `DangerousOceans` first, as ours do.
- **Server and client.** The server computes the state. A client reads the map the server sends of the regions around its player, so far from players a client knows nothing and answers zeros.

A vessel rides the waves drawn on a client's screen through our behaviours, on [Building a vessel](Building-a-Vessel).
