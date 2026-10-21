Rough Seas keeps one model of the world's wind, waves, storms and currents, computed on the server from the world's own climate and ocean map. The rest of the game meets that model in three ways, and a mod meets it in the same three.

![Rough Seas lies under vanilla: the world's ocean map and climate at the bottom, Rough Seas' map on the server above them, vanilla's weather machinery driven by it, and other mods reading vanilla as usual at the top; clients read the map the server sends](images/diagrams/under-vanilla.svg)

- **Through vanilla.** We don't replace the game's weather; we drive it. Each region's wind pattern, the rain and the weather events still come out of vanilla's machinery, set from our model. So a mod written for plain Vintage Story reads our weather without a line about us. A mod that moves a lever vanilla itself has, drying the world or shifting its temperature, gets the answer vanilla would give; a mod that sets the weather in vanilla's place changes what vanilla reports, not our sea ([Reading the weather](Reading-the-Weather#what-moves-our-sea)).
- **Through our own code.** Vanilla has no place for the wind's direction, for waves or for currents. We publish them from our own types, on both sides: the sea state, and on a client the drawn sea, its picture of the waves.
- **Through behaviours.** A vessel takes our sea by carrying our entity behaviours, configured in JSON: it floats by its mass, ships water, cracks, floods, moors and drifts. No code.

## The pages

Each page covers one part, for the mod that touches it; read the ones yours does.

| Your mod | Read |
| --- | --- |
| reads the wind, rain or temperature, or changes them | [Reading the weather](Reading-the-Weather) |
| adds weather events | [Weather events](Weather-Events) |
| sets a region's wind pattern | [Wind patterns](Wind-Patterns) |
| reshapes the oceans, or calls vanilla's ocean layer | [The ocean map](The-Ocean-Map) |
| reads the wind's direction, waves, storms or currents: a helm, a sail, a fish | [Reading the sea](Reading-the-Sea) |
| poses a vessel of its own on the swell a client draws | [Reading the sea](Reading-the-Sea#the-drawn-sea) |
| adds a vessel, or gives ours its behaviours | [Building a vessel](Building-a-Vessel) |

Where a page touches physics, it says what the mod does and links [The science behind it](The-Science-Behind-It) for why.

## What we keep

What these pages document is what we keep: vanilla's channels as they describe them, `ISeaWeather` with `SeaState` and `SeaCurrent`, `IDrawnSea` with `SeaPose` and `HullResponse`, and our vessel behaviours with their keys and their configs. It follows semantic versioning: a change that breaks any of it comes with a new major version and a line in the release notes, and what we add comes in a minor version. If you build on it, or need something it doesn't give, tell us on the [issue tracker](https://github.com/hoblin/rough-seas/issues).

### Hulls that crack, leak and flood

Hulls that crack, leak and flood changed the vessel keys, so they came as a major version. What a vessel of yours built before them needs to change:

- `roughseas:vesselintegrity` loses `wearPerDay`: a reed hull's [`soak`](Building-a-Vessel#the-hull) and a cracking hull's `seep` take its place. A mend's `restores` now closes that much of one place's crack, or of a lashed hull's lashing; with `greases`, it is the share of `seep.greaseDays` it gives back.
- `roughseas:bilge` loses `sinks`: a swamped vessel sinks by its [buoyancy](Building-a-Vessel#floating).
- `roughseas:seaforces`' `freeboardMetres` counts only for a vessel that doesn't float by its mass, one with no `depthMetres` on its `vesselintegrity`.
- `config/vessels/impact.json` is now `config/vessels/mass.json`, with `crewKg` added; a patch to it follows the rename.
- A carried vessel's hull travels on its item as the stack attribute `roughseas:vessel`; `roughseas:worn` is gone.

New: the hull `model`, `parts[].share`, `depthMetres`, `waterplaneSquareMetres`, `seep`, `soak`, the `density` of a material, `config/vessels/hull.json`, the `response` block of `config/storms.json`, and [`HullResponse`](Reading-the-Sea#how-a-hull-answers). A world from before keeps its vessels: a raft takes its old condition as its lashing, a reed boat as water soaked up, and a cracking hull starts sound; a vessel carried as an item does the same from its durability.

### Blows by their physics

In 3.1 every blow meets what really takes it ([The hull](Building-a-Vessel#the-hull)). No key was removed, so a vessel of yours loads as it did, but it fares differently:

- `impactCapacity` is what a broad load (a knock, a ram, rapids, a blast) must reach to break the hull up, and a smaller one cracks its place by its share; on a `reed` hull too, which now tears.
- Two hulls with `vesselintegrity` ram each other; `minImpactSpeed` is the speed they take in their stride.
- Without a `shell`, the frame's capacity resists point blows and cuts too, and breakers, now a pressure against the planking, don't harm the hull. Give it a [`shell`](Building-a-Vessel#the-hull) for both.
- `Struck`'s second value is the blow over what resisted it, which is the shell for a point blow, a cut or a breaker, no longer always the hull's capacity. Its knock is `Harmed` only when the place came to a worse stage.

New: the vessel's `shell`, `config/vessels/damage.json`'s `broadSources`, `cutShare` and `variation`, a material's `toughness` and `cutToughness`, and `hull.json`'s `soakedStrengthShare`. Gone, never documented here: `config/storms.json`'s `waves.breakerTransfer`, `waves.periodSecondsPerRootMetre` and `ashore.impactFactor`.

## What not to touch

| Name | What it is |
| --- | --- |
| `roughseas:seamap` | the map region moddata, and the network channel that carries the map to clients |
| `roughseas:oceancalibration` | save data: what the world's ocean map means |
| `roughseas:vessels` | the network channel a rider's client reports its vessel's knocks over |
| `roughseas-force*` | our wind patterns, which our model sets; to hold a region's wind, set a pattern of your own ([Wind patterns](Wind-Patterns)) |

## Adding and removing Rough Seas

The mod can join an existing world, and leave one. Without it the world opens cleanly: each region keeps the strength of the wind we last set for up to a game hour, then vanilla rolls its own pattern.
