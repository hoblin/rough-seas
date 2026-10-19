Rough Seas gives the world weather of its own, but it doesn't replace the game's weather. It drives it. Wind, rain and weather events still come out of vanilla's own machinery, so a mod written for plain Vintage Story works next to Rough Seas without a line about it. What vanilla has no place for, the wind's direction, waves and currents, we publish from our own code, and a vessel takes our sea through behaviours set in JSON. These pages are for a mod's author: each one covers one part, so read the ones your mod touches.

![Rough Seas lies under vanilla: the world's ocean map and climate at the bottom, Rough Seas' map on the server above them, vanilla's weather machinery driven by it, and other mods reading vanilla as usual at the top; clients read the map the server sends](images/diagrams/under-vanilla.svg)

## The pages

**Weather and climate**
- [Reading the weather](Reading-the-Weather): wind, rain and temperature through vanilla's own calls, and the levers, handlers and patches we follow.
- [Weather events](Weather-Events): how vanilla's events, and yours, fire under our weather.
- [Wind patterns](Wind-Patterns): our pattern in every region, and setting one yourself.

**Worldgen**
- [The ocean map](The-Ocean-Map): the world's own ocean layer, and a mod that reshapes the oceans.

**The sea from code**
- [Reading the sea](Reading-the-Sea): the wind's direction, waves, storms and currents at any point.

**Vessels**
- [Building a vessel](Building-a-Vessel): making a vessel take our waves, ship water, wear, break, moor and drift.

## What we keep

What these pages document is what we keep: vanilla's channels as they describe them, `ISeaWeather` with `SeaState` and `SeaCurrent`, and our vessel behaviours with their keys. It follows semantic versioning: a change that breaks any of it comes with a new major version and a line in the release notes, and what we add comes in a minor version. If you build on it, or need something it doesn't give, tell us on the [issue tracker](https://github.com/hoblin/rough-seas/issues).

## What not to touch

| Name | What it is |
| --- | --- |
| `roughseas:seamap` | the map region moddata, and the network channel that carries the map to clients |
| `roughseas:oceancalibration` | save data: what the world's ocean map means |
| `roughseas-force*` | our wind patterns; setting one by hand works, but the next lease renews it |

## Adding and removing Rough Seas

The mod can join an existing world, and leave one. Without it the world opens cleanly: regions keep our last wind pattern until it runs out, then vanilla rolls its own.
