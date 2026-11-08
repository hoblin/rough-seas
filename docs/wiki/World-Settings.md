Rough Seas works in every world it is added to: the wind, [storms](Storms), [currents](Currents) and the [condition of every hull](Hull-Condition) come with the mod, whatever the world was made with. The new world screen decides how much sea there is, and a world setting for each mechanic decides which of them your game has.

## The Rough Seas playstyle

Since Vintage Story 1.21 a new world is mostly land, with only a few percent of ocean. The *Rough Seas* playstyle on the new world screen makes a world with far more open water: it starts from the survival playstyle and lowers the land cover to 60%. It also sets 100,000 blocks from pole to equator, twice the default, so the climate zones are twice as wide and a block of open water counts as 100 m of real fetch instead of 200 ([the world's scale](Wind-and-Waves#the-worlds-scale)). The rest is a plain survival world with harsh winters and realistic climate, so the latitude decides the [wind](Wind-and-Waves) and the [storms](Storms).

A world made with any other playstyle still gets every mechanic of the mod and every [vessel](Vessels); it simply has less sea to cross.

## The mechanics

Each mechanic of the sea has its own world setting, under *Rough Seas* on the new world screen. All are on by default. Turn one off to leave that mechanic out of your game; the others play as before. Wind, storms, rain and the sea itself stay either way: a setting stops what a mechanic does to you and your vessels, not the weather.

| Setting | Code | With it off |
| --- | --- | --- |
| *Water in the bilge* | `bilgeWater` | No [wave](Surf-and-Swamping) comes aboard and no rain falls into a [bilge](Bailing). A leak still fills it. |
| *Hull damage* | `hullDamage` | Nothing harms a hull: no [knock](Hull-Condition#knocks-and-landings) on rock or shore, [ram](Hull-Condition#rams), [rapids](Rapids), breaker or weather. Hide doesn't rot, ungreased seams don't seep and reed doesn't soak ([seeping and soaking](Hull-Condition#seeping-and-soaking)). Every hull is mended whole when the world loads, or when you turn the setting off. Weapons and explosions still harm a hull. |
| *Lightning* | `stormLightning` | Storms bring no [lightning](Lightning) of their own, and masts draw none. |
| *Mooring strain* | `mooringStrain` | The sea puts no strain on a mooring: no [anchor](Anchoring) drags, no [rope snaps](Mooring#tying-up), and no vessel is [thrown ashore](Mooring#thrown-ashore) while you are away. |
| *Currents* | `oceanCurrents` | [Currents](Currents) and the wind carry no vessel left alone. The currents still flow, and the water keeps its temperature. |
| *Rower fatigue* | `rowerFatigue` | [Rowing, paddling and poling](Paddles-Poles-and-Oars) cost no satiety. |
| *Pole's depth* | `poleDepth` | A [pole](Paddles-Poles-and-Oars) pushes in water of any depth, as it does at its full stroke. |

To change a setting in a world already made, open the world's settings from the singleplayer list, or type `/worldconfig CODE false` (or `true`) and reload the world. The setting stays as you left it.

A world made before version 4.1.0 had one setting, *Dangerous oceans*. On its first load it gives every setting above its value, on or off; from then on you change them one by one.

A swimmer breathes by the waves you see whatever the settings: a crest over your head takes your air, and a trough gives it back. Drowning is the game's own. Cold water and swimming stamina are planned for [The living sea](Roadmap#the-living-sea).
