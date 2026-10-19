Vanilla has no place for the wind's direction, for waves or for currents, so Rough Seas publishes them from its own code. This page is for a mod that reads them: a helm that shows the sea, a fish that swims with the current, a ship that rides our swell.

## Two seas

Rough Seas keeps the sea twice, for two different jobs.

**The sea state** is the sea as a number: at any point, how hard the wind blows and from where, how high the waves run and how long between them, how much of a storm is there, and where the current flows. The server computes it, and everything that has consequences comes from it: the waves that come aboard a vessel, the breakers that strike her, the current that carries her. It knows no single wave. A wave that reaches a vessel is drawn by chance from the state, once a wave period, and two vessels side by side meet different waves.

**The drawn sea** is the sea as a picture: the moving surface a client draws, built from the sea state around its player. Its crests have a place and a moment, so a hull can ride them: lift on a crest, roll with its slope. But each client draws its own, on a clock of its own, so two players see crests in different places, and neither picture is the wave the server struck with.

So a mod **decides** by the sea state and **shows** by the drawn sea. A helm, a fish's swimming, a sail's push, anything that harms or moves on the server: the sea state. A hull's pose on the screen: the drawn sea. Nothing on the server should wait for a crest a client draws.

## Reaching us

Compile against `RoughSeas.dll` from our release, referenced but not shipped in your mod (`<Private>false</Private>`).

If Rough Seas is optional for you, check `api.ModLoader.IsModEnabled("roughseas")` and keep every use of our types in a class of its own that you create only then. A method that names one of our types fails to compile at run time when our assembly isn't there, even if the line never runs.

## The sea state

```csharp
var sea = api.ModLoader.GetModSystem<SeaWeatherSystem>();
SeaState state = sea.At(x, z);
SeaCurrent current = sea.CurrentAt(x, z);
```

`SeaWeatherSystem`, in `RoughSeas.SeaAndWeather`, implements `ISeaWeather`. Positions are absolute block coordinates, and the answer is for now: on the server exactly, on a client a few seconds behind.

### Where it comes from

The wind is the background wind of the region and season, plus every storm that covers the point. The waves grow from that wind as a real sea grows: higher the longer the open water upwind, the longer the wind has blown (up to three days of it), and the deeper the water. So a sea doesn't follow a gust: it builds behind a storm and dies down slowly after it. The current is the ocean's circulation, as on [Currents](Currents). How each is built, and from which measurements, is on [The science behind it](The-Science-Behind-It).

**On the server** the state is computed where you ask. It isn't free: growing the waves walks back through the wind's last three days. Ours read it twice a second for each vessel, which is as often as the sea changes. Read it on a timer of your own, not every frame.

**On a client** the state is read from a map the server sends: the nine map regions around its player, a cell every 32 blocks, refreshed every few seconds. A client blends the last two maps it received, so its answer trails the server's by a few seconds and is cheap to ask. Where it holds no map younger than half a game hour, a client knows nothing and answers zeros, a water temperature of zero included. A client mod reads the sea near its own player.

### What it answers

`ISeaWeather`:

| Member | What it is |
| --- | --- |
| `At(x, z)` | the sea state there, a `SeaState` |
| `CurrentAt(x, z)` | the current and the water's temperature there, a `SeaCurrent` |
| `DangerousOceans` | the world's [Dangerous oceans](World-Settings#dangerous-oceans) setting |
| `NowHours` | the game time, in total game hours |

`SeaState`:

