# The wiki's pictures

Read before taking or retaking a picture for the wiki, the handbook or the mod page, with `docs/bench.md` and `bin/film help`. Every picture is taken in the bench world by a command, so any agent can take it again the same way. When a pull request changes what a picture shows (a model, a mechanic, a number in its caption), it retakes that picture and fixes its caption in the same pull request.

## The spot and the light

- Every vessel picture is taken at one spot of the bench world: the `beach` zone, a sand spit flush with a calm reed lagoon, and the `lagoon` zone, open water just south of it. Name the zone; coordinates hold only in the bench world.
- The light is calm spring noon, the clock and the weather stopped: `bin/scene` sets it, and a screenplay's `time: "12:00"` and `weather: calm` do. `/time set` only moves the clock forward, so set the hour once.
- Clear the site first: `/bridge weed 8` and `/entity remove` the pigs and deer around it. Weed before laying blocks: a reed bundle counts as a plant.
- Pictures for others never show the bare default skin. `bin/scene dress` puts on the outfit (a hunter's shirt, commoner's trousers, worn boots); `bin/scene views` and the propulsion screenplays dress the player themselves.
- Shoot in spectator, set after the last `/bridge fly`, so no player or shadow is in the frame, unless the player is the subject.
- Start each filmed take on a fresh world: `bin/client stop`, then `bin/client`.

## What takes each picture

| Picture | Command |
| --- | --- |
| A vessel's gallery, `images/VESSEL/{afloat,ashore}-*.webp`, `beside-sailboat`, `dropped`, `wreckage`, `icon.png` | `bin/scene views CODE docs/wiki/images/DIR SCALE`; scales: reed boat 0.8, bark canoe 1, coracle 0.7, currach 1, dugout 1, outrigger 1.3, raft 1, sailboat 1.5 |
| The close shot opening a vessel's page, `ashore-close.webp` | `bin/scene portrait CODE docs/wiki/images/DIR/ashore-close.webp DISTANCE`; distances: raft 4.2, reed boat 2.9 (`reedboat-papyrustops-papyrustops`), dugout 4.4, coracle 2.2, bark canoe 3.7, currach 4.3, outrigger 6.6, sailboat 12.5 |
| A vessel's build stills and animation, `build-*.webp` | `bin/film thoughts/shared/screenplays/2026-11-04/build-VESSEL.yml --webp docs/wiki/images/DIR`. `bin/film` can't play the dugout's `build-dugout.yml` (it holds the axe until a hewing ends): her hewn stills, `build-hewn`, `build-hewn-3`, `build-charred-4` and `build-finished`, come from `2026-11-08/build-dugout-hewn.yml` with `--no-captions`, which sets each stage, and the stills before the first hewing show the bare trunk, unchanged since they were taken; her spoiled blank is `build-dugout-spoiled.yml` |
| The propulsion loops, `images/propulsion/*.webp` | `bin/film thoughts/shared/screenplays/2026-11-05/STROKE.yml --no-captions --webp docs/wiki/images/propulsion --width 640` |
| The recipes, `images/recipes/*.png` | `bin/scene recipes docs/wiki/images/recipes` |
| A handbook page | `bin/game hotkey handbook`, `bin/game link handbook://PAGECODE`, `bin/game shot FILE gui` |
| A face under the waves, `images/sea/{face-steps,face-wall}.webp` | Fresh world, `/weather setprecip 0`. Steps, staged first: `/time set 12:00`, `/bridge fly 376 113 -87`, `/seas stage gale 24 600`, `/time add 15 minutes`, then in the `steep-shore` zone: `/bridge fly 672 115 -68`, `/gamemode spectator`, `bin/game look 3.14 3.38`, `bin/game burst DIR 16 12`, the frame with spray at the steps. Cliff, in the `sea-cliff` zone: `/bridge fly 240 113 -160`, `/seas stage gale 26 600`, `/bridge fly 540 118 -108`, `/gamemode spectator`, `/time set 12:00`, `/time add 15 minutes`, `bin/game look 0 3.36`, `bin/game burst DIR 20 14`, the frame with spray up the face. Each cut to 16:9 and 960×540 |
| The surf and the wet sand, `images/sea/{surf,wet-sand}.webp` | In the `gentle-beach` zone, on a fresh world: `/time set 8:00`, `/weather setprecip 0`, `/weather set clearsky`, `/bridge weed 24` over 164 -58, 164 -40 and 164 -76; `/bridge fly 168 112 237`, `/seas stage gale 24 600`, `/time add 10 minutes`. Surf: `/bridge fly 186 114 -48`, `/gamemode spectator`, `bin/game look 3.95 3.4`, `bin/game burst DIR 16 10`, the frame with the widest sheet. Wet sand: `/bridge fly 180 120 -48`, `/gamemode spectator`, `bin/game look 4.1 3.8`, the burst's frame with the least sheet. Each cut to 16:9 and 960×540 |
| The surf from the shore, `images/sea/surf-from-shore.webp` | In the `gentle-beach` zone, on a fresh world: `/time set 12:00`, `/weather setprecip 0`, `/weather set clearsky`, `/bridge weed 24` over 164 -58, 164 -40 and 164 -76; `/bridge fly 168 112 237`, `/seas stage gale 24 600`, `/time add 10 minutes`, `/bridge fly 156 113.4 -48`, `/gamemode spectator`, `bin/game look 1.75 3.42`, wait 45 s; `bin/film thoughts/shared/screenplays/2026-11-10/surf-from-shore.yml --no-captions --webp DIR --width 640` |
| The mesh in height colours, `images/sea/{mesh-block,mesh-divided,mesh-far}.webp` | In the `open-sea` zone, on a fresh world: `/gamemode spectator`, `/time set 12:00`, `/weather setprecip 0`, `/bridge fly 152 116 160`, `/seas stage hurricane 55 800`, `/bridge fly 302 118 160`, wait 45 s, `.surface colours true`, `bin/game look 1.5 3.5`, `/time stop`. `mesh-block` with `.surface tessellation false`, `mesh-divided` with it true on the same frozen frame, each the bottom-centre 1280×720 cut to 960×540; `mesh-far` a whole frame cut to 16:9 and 960×540 |
| A bar in height colours, `images/sea/bar-colours.webp` | In the `open-sea` zone, on a fresh world: `/gamemode creative`, `/giveblock rock-granite 64`, its slot in hand, `/bridge fly 352 118 160`, `/we mark 300 40 130 404 108 142`, `/we fill`, `/we clear`, then `/bridge fly 152 116 160`, `/seas stage gale 34 800`, `/gamemode spectator`, `/bridge fly 352 124 165`, `bin/game look 3.14 3.6`, wait 50 s, `.surface colours true`, `bin/game burst DIR 6 18`, the frame with the granite bared, cut to 16:9 and 960×540 |

