# The QA session

Read before a QA session, with `bin/qa help`. Before each release a person plays the release build on a real survival world on this machine, and an agent sets up each scene for them from the server console. The player plays; the agent prepares, watches the log and drops markers.

## Running it

- `bin/qa up` starts the server with this clone's AgentBridge and the release zip it ran last: the first start takes the one from the last green CI run on `main`, `bin/qa up latest` takes the newest, `bin/qa up ZIP` any other. A restart never changes the build under the player. The player joins with their own client: Multiplayer, `localhost:42430`, with the same zip from `$QA_DATA/Mods` in that client's own Mods folder.
- The first start makes the world: the Rough Seas playstyle, survival, a fixed seed, the spawn on the shore beside cooper's reeds within sight of the sea. After the player's first join, `bin/qa kit` makes them an admin and gives a flint axe, knife, shovel, hoe and spear. To start over, `bin/qa down` and delete `$QA_DATA`.
- `bin/qa down` saves and stops. A kill loses the last minutes of the world.
- `bin/qa as CMD...` runs each command as the player, at their position, and after a flight waits for them to land: the scenes below are written for it. `bin/qa do CMD...` runs them as the console, for commands that name their target. Both print the reply from the log.
- `bin/qa mark TEXT` writes a timestamped line to `$QA_DATA/markers.log`: one per moment worth a shot in the promo video, as it happens. The timestamps match the game's logs.
- `bin/qa log` prints the mod's decision lines from this run: every storm, wave, strike, capsize and break, with its time. Read it before guessing what happened.
- `/bridge sea`, `shore`, `storm`, `fly` and `find` fly the player in creative. Each scene ends with `/gamemode survival` so it plays as the game does; an idle swimmer in survival sinks, so put the player in a vessel first.
- Coordinates are relative to the map centre, as `/seas` and `/bridge` print them; `/tp 465 111 51` takes the player home.
- `/bridge ride CODE` spawns a vessel under the player, seats them and prints its id. `/bridge dismount` leaves it. `/bridge entity ID` shows its `integrity` and bilge.
- `/giveitem` fills the first free slot, and the player carries ten without a bag: give a scene's items, not every scene's.
- Clean up a scene: `/entity remove e[type=boat-*,range=64]` and the same for `roughseas:*`.
- Storms and currents follow the seed and the hour: `/time add 1 hours` moves both.

## Scenes

Each scene: what the player should see, then its setup.