| Field | Unit | What it is |
| --- | --- | --- |
| `WindX`, `WindZ` | m/s | the wind, toward east and toward south |
| `WindSpeed` | m/s | its strength |
| `WindFromDegrees` | ° | where it blows from: 0 north, 90 east |
| `Beaufort` | 0–12 | its force on the [Beaufort scale](Wind-and-Waves#the-beaufort-scale) |
| `WaveHeight` | m | the significant height of the sea: the mean of its highest third |
| `WavePeriod` | s | its peak period, the time between waves |
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

Three things to keep in mind when you use the numbers:

- **A height is the sea, not a wave.** Single waves scatter around `WaveHeight`: most lower, one in a hundred half again as high ([Every wave is different](Wind-and-Waves#every-wave-is-different)). A mod that throws waves at something draws them the same way, one per `WavePeriod`.
- **The wind is in m/s here.** Vanilla's `GetWindSpeedAt` gives the same wind on its own scale, 1.0 for 25 m/s ([Reading the weather](Reading-the-Weather)).
- **The state doesn't know about danger.** Wind, waves and currents run whether or not the oceans are dangerous. A mod that harms something by them reads `DangerousOceans` first, as ours do.

### A helm that shows the sea

A client mod that shows the sea at its player reads the state on a tick listener; once a second is plenty, as the map behind it changes every few seconds.

```csharp
var sea = capi.ModLoader.GetModSystem<SeaWeatherSystem>();
capi.Event.RegisterGameTickListener(_ =>
{
    var pos = capi.World.Player.Entity.Pos;
    var state = sea.At(pos.X, pos.Z);
    var current = sea.CurrentAt(pos.X, pos.Z);
    helm.Show(state.Beaufort, state.WindFromDegrees, state.WaveHeight, current.TowardDegrees, current.Speed);
}, 1000);
```

### Something that swims with the current

The current is a velocity in m/s. Vanilla's water drag slows whatever swims every tick, so adding the current's speed to an entity's motion once isn't enough, and adding all of it every tick is too much. Our `roughseas:currentdrift` adds, each tick on the server, as much of the current as the drag has just taken away, so the drag pulls the entity toward the current's speed rather than to rest (ours add a little of the wind too):

```csharp
var current = sea.CurrentAt(pos.X, pos.Z);
var carried = (1 - Math.Pow(physics.WaterDragValue, deltaTime * 33)) / 60;
pos.Motion.X += current.X * carried;
pos.Motion.Z += current.Z * carried;
```

`physics` is the entity's `EntityBehaviorPassivePhysics`, whose drag is applied 33 times a second; 60 turns m/s into blocks per tick. An entity of yours that moves by other physics adds the current to its own motion its own way.

Our behaviour carries vessels nobody steers ([Building a vessel](Building-a-Vessel#currents)). A vessel a player steers is moved by that player's client, which the server's drift doesn't reach, so a mod that sails its own vessels adds `CurrentAt` to its own motion.

## The drawn sea

The drawn sea exists only on a client:

```csharp
IDrawnSea? drawn = capi.ModLoader.GetModSystem<StormsModSystem>().DrawnSea;
```

`StormsModSystem` and `IDrawnSea` live in `RoughSeas.Storms`. `DrawnSea` is null on the server and before the client side starts.

### How it is drawn

Every half second a client reads the sea state around its player: the highest sea in the nine regions sets the waves' height and period, the wind at the player their direction. From those it draws a few trains of waves crossing near the wind's line, and eases toward a new sea over some seconds rather than jumping. Each place then draws its own share of that height, from the state there: a sheltered bay draws a lower swell than the open sea beside it, and a calm lake draws none.

Our own vessels are posed by the same calls this page describes, so a ship of yours that uses them rides exactly as ours do.

### Reading it

| Member | What it is |
| --- | --- |
| `Swell` | whether any swell is drawn now |
| `HeightAt(x, z)` | the surface's height above still water there now, in metres |
| `PoseOf(x, z, yaw, length, width)` | the pose of a hull there now, a `SeaPose` |

`PoseOf` samples the surface at the ends and sides of a hull of `length` by `width` blocks centred at `x, z`, and turns those four heights into how the hull would lie on them. So the hull's size does part of the work: a long hull on short waves has its bow and stern on different crests and barely rocks, while a small one takes every wave. Its length runs along the shape's X axis. `yaw` is its heading in radians as its renderer turns it: the entity's `Pos.Yaw` plus its shape's `rotateY`, which is in degrees, so `Pos.Yaw + rotateY * GameMath.DEG2RAD`.

`SeaPose`:

| Field | Unit | What it is |
| --- | --- | --- |
| `Hold` | 0–1 | how much of the drawn swell is under the hull: 1 where the sea runs as high as the highest drawn around the player, less where it runs lower, as in a sheltered bay, 0 where none is drawn |
| `Lift` | m | how far the surface lifts it, added to its model's height |
| `Roll` | rad | its roll, about its length, as the renderer's `xangle` |
| `Pitch` | rad | its pitch, about its width, as the renderer's `zangle` |

`Lift`, `Roll` and `Pitch` are the water's: where a hull lies on it exactly as the surface lies. Two things turn that into a hull that looks right:

- **Blend by `Hold`.** Where no swell is drawn, your vessel rocks however it rocked before. Ours keep vanilla's rocking there, and take the pose where the swell holds them fully, blending between by `Hold`. So a boat rowed from a calm river into the surf passes from one to the other without a jump.
- **Scale it as your hull would.** Ours multiply roll and pitch by a vessel's `rollResponse` and `pitchResponse`: a raft lies flatter on the water than its slope, a narrow canoe rolls more.

The pose is only the picture. The server knows nothing of it, and it changes as the waves move: read it every frame, in your render path.

### How a hull answers

A real hull doesn't take the water's pose at once. It has mass, and lifts, rolls and pitches toward the water on periods of its own: a light canoe follows a long swell as the water lies, while a heavy, deep hull lets a short chop pass under her and rolls slowly on her own. `HullResponse`, in `RoughSeas.Storms`, gives a hull those periods. Ours keep one per vessel and pass every pose through it; yours can do the same.

| Member | What it is |
| --- | --- |
| `HullResponse.Of(lengthMetres, beamMetres, draftMetres, depthMetres, config)` | the response of a hull of these dimensions, floating at this draft |
| `new HullResponse(heavePeriodSeconds, rollPeriodSeconds, config)` | one on periods you give; a period of 0 follows the water as it lies |
| `Follow(pose, seconds)` | the pose the hull takes `seconds` after the last call, chasing `pose`; where no swell is drawn it comes to rest at once |
| `HeavePeriodSeconds`, `RollPeriodSeconds` | its periods; set them as its draft changes |
| `HullResponse.HeavePeriod(draftMetres, config)` | the heave and pitch period of a hull floating this deep, in seconds |
| `HullResponse.RollPeriod(lengthMetres, beamMetres, draftMetres, depthMetres)` | its roll period, in seconds |

`Follow` returns the pose with its `Lift`, `Roll` and `Pitch` answered and its `Hold` as it was, so blend by it as before. Ours refresh the periods from the hull's draft every frame, so a laden hull answers slower than an empty one. A long gap between calls, as when a hull returns to the water, is caught up as one period at most, so the hull doesn't leap. Where the periods come from is on [The science behind it](The-Science-Behind-It#riding-the-swell).

`config` is the `response` block of our `config/storms.json`, reached as `StormsModSystem.Config.Response`: `heaveAddedMass`, the water a hull drags with it as it heaves, in its own masses (1); and `heaveDamping` (0.3) and `rollDamping` (0.1), how fast a swing dies, as shares of critical damping.

### A ship that rides our swell

A vessel that is an `EntityBoat`, or derives from one, needs none of this: give it `roughseas:seaforces` and, while it swims, it is posed as ours are ([Taking the sea](Building-a-Vessel#taking-the-sea)). Its seats, its selection box and its rider's position follow our pose whatever draws it; its hull follows when the renderer is vanilla's `EntityShapeRenderer`, or derives from it and calls the base `loadModelMatrix`. So an `EntityBoat` with a renderer of its own derives it from vanilla's, rather than posing itself against seats we already lift.

The calls above are for a vessel that isn't an `EntityBoat` and poses itself. Each frame, in its render path:

```csharp
var storms = capi.ModLoader.GetModSystem<StormsModSystem>();
response ??= HullResponse.Of(length, beam, draft, depth, storms.Config.Response);

var rotateY = entity.Properties.Client.Shape?.rotateY ?? 0;
var yaw = entity.Pos.Yaw + rotateY * GameMath.DEG2RAD;
var pose = response.Follow(storms.DrawnSea!.PoseOf(entity.Pos.X, entity.Pos.Z, yaw, length, beam), frameSeconds);

lift = (float)pose.Lift;
roll = (float)GameMath.Lerp(ownRoll, pose.Roll * rollResponse, pose.Hold);
pitch = (float)GameMath.Lerp(ownPitch, pose.Pitch * pitchResponse, pose.Hold);
```

The lift moves only what you draw with it. The server's vessel doesn't rise, so ours lift their seats and selection box by the same amount on the client, and take it back out of the position a rider's client sends the server. A vessel of yours that carries riders does the same, or its riders sit below a hull that rose.

What the vessel meets on the server, waves aboard and breakers on the hull, is the sea state's business, and our behaviours take care of it for any vessel that carries them.
