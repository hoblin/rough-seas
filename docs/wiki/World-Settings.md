Rough Seas works in every world it is added to: the wind, [storms](Storms), [currents](Currents) and the [condition of every hull](Hull-Condition) come with the mod, whatever the world was made with. The new world screen decides how much sea there is, and a world setting for each mechanic decides which of them your game has.

## The Rough Seas playstyle

Since Vintage Story 1.21 a new world is mostly land, with only a few percent of ocean. The *Rough Seas* playstyle on the new world screen makes a world with far more open water: it starts from the survival playstyle and lowers the land cover to 60%. It also sets 100,000 blocks from pole to equator, twice the default, so the climate zones are twice as wide and a block of open water counts as 100 m of real fetch instead of 200 ([the world's scale](Wind-and-Waves#the-worlds-scale)). The rest is a plain survival world with harsh winters and realistic climate, so the latitude decides the [wind](Wind-and-Waves) and the [storms](Storms).

A world made with any other playstyle still gets every mechanic of the mod and every [vessel](Vessels); it simply has less sea to cross.

## The mechanics

Each mechanic of the sea has its own world setting, under *Rough Seas* on the new world screen. All are on by default. Turn one off to leave that mechanic out of your game; the others play as before. Wind, storms, rain and the sea itself stay either way: a setting stops what a mechanic does to you and your vessels, not the weather.

| Setting | Code | With it off |
| --- | --- | --- |
| *Water in the bilge* | `bilgeWater` | No [wave](Surf-and-Swamping) comes aboard and no rain falls into a [bilge](Bailing). A leak still fills it, and so does the sea when a vessel lies awash or sunk. |
| *Hull damage* | `hullDamage` | Nothing harms a hull: no [knock](Hull-Condition#knocks-and-landings) on rock or shore, [ram](Hull-Condition#rams), [rapids](Rapids), breaker or weather. Hide doesn't rot, ungreased seams don't seep and reed doesn't soak ([seeping and soaking](Hull-Condition#seeping-and-soaking)). Every hull is mended whole when the world loads, or when you turn the setting off. A knock is still heard, and weapons and explosions still harm a hull. |
| *Lightning* | `stormLightning` | Storms bring no [lightning](Lightning) of their own, and masts draw none. The game's own thunderstorms are unchanged. |
| *Mooring strain* | `mooringStrain` | No [rope parts](Mooring#tying-up), no [anchor](Anchoring) drags, and no vessel is [thrown ashore](Mooring#thrown-ashore) while you are away. A rope and an anchor still hold a vessel at their length. |
| *Currents* | `oceanCurrents` | [Currents](Currents) carry no vessel, steered or not, and no swimmer, and pull on no mooring. The currents still flow, and the water keeps its temperature. |
| *Rower fatigue* | `rowerFatigue` | [Rowing, paddling and poling](Paddles-Poles-and-Oars) cost no satiety. A vessel still goes only as fast as you row it. |
| *Pole's depth* | `poleDepth` | A [pole](Paddles-Poles-and-Oars) pushes in water of any depth, as it does at its full stroke. |
| *Breakers on swimmers* | `swimmerBreakers` | No wave [breaks over a swimmer](Swimming#breaking-crests). You still float on the waves and breathe by them. |

## Wind in the sails

*Wind in the sails* (`sailWind`) is for players who would rather spend less time at sea. Sails feel the wind 1, 2 or 3 times as strong as it blows, and a [sailing vessel](The-Science-of-the-Hull#under-sail) makes about that many times her speed: a little more, as her hull's own windage keeps the real wind, which also pushes her sideways a little less. She sails as before in everything else: how close to the wind she goes, how her sail trims and how she shunts. The wind itself, the waves, a vessel with her sail down and every paddled or rowed vessel stay as they are. 1× is the real wind and the default.

To change a setting in a world already made, open the world's settings from the singleplayer list, or type `/worldconfig CODE false` (or `true`; for the sail's wind a number, such as `/worldconfig sailWind 2`) and reload the world. The setting stays as you left it.

A world made before version 4.1.0 had one setting, *Dangerous oceans*. On its first load it gives every setting above its value, on or off; from then on you change them one by one. So a world that had *Dangerous oceans* off opens with all of them off.

Each setting's description on the world screen, under its name, says what it stops and what stays.

A swimmer [floats](Swimming) and breathes by the waves you see whatever the settings: a crest over your head takes your air, and a trough gives it back. Drowning is the game's own. Cold water and swimming stamina are planned for [The living sea](Roadmap#the-living-sea).
