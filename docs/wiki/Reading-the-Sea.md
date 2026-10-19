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

## The drawn sea

The waves a client draws are a picture of the sea state, not the state itself. On a client, they are published for a vessel that poses itself on them: one whose class doesn't take our pose (see [Riding the drawn swell](Building-a-Vessel#taking-the-sea)).

```csharp
IDrawnSea? drawn = api.ModLoader.GetModSystem<StormsModSystem>().DrawnSea;
SeaPose pose = drawn.PoseOf(x, z, yaw, length, width);
```

`StormsModSystem` and `IDrawnSea` live in `RoughSeas.Storms`. `DrawnSea` is null on the server. Our own vessels are posed by the same call.

| Member | What it is |
| --- | --- |
| `Swell` | whether any swell is drawn now |
| `HeightAt(x, z)` | the surface's height above still water there now, in metres |
| `PoseOf(x, z, yaw, length, width)` | the pose of a hull there, a `SeaPose` |

`PoseOf` samples the surface at the ends and sides of a hull of `length` by `width` blocks centred at `x, z`. `yaw` is its heading in radians as its renderer turns it: the entity's `Pos.Yaw` plus its shape's `rotateY`, which is in degrees: `Pos.Yaw + rotateY * GameMath.DEG2RAD`. Its length runs along the shape's X axis.

`SeaPose`:

| Field | Unit | What it is |
| --- | --- | --- |
| `Hold` | 0–1 | how much the drawn swell holds the hull: 0 where none is drawn |
| `Lift` | m | how far the surface lifts it, added to its model's height |
| `Roll` | rad | its roll, about its length, as the renderer's `xangle` |
| `Pitch` | rad | its pitch, about its width, as the renderer's `zangle` |

- **Blend by `Hold`.** Our vessels rock as vanilla rocks them where no swell is drawn and take the pose where it holds them fully, blending between by `Hold`. A shallow bay draws a lower swell than the open sea, so it holds less.
- **The pose is only the picture.** The server knows nothing of it: breakers, shipped water and impacts come from the sea state. Read it every frame, in your render path; it changes as the waves move.
- **A pose is the water's slope.** Ours multiply roll and pitch by a vessel's `rollResponse` and `pitchResponse`; yours scales them as its hull would.

### How a hull answers

A hull of some mass doesn't take the water's pose at once: it lifts, rolls and pitches toward it on periods of its own, so a light canoe follows a long swell as the water lies and a heavy, deep hull lets a short chop pass under her. `HullResponse`, in `RoughSeas.Storms`, is how ours do it, and yours can do the same: one instance per hull, fed every frame with what `PoseOf` gives.

```csharp
var config = api.ModLoader.GetModSystem<StormsModSystem>().Config.Response;
var response = HullResponse.Of(length, beam, draft, depth, config);
SeaPose answered = response.Follow(drawn.PoseOf(x, z, yaw, length, beam), frameSeconds);
```

| Member | What it is |
| --- | --- |
| `HullResponse.Of(lengthMetres, beamMetres, draftMetres, depthMetres, config)` | the response of a hull of these dimensions, floating at this draft |
| `new HullResponse(heavePeriodSeconds, rollPeriodSeconds, config)` | one on periods you give; a period of 0 follows the water as it lies |
| `Follow(pose, seconds)` | the pose it takes `seconds` after the last call, chasing `pose` as a damped oscillator; where no swell is drawn it comes to rest at once |
| `HeavePeriodSeconds`, `RollPeriodSeconds` | its periods; set them as its draft changes |
| `HullResponse.HeavePeriod(draftMetres, config)` | heave and pitch: 2π √((1 + added mass) × draft / g), in seconds |
| `HullResponse.RollPeriod(lengthMetres, beamMetres, draftMetres, depthMetres)` | roll by 46 CFR 28.575, 2 C B / √GM, for a box hull with its centre of gravity at half its depth, in seconds |

`Follow` returns the pose with its `Lift`, `Roll` and `Pitch` answered, its `Hold` as it was: blend by it as before. Ours refresh the periods from the hull's draft every frame, so a laden hull answers slower than an empty one. `config` is the `response` block of our `config/storms.json`: `heaveAddedMass` (the water a hull drags with it, in its own masses: 1), `heaveDamping` (0.3) and `rollDamping` (0.1), each a share of critical damping.