1. **The new-world screen.** Nothing to set up. On the player's client: Singleplayer, Create new world. Rough Seas is the first playstyle, with Dangerous oceans and their descriptions. Then Settings, Language, Русский, and the same screen again. Cancel without creating.
2. **The guide.** Nothing to set up. The handbook (H), search "Rough Seas": five pages, from Going to sea to Mooring. The vanilla Sailboat and Stone anchor pages carry our text too.
3. **The reed boat from spawn.** `bin/qa as '/tp 465 111 51' '/gamemode survival'`. Harvest cooper's reeds with the knife, craft the reed boat (reed tops around a knife), place it on the water and paddle. `'/giveitem cattailtops 24'` skips the harvest.
4. **A calm sea on a raft.** `bin/qa as '/time set 12:00' '/bridge sea' '/bridge ride boat-raft-oak' '/giveitem oar-crude-oak' '/gamemode survival'`. `/seas here` reads wind, waves, current and water temperature where the player is.
5. **Currents.** `bin/qa as '/seas currents 4000'` names the warmest and coldest current near the player and where. `'/bridge fly X 111 Z' '/bridge ride boat-raft-oak' '/entity spawnat boat-raft-oak 1 ~4 ~ ~ 0' '/gamemode survival'` with that current's X and Z: the empty raft drifts off with it.
6. **The vessels and their niches.** At a calm sea (scene 4's first two commands), one at a time: `'/bridge ride CODE' '/giveitem oar-crude-oak' '/gamemode survival'`, and race it against the raft.
   - `roughseas:reedboat`: quick and cheap, wears out in about three days.
   - `roughseas:dugout-oak`: the toughest hull.
   - `roughseas:coracle`: slow and turns on the spot; shift-right-click picks it up, and it is carried on the back (`'/bridge wear roughseas:coracle'`).
   - `roughseas:barkcanoe`: the fastest paddled, and the only one that runs rapids.
   - `roughseas:currach`: loses condition every day; grease it with `'/giveitem fat-rendered 2'`.
   - `roughseas:outrigger-oak`: sails, the fastest of all.
   - `boat-sailed-oak`: the vanilla sailboat, now with a bilge and a hull that can break.
7. **Damage and repair.** `bin/qa as '/bridge shore' '/bridge ride roughseas:barkcanoe' '/bridge dismount' '/giveitem roughseas:bark-birch 4' '/gamemode survival'`: a canoe on the beach, its id printed. Hit it with the axe and watch the cracks; right-click it holding bark to mend; shift-right-click picks it up, and the item keeps the damage. The reed boat, coracle and currach pick up the same way. Other materials: `'/giveblock log-placed-oak-ud 2'` for the dugout, `'/giveitem plank-oak 4'` the outrigger, `'/giveitem hide-prepared-large'` the currach, `'/giveitem hide-prepared-small'` the coracle. To break one at once: `'/bridge strike ID 1000000'`; it leaves floating wreckage.
8. **Rapids.** `bin/qa as '/bridge sea' '/bridge hold rapidwater-still-7' '/we mark ~-4 ~-1 ~-4 ~4 ~-1 ~4' '/we fill' '/bridge ride boat-raft-oak' '/gamemode survival'`. The raft breaks up within seconds; repeat with `roughseas:barkcanoe`, which runs them unharmed. `/we mark` the same box again and hold `saltwater-still-7` for `/we fill` to undo it.
9. **A storm rising.** `bin/qa as '/seas storms 20000'` lists squalls, gales and hurricanes with their bearing and hours left. `'/time set 12:00' '/bridge storm 60000 1' '/bridge ride boat-sailed-oak' '/gamemode survival'`, then `'/time add 1 hours'` again as it builds. Lightning comes in warm storms only, 12 °C and up.
10. **Surf.** `bin/qa as '/bridge storm 60000 2' '/bridge shore' '/seas here'`: a shore with waves. `'/bridge ride roughseas:dugout-oak' '/gamemode survival'` and paddle in: the waves break in the shallows, near enough certainly, and board the hull. If `/seas here` shows no waves, `'/bridge storm 60000 3'` and try again.
11. **Bailing.** `bin/qa as '/bridge storm 60000 2' '/bridge ride roughseas:dugout-oak' '/giveblock bowl-blue-fired' '/gamemode survival'`. Waves fill the bilge (the hull rides lower); with the empty bowl in hand and no block in sight, right-click to bail. A raft and a reed boat have no bilge to fill.
12. **Capsize and seaworthiness.** `bin/qa as '/bridge storm 60000 3' '/bridge ride roughseas:dugout-oak' '/gamemode survival'`: she ships water wave after wave, and a breaker or a full bilge rolls her over and throws the player out, within a few minutes unless they bail. The outrigger holds to 4 m (`'/bridge storm 60000 4'`); the coracle, the reed boat and the raft give up sooner.
13. **Safe seas.** `bin/qa do '/worldconfig dangerousOceans false'`, then scene 12 again: no water aboard, no capsize, no lightning of ours; currents, rapids and blows still count. `'/worldconfig dangerousOceans true'` brings the danger back.
14. **Anchoring.** `bin/qa as '/bridge sea' '/bridge ride boat-sailed-oak' '/giveitem anchor-stone' '/giveitem roughseas:anchorwarp' '/gamemode survival'`. Ctrl-right-click the prow with the anchor to fit it; right-click it with an empty hand to let go or weigh. It pays out five times the depth, up to the 120 m its recipe carries; the anchor warp in the Lower Mast Storage adds 90 m. Then a storm (`'/bridge storm 60000 2'` takes the player, not the boat: ride a fresh sailboat there) strains the rode.
15. **Mooring to a stake.** `bin/qa as '/bridge shore' '/giveblock woodenfence-oak-ew-free' '/giveitem rope 2' '/bridge ride roughseas:dugout-oak' '/gamemode survival'`. Paddle to the shore, place the post, tie the dugout to it with a rope, go far enough for its chunk to unload and come back: a moored vessel left alone is found pulled up on the shore.