`bin/scene views` shoots every form a vessel takes, the held one included. Publish only the forms a player meets: a vessel too heavy to pick up (the dugout, the outrigger) has no held picture on its page, and its `held.webp` is not kept.

## The mechanics stills

Each is the hull as the game damages it, struck for real, never a crack written into its tree (a written crack shows in the tree but not on the hull's panel). Each is taken in the `lagoon` zone at calm noon (`/time set 12:00`, `/weather setprecip -1`, `/weather set clearsky`, `/weather setw still`, `/time stop`), its vessel spawned under the player with `/bridge spawn CODE ~ ~-1 ~ 90`, and shot with `bin/scene photo ID FILE DX DY DZ`, adding `hover` when the caption quotes the panel.

| Picture | How |
| --- | --- |
| `mechanics/damaged-currach.webp` | `/bridge strike ID 400 portside` on a currach |
| `mechanics/wounds-from-the-seat.webp` | a dugout struck with 6000 J twice on the `bottom`, three times `portside`, four times `starboardside`; then `/bridge mount ID`, `bin/game camera first`, `bin/game look 4.712 4.0` (aft from the seat) and `bin/game shot`, cut to 960×540 |
| `mechanics/float-coming-away.webp` | an outrigger struck with 400 J four times `starboardside`, its lashings at 0.23; `photo` from the float's side, `1.5 3.5 -3.5` |
| `mechanics/dugout-bilge.webp` | a dugout with `/bridge set ID roughseas:bilge/litres 650`; `photo` from `2.5 3.5 2` with `hover`, and the caption quotes its bilge and side from the panel |

## Rakes

- The lagoon is shallow and clear: under a ridden hull, which sits deeper and pitches, its underwater half shows refracted beside it as a pale ghost hull. Film a ridden vessel at `open-sea`, or at `shallows` for poling.
- A vessel spawned at a half block sits on the line between two blocks and slides into the water: place at whole numbers. A screenplay's player hovers at y110.3 and spawns its cast at `at: [0, -0.3, 0]`.
- A build stage is set before the vessel enters the world (a screenplay's `build:`, `/bridge spawn CODE X Y Z YAW KEY VALUE`), never after: a vessel spawned finished has run a tick as a finished hull and slides.
- Captions only time a scene: stills and loops are filmed `--no-captions`.
- An animated WebP is made by `bin/film --webp` at 12 fps (ffmpeg's `libwebp_anim`): there is no cwebp on the machine.
