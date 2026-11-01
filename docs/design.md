# Rough Seas design

Normative; new work conforms to it. It is a living document: it shows the design as it stands now, and when the work finds a problem, a bottleneck or something we forgot, the design gets its next version and the change gets tickets.

## Purpose

Vintage Story's sea is flat, safe and empty; crossing it means holding W on a raft. Rough Seas makes the sea a place of danger and reasons to go out: weather and waves that matter, vessels that break by what they are made of, a body that tires and freezes in the water, creatures, and things to find below. Target: the current stable game, 1.22.7; multiplayer throughout. We build all of it ourselves, with no dependency on other mods, and it lives in large mod sets: version 2 is rebuilt so that mods changing the environment beside it keep working.

## Principles

1. **The ground under other mods.** Rough Seas changes vanilla itself, so that its storms, sea and dangers come out of vanilla as if the game had always had them. Other mods build on that ground and see vanilla's values through vanilla's channels; they never need to know we exist. We drive vanilla's own state rather than replace its answers. What lies below us (worldgen, climate, another mod's terrain) we read as the world's result, whoever built it. Where vanilla has no place for a thing (wind direction, waves, currents), we extend vanilla in its own form where it has one, and publish the rest through our own API. When another mod moves a lever vanilla itself has (a precipitation override, a temperature offset), our weather answers it as vanilla's would: a mod that makes the whole world dry gets a dry world. Our state (where a storm is, how strong, where it goes) stays a function of seed, climate as the game computes it (worldgen climate and the season's temperature, with every mod that moves it) and time; its expression through vanilla (thunder, lightning, rain, snow) reads vanilla's live conditions, as vanilla does. A module that detects another mod and adapts to it is the last resort, for a mod that sits in the very machinery we change.
2. **Real-world data.** Mechanics follow how the real world works, with real numbers: wood and rope strength, the Beaufort scale, wave height from wind, cold-water survival tables, the size and behaviour of animals. One block is one metre. Realistic but cheap: model what really decides the outcome, the way the real world does it, at the least cost to server and client that keeps it true, and do it well the first time so nobody has to come back to it. What has no real counterpart follows vanilla's lore and behaviour. Balance is tuned afterwards through constants and configs.
3. **One global state, zoomed in by distance.** A single source of truth. The coarse level computes the global state; detail is computed as you get closer, for nearby chunks as they become visible; physical effects only right around a player. Detail is a zoom into the one state, never a separate model, and it starts before a player crosses into a chunk so the state is ready on arrival. Every player in multiplayer is a centre of detail. The model is Egosoft's X series.
4. **Determinism from the seed.** The coarse state is a function of world seed, position and time, laid over the world's own worldgen: storms are born at real rates per day and area, depressions in the storm-track belt and the stronger the steeper the season's temperature gradient, hurricanes over warm tropical seas. It is never stored, only what players change is. The server alone computes it; clients read it as a map the server sends them, so there is one source of truth and a client needs no generator.
5. **Systems over scripts.** Mechanics never know each other directly. They read and change the world state through interfaces: wind raises waves, waves make surf, surf strikes the hull, the hull fails by its material. No rule says "a storm breaks the boat".
6. **Modules with clear boundaries.** Written as if it were our own game: every system its own module, every class a clear responsibility, a well-defined interface, private internals. A module can later be extracted as its own mod by moving it, not rewriting it.
7. **Data apart from code.** Every number lives in JSON assets, as in the game itself; players and other mods tune them through patches. A vessel type is data: hull, materials, size, capacity, propulsion.
8. **Fair danger.** Every threat shows itself first, the way nature does: treetops sway, the sky darkens, the swell rises, the fin shows before the bite.
9. **Performance budget.** Every system has a per-tick server budget, and it is measured.
10. **Save safety.** The mod can be added to an existing world, updated or removed without breaking it. Mod data is versioned.

## The system

```
seed · climate · biome · time
        │  deterministic, computed on the server, sent to clients as a map
        ▼
sea and weather state: wind (direction, strength), sea state, storms
        │  coarse everywhere → detailed near players → physical at the player
        ▼
┌───────────┬──────────────┬───────────┬──────────────────────┐
│ vessels   │ body in water│ creatures │ underwater and seabed│
└───────────┴──────────────┴───────────┴──────────────────────┘
        │  each reads the state and the others' public interfaces
        ▼
player-caused state (vessel damage, wrecks, salvage): stored and synced
```

### Sea and weather

The global state of wind with direction and strength, of sea state, and of storms, derived from the seed and laid over climate and biome. Vanilla's wind is one strength per region with no direction, and its waves are shader-only, so this model is ours. It exposes the state at any position and time and the detail around a player through `ISeaWeather` (`SeaWeatherSystem`): `At(x, z[, totalHours])` gives wind in m/s, Beaufort force, wave height in metres, oceanity and storm presence; `StormsWithin` lists storms in range. It owns the world settings: a "Rough Seas" playstyle listed first on the new-world screen, with more ocean in the landcover (a mod can't change vanilla's landcover default), and a "dangerous oceans" toggle. With dangerous oceans off the sea keeps its weather and does no harm; each danger module reads the flag. Vanilla's visible weather follows this state. Today it does so by overriding vanilla's answers (wind handler, rain, events); version 2 moves it under vanilla, layer by layer.

Version 2's weather layer is a weather map under vanilla: wind with direction and strength, cloud, precipitation and storms, a field over the whole world like a forecast map. It stands on two foundations it never changes: worldgen (where the sea is) and climate (temperature and its zones, which other mods move). The ocean map is the world's own, never built by us: a built region's `OceanMap`, and for places not built yet the world's own generator, which answers what the world will get there. Detail follows principle 3. Far away nothing runs continuously; the model answers when asked, and knows only that ocean is there. Near players, waves come from the water body in the built world, by wind, fetch and depth, with no sea threshold, so a big shallow lake has waves too. Fetch is measured on the game's own horizontal scale, the compression vanilla already gives its climate zones: real pole-to-equator distance (10,000 km) over the world's `polarEquatorDistance`, ×200 by default, so it follows each world's setting. Our storm sizes already sit on that scale. Depth, heights and the player stay at one block per metre. The wind's blowing time limits the sea too: it becomes an equivalent fetch and the shorter fetch applies, so a sudden squall raises a young short sea and a long storm a long high one. For now duration comes from the mean wind at a point (#97 tunes how a storm's sea builds across its field). Above a knee, wave height bends smoothly toward a ceiling it never reaches, since the formula's hurricane peaks run twice the real ones; knee and ceiling are balance constants in JSON. The coarse layer is a coarsening of the fine one: where both apply they agree, so its "ocean here" reads the world's map as the world itself builds it, calibrated against the built world. Its state is laid over worldgen climate and the season's temperature as the game computes it; its expression reads live temperature (principle 1). Vanilla stops rolling dice and reads the map as its inputs; its own machinery expresses it, and every mod reading vanilla stands on the map in turn. Weather events stay vanilla's: each event of vanilla or any mod fires where its own declared conditions hold against the map's rain and live temperature, so hail falls in a cold downpour, thunder in a warm storm, and nothing under a calm sea, with no list of other mods' events. Trees lean only along one axis because vanilla's foliage shader takes no direction, so they follow strength and everything else follows our direction. A day is a day: storm sizes, speeds, lifetimes and birth rates per day are real, and only the calendar is the game's, so the seasons run about 3.4 times faster than the real year. Every storm number is taken from measurements, with its source in `seaweather.json`.

Ocean currents are part of the same state: a simplified gyre model over the climate (hoblin chose it over a full simulation), steady in time, derived from seed and position. `ISeaWeather.CurrentAt(x, z)` gives the current in m/s with its speed and heading, the water temperature and the current's warmth. Currents are state, not danger, so they ignore the dangerous-oceans flag. Unridden vessels drift with the current through the `roughseas:currentdrift` behaviour; ridden ones are driven by the client, and sailing takes them. Water temperature has no seasonal cycle yet; body in water settles it.

### Vessels

Every watercraft is one system; a vessel type is data. Integrity comes from the materials it is built from (wood species, the rope that lashes it, hide, later metal) and from what strikes it: impact speed against shore and rock, surf, wind on a mast, a smashing wave. A knock cracks the hull where it lands, and a crack leaks; a vessel is usually lost to flooding, floating awash or sinking by real buoyancy (draft and freeboard by Archimedes from one full mass, density from the materials table). Breaking up into wreckage takes severe damage. A sunk vessel lies on the bottom with its cargo, and can later be raised. Vanilla vessels already remember their wood; strength values are ours.

The roster, each with its own niche rather than a step on a ladder:

- **Raft**: a barge, cargo downriver; slow, steady on calm water.
- **Reed boat**: a one-way crossing; quick to build, waterlogs within days.
- **Dugout**: the workhorse of rivers and lakes; the toughest hull, heavy, tippy in a sea.
- **Coracle**: fishing and crossings; one person, spins on the spot, carried on the back.
- **Birch-bark canoe**: long river journeys; fast, shallow, runs rapids, fragile, repaired on the way.
- **Hide currach**: the first venture to sea under oars; flexes with the sea, carries cargo, needs tallow.
- **Outrigger canoe**: a long crossing of open sea; fastest before metal, sails, little cargo.
- **Vanilla sailboat**, then **metal boats**.

How the roster's niches play (#17): upkeep is physics. Reed soaks up water and sinks in about three days; it can't be mended, but drying it ashore lifts it again, a little less each time. An ungreased currach seeps at the seams, and rendered fat stops it; holes mend with hide. Wooden hulls barely wear: knocks and cracks are what kill them. A raft's knocks harm its lashings, not its logs. The coracle rides in the backpack slot, on the player's back. Other small vessels pick up into the inventory, and the dugout and outrigger can't be picked up. In rapids every vessel takes a rock strike each second, except the bark canoe. Forward and turning speed are separate, which is what makes the coracle spin and the outrigger clumsy.

Seaworthiness (#42) is per-vessel data from real craft: freeboard, heave, the breaker height that capsizes it, how it rolls and pitches. Vessels with a hull hold water and are bailed (dugout, currach, outrigger, bark canoe, coracle); the raft and reed boat wash through. A capsize throws the rider. A breaker strikes with the energy of the wave actually drawn, so most hits are milder than the significant height and a few are much worse. An anchored vessel ships less water and takes milder strikes, but until #7's weathervaning it capsizes as if beam-on. Roll is vanilla's boat motion scaled per vessel, until #37's sea surface takes it over.

Cost matches benefit: of two vessels in one niche, the better costs more (a knife and reed against an axe and timber; hide is dearer than wood).

A vessel's condition is per place on its hull and survives being picked up and placed again. Every kind of damage reaches the hull: impacts, surf, weapons, lightning, blasts, each through the same roll by material. Repair patches one place, a right-click holding the vessel's own material: a full mend costs half the build materials, as vanilla mends armour, and each patch costs satiety, which is the work.

Mooring. A raft ties to a stake with vanilla's rope to a fence post. The sailboat anchors with vanilla's stone anchor, which we give real holding. Its own rode is the rope its recipe spends (12 × 10 m = 120 m of flax). Extra rode is one anchor warp, 9 ropes spliced into 90 m and uncrafted back into ropes, stowed in the sailboat's lower mast storage, which holds a single item. Lowered, the anchor reaches the seabed and pays out five times the depth, capped by the rope aboard (up to 210 m). With a shorter scope it holds less and drags in a storm. Deeper than the rope reaches, it hangs and holds nothing until the vessel drifts over shallower water. A rode snaps by its material, and the anchor is lost with it, along with the warp's rope that was paid out. A vessel held at one point will weathervane bow into the wind (not yet: it comes with #7). With no player near, the chance that a moored vessel is thrown ashore comes from the storm's strength and its distance to the shore.

Sailing reads the wind: a sail works with the wind's direction, rowing on one side turns the vessel, a windsock shows the wind, and you can be stranded until it turns.

### Body in water

A person is one module behind an interface: the body, its mass and its volume by height from measured anatomy, and what it carries. Vessels, the swimmer and any other system ask it what a player weighs and displaces. One reference body serves every player, the same in a boat and in the water. What is carried is weighed by a game convention: the hotbar is the belt, what the person has at hand, and the backpack is the store, which the game lets one person carry as many people would in life. So armour and the tools and weapons in the hotbar are weighed, each by a real analogue; materials, even in the hotbar, and everything in the backpack are not. The body floats by the water it displaces under the drawn surface. Extra weight doesn't sink a swimmer but makes them work, and stamina pays for it.

Swimming drains a stamina whose rate depends on water temperature and on how well the player has eaten; exhausted, you sink. Cold kills at a rate set by the biome's climate and the current temperature, from real cold-water survival data; vanilla's flat rate is replaced. Diving builds on this: the Jonas-tech diving suit gives air and light, and armour and lanterns stop being a free ticket to the seabed.

### Creatures

Sharks hunt swimmers and attack small vessels and dodge melee; whales; rust creatures on the seabed. They read the sea state and the vessel and body interfaces like any other system.

### Underwater and seabed

Shipwrecks as a rare structure on the deep seabed (vanilla places nothing there), their maps from a treasure-hunter trader built on vanilla's existing maps, harpoons, depth sounding, and raising your own vessel and belongings.

## Multiplayer

The server owns all state that players change, and computes the seed-derived state too. Clients get the sea and weather around them as a map, the way vanilla syncs its weather: a snapshot in the map region, which the game sends with it, and live updates over our channel to players within a region. Each cell carries wind, its direction, current, and wave height and period; a client reads the value at its point. Clients show; they decide and compute nothing.

## Work order

**Version 2, compatibility**, before any new mechanic: each layer the mod touches (worldgen, climate, weather, water blocks, rendering, vessel entities, materials, the player's body, our API) is decided in turn: what we take from below, what we change, what we hand up.

Two clones, two lanes, few crossings in code:

- **Sea**: sea and weather (with world settings) → storms and waves → sailing → metal boats.
- **Vessels and materials**: vessel integrity and the roster → body in water → creatures → underwater → seabed.

Crossings: storms and sailing break vessels, so they use vessel integrity; metal boats extend the material tables and use sailing; the seabed uses wreckage from vessels and diving from underwater. The material tables belong to the vessels lane.

## Open questions

Parked by hoblin, to settle before the module they touch:

1. **Time scale.** A game day is about 48 real minutes. Do real rates run on the real clock or the game clock? One suggestion: the player's body on the real clock, weather and sea on the game clock. Settled for weather and sea (#99): a game day is a real day, and only the calendar is compressed.
2. **Where realism breaks play.** A squall that smashes a vessel within a minute is true to life but leaves no chance. Fair danger (principle 8) covers the warning; how far the numbers bend is open.
3. **What has no real counterpart.** Rust creatures, Jonas tech, temporal storms: vanilla's lore and behaviour, case by case.
4. **Other mods' weather in our state.** Their weather reaches how our storms show (principle 1). Whether it may also change our state itself (another mod's storm raising our waves) is open: its state would become an input to our model, which then is no longer a function of seed, position and time alone (principle 4). Partly settled (#99): a mod that moves temperatures moves our storms' strength, through the game's own climate chain; another mod's storms still don't enter our state.
