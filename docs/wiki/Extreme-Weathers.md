[Extreme Weathers](https://mods.vintagestory.at/show/mod/58501) rolls hurricanes, monsoons, blizzards, sandstorms, heat waves and cold snaps of its own, and each one covers the whole world at once. Rough Seas brings hurricanes, downpours and snow too, and ours travel across the map. In one mod set, Extreme Weathers stops rolling the storms that are ours and becomes how they look and what they do. This page is for server owners and modders running both.

## Tested versions

Rough Seas works with Extreme Weathers **1.8.5**. It reaches into Extreme Weathers' own code, so it does so only on a version it was tested against. On any other version it logs a warning and leaves Extreme Weathers alone: both mods then roll their own storms side by side.

## Storms that are ours

Extreme Weathers' own hurricane, monsoon and blizzard rolls are off. Our [storms](Storms) bring the hurricane, the downpour and the snow instead. The change is made in memory when the server starts, so `extremeweather.json` in your `ModConfig` folder stays as you wrote it. `HurricanesEnabled` stays on, since it also switches Extreme Weathers' storm damage.

A server admin can see the settings as they stand with `/roughseas extremeweather`.

## What rides our weather

Most of Extreme Weathers reacts to the wind and rain where a player stands, not to its own events. Under our storms:

- **Damage.** Windows shatter and doors, thatch and leaves tear loose once our wind passes 1.3 (about 33 m/s, a hurricane near its eye wall).
- **Howl and shoving.** The wind howls, items blow away and animals get pushed, at the strengths Extreme Weathers sets.
- **Snow.** A cold storm of ours piles Extreme Weathers' deep snow.
- **Tornadoes.** Inside the core of our hurricane, within about a fifth of its radius of the eye, a tornado can come down. The roll happens every five minutes at Extreme Weathers' own `TornadoChance`. The tornado's world-wide storm sky is off: our hurricane keeps its own clouds.

All of it happens around the players inside the storm. A player far from it sees calm weather.

### The wind cap

Extreme Weathers caps every wind at 1.05, which is below the 1.3 its own hurricane damage needs. Beside Rough Seas, the cap holds only while one of Extreme Weathers' own events runs, so our hurricane reaches that damage.

## What stays Extreme Weathers'

Sandstorms, heat waves and cold snaps still roll as Extreme Weathers decides. They act through levers the game itself has, and Rough Seas answers them as vanilla would:

- A sandstorm dries the world, our storms included: no rain falls under one.
- A heat wave or a cold snap shifts the temperature our storms read. A cold snap can silence a storm's thunder and [lightning](Lightning), since both need warm air.

Extreme Weathers' own weather patterns and events (`supercell`, `torrential`, `zephyr` and the rest) join the game's rotation as usual and play [by their own conditions](For-Modders#weather-events-fire-by-their-own-conditions).

## No whiteout

Extreme Weathers' whiteout needs precipitation above 0.95 at −2 °C or colder. Our coldest storm, the gale, tops out at 0.7, so beside Rough Seas there is no whiteout. That stays as it is until our storms are tuned to real climate data ([#107](https://github.com/hoblin/rough-seas/issues/107)); a whiteout should come from a real blizzard, not from a number raised for one mod.
