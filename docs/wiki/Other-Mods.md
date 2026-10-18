Rough Seas drives the game's own weather rather than replacing it, so most mods built for plain Vintage Story work beside it with nothing to do (see [For modders](For-Modders)). These are the mods we have tested it with, on Vintage Story 1.22.7: what changes when they share a mod set, and what we recommend. This page is for server owners and modders.

| Mod | Version | Verdict |
| --- | --- | --- |
| [Dynamic Seasonal Temperature](#dynamic-seasonal-temperature) | 1.4.0 | Compatible. |
| [Better and Improved Weather Continued](#better-and-improved-weather-continued) | 2.0.5 | Compatible. |
| [Realistic Water](#realistic-water) | 0.4.0 | Compatible. |
| [Realistic Rapids](#realistic-rapids) | 0.1.3 | Compatible. |
| [Extreme Weathers](#extreme-weathers) | 1.8.5 | Compatible, with changes on both sides. |
| [Terra Prety](#terra-prety) | 7.11.2-pre.1 | Compatible at world height 448. |
| [Wildcraft: Trees and Shrubs](#wildcraft-trees-and-shrubs) | 1.3.4 | Compatible: its woods build our vessels. |

Running Rough Seas with a mod not listed here? Tell us how it went on the [issue tracker](https://github.com/hoblin/rough-seas/issues).

## Dynamic Seasonal Temperature

[Dynamic Seasonal Temperature](https://mods.vintagestory.at/show/mod/69562) 1.4.0. Compatible: each mod works as it does alone.

## Better and Improved Weather Continued

[Better and Improved Weather Continued](https://mods.vintagestory.at/biwc) 2.0.5. Compatible: each mod works as it does alone.

## Realistic Water

[Realistic Water](https://mods.vintagestory.at/realisticwater) 0.4.0. Compatible: each mod works as it does alone.

## Realistic Rapids

[Realistic Rapids](https://mods.vintagestory.at/realisticrapids) 0.1.3. Compatible: each mod works as it does alone.

## Extreme Weathers

[Extreme Weathers](https://mods.vintagestory.at/show/mod/58501) rolls hurricanes, monsoons, blizzards, sandstorms, heat waves and cold snaps of its own, and each one covers the whole world at once. Rough Seas brings hurricanes, downpours and snow too, and ours travel across the map. In one mod set, Extreme Weathers stops rolling the storms that are ours and becomes how they look and what they do.

### Tested versions

Rough Seas works with Extreme Weathers **1.8.5**. It reaches into Extreme Weathers' own code, so it does so only on a version it was tested against. On any other version it logs a warning and leaves Extreme Weathers alone: both mods then roll their own storms side by side.

### Storms that are ours

Extreme Weathers' own hurricane, monsoon and blizzard rolls are off. Our [storms](Storms) bring the hurricane, the downpour and the snow instead. The change is made in memory when the server starts, so `extremeweather.json` in your `ModConfig` folder stays as you wrote it. `HurricanesEnabled` stays on, since it also switches Extreme Weathers' storm damage.

A server admin can see the settings as they stand with `/roughseas extremeweather`.

### What rides our weather

Most of Extreme Weathers reacts to the wind and rain where a player stands, not to its own events. Under our storms:

- **Damage.** Windows shatter and doors, thatch and leaves tear loose once our wind passes 1.3 (about 33 m/s: a hurricane near its eye wall, or the rare gale that reaches hurricane force).
- **Howl and shoving.** The wind howls, items blow away and animals get pushed, at the strengths Extreme Weathers sets.
- **Snow.** A cold storm of ours piles Extreme Weathers' deep snow.
- **Tornadoes.** Inside the core of our hurricane, within about a fifth of its radius of the eye, a tornado can come down. The roll happens every five minutes at Extreme Weathers' own `TornadoChance`. The tornado's world-wide storm sky is off: our hurricane keeps its own clouds.

All of it happens around the players inside the storm. A player far from it sees calm weather.

#### The wind cap

Extreme Weathers caps every wind at 1.05, which is below the 1.3 its own hurricane damage needs. Beside Rough Seas, the cap holds only while one of Extreme Weathers' own events runs, so our hurricane reaches that damage.

### What stays Extreme Weathers'

Sandstorms, heat waves and cold snaps still roll as Extreme Weathers decides. They act through levers the game itself has, and Rough Seas answers them as vanilla would:

- A sandstorm dries the world, our storms included: no rain falls under one.
- A heat wave or a cold snap shifts the temperature our storms read. A cold snap can silence a storm's thunder and [lightning](Lightning), since both need warm air.

Extreme Weathers' own weather patterns and events (`supercell`, `torrential`, `zephyr` and the rest) join the game's rotation as usual and play [by their own conditions](For-Modders#weather-events-fire-by-their-own-conditions).

### No whiteout

Extreme Weathers' whiteout needs precipitation above 0.95 at −2 °C or colder. Our coldest storm, the gale, tops out at 0.7, so beside Rough Seas there is no whiteout. That stays as it is until our storms are tuned to real climate data ([#107](https://github.com/hoblin/rough-seas/issues/107)); a whiteout should come from a real blizzard, not from a number raised for one mod.

## Terra Prety

[Terra Prety](https://mods.vintagestory.at/terraprety) 7.11.2-pre.1 redraws the world's continents and coasts. Rough Seas reads the world it builds as it is, with no code of its own for Terra Prety.

**Set world height 448** when you create the world, as Terra Prety's author recommends. How deep its seas are depends on the world height:

- At the default height of 256, Terra Prety's seas are only a few blocks deep, much of them marsh at sea level. Our storms raise only about a metre of sea there.
- From 320 up, vanilla's ocean floor returns. At 448 the open sea is about 90 to 96 blocks deep, with Terra Prety's shores, barrier islands and lagoons in front of it.

## Wildcraft: Trees and Shrubs

[Wildcraft: Trees and Shrubs](https://mods.vintagestory.at/wildcrafttree) 1.3.4 (it needs Herbarium) adds 44 woods. Beside Rough Seas, 43 of them build a [dugout](Dugout) and an [outrigger canoe](Outrigger-Canoe), the same way vanilla's woods do. Petrified wood builds none: it is stone in a tree's shape and doesn't float, and Wildcraft keeps it out of its own boats too.

### What its woods do in our vessels

- **Built** from the wood's own logs and planks, in the crafting grid as vanilla's are. Its seven giant trees, whose logs come as sections, build from those sections as redwood does. The [hide currach](Hide-Currach) takes its planks too.
- **Look.** A vessel wears its wood's own bark and timber, taken from Wildcraft's log of that tree.
- **Named** after the wood, by the name Wildcraft gives it: an elm dugout, a mahogany outrigger canoe.
- **Mended** with that wood's own logs or planks, as for a vanilla wood; a broken outrigger leaves that wood's planks.
- **Strength** comes from the real tree each wood stands for, by the same table as vanilla's woods ([Hull condition](Hull-Condition#what-it-is-made-of)):

| Wood | Real tree | MPa |
| --- | --- | --- |
| dalbergia | African blackwood | 214 |
| azobe | ekki | 196 |
| mangrove | red mangrove | 190 |
| leadwood | leadwood | 145 |
| umnini | pink ivory | 138 |
| jacaranda | Brazilian rosewood | 135 |
| eucalyptus | blue gum | 135 |
| ohia | ʻōhiʻa | 126 |
| guajacum | lignum vitae | 124 |
| tigerwood | goncalo alves | 117 |
| sapele | sapele | 111 |
| beech | European beech | 110 |
| ash | European ash | 104 |
| honeylocust | honey locust | 101 |
| yew | European yew | 97 |
| kauri | kauri | 87 |
| douglasfir | Douglas fir | 86 |
| linden | European lime | 85 |
| mahogany | big-leaf mahogany | 81 |
| sycamore | American sycamore | 69 |
| horsechestnut | horse chestnut | 68 |
| fir | silver fir | 66 |
| elm | English elm | 65 |
| catalpa | catalpa | 65 |
| poplar | black poplar | 64 |
| spruce | Norway spruce | 63 |
| willow | white willow | 56 |
| redcedar | western red cedar | 52 |
| tuja | northern white cedar | 45 |
| empresstree | paulownia | 38 |

The rest (cedar, which is Siberian pine, ginkgo, banyan, bearnut, tamanu, spurgetree, ghostgum, satinash, sal, bluemahoe, saxaul, charred and chlorociboria) have no published bending strength we could source, and count as an unknown wood: 93.5 MPa, the middle of the vanilla woods. The server log names each of them once.

### Canoes of elm and spruce bark

The [birch-bark canoe](Birch-Bark-Canoe) is built of the barks real canoes were made of: birch, elm and spruce. Wildcraft has elm and spruce, so their logs give bark with a knife, and the bark builds an elm-bark or a spruce-bark canoe, mended with the same bark. Its other barks build no canoe.

### Its own boats

Wildcraft's raft and sailboat stay as Wildcraft made them. Rough Seas' hull condition, mending and sea forces reach only vanilla's boats and our own.

### For server owners and modders

- Vessels of Wildcraft's woods carry its name in their codes, `roughseas:dugout-wildcrafttree-elm`, so two mods with a wood of the same name never meet. Vanilla's woods keep their codes, `roughseas:dugout-oak`.
- Take Wildcraft out of a world and the vessels built of its woods go with it, as its own blocks do.
- Any other mod's trees work the same way, with nothing to write for it. Rough Seas reads every mod's `worldproperties/block/wood.json`, and builds from the blocks a wood has in vanilla's shapes: `log-placed-{wood}-ud` and `-we`, `plank-{wood}`, `debarkedlog-{wood}-ud` and `logsection-placed-{wood}-ne-ud`, in that mod's own domain. A mod's birch, elm or spruce gives canoe bark.
