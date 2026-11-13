A vessel becomes one of ours by data, not code: a few behaviours on its entity, configured in JSON. They float it by its mass, take the sea for it (waves over the side, breakers on the hull, a ride on the drawn swell), hold its bilge, crack, flood and sink its hull, moor it and drift it with the current. Vanilla's raft and sailboat get theirs by patch. This page is for a mod that adds a vessel, or makes its own vessels ride our sea.

## A vessel in our sea

A vessel meets [both of our seas](Reading-the-Sea#two-seas). On the server the waves at its place decide what happens to it: the sea state's waves as the bed under it grows, turns and breaks them, read from the drawn sea the server builds over its region. On a client the drawn sea shows the vessel riding it.

On the server, once a wave period, every vessel in a sea of 0.3 m or more meets a wave. The wave is drawn from the waves at its place, most lower than their significant height and a few much higher ([Every wave is different](Wind-and-Waves#every-wave-is-different)). A hull rises with part of the crest. What is left above its side comes aboard, into its bilge, or washes through a hull that has none. A wave that breaks slams the hull too, on the side the waves come from, with a pressure that grows with its speed and is far greater if it plunges. The player's view of all of it is [Surf and swamping](Surf-and-Swamping).

On both sides, a vessel with a hull of ours floats by its full mass: hull, riders, what they carry, containers and the water aboard settle it lower, and it floods when its side goes under. On a client, it rides the drawn sea: lifted with the surface under it, rolled and pitched with its slope, on periods of its own mass. Water aboard rolls it harder and slows it, and a full bilge swamps it.

Each way the sea harms a vessel has its own [world setting](World-Settings#the-mechanics), all on by default. Turn one off and that harm stops, while the sea, the swell and the storms still run: with *Hull damage* off nothing cracks a hull, and with *Water in the bilge* off no wave or rain comes aboard.

## The behaviours

| Code | Sides | What it does |
| --- | --- | --- |
| `roughseas:seaforces` | client and server | the vessel's contact with the sea: waves aboard, breaker strikes, the ride on the drawn swell and the waves' water under it, its mass slowing it, the settling it draws. Mooring needs it |
| `roughseas:bilge` | client and server | the water aboard: from waves, leaks and rain, bailed out, swamping the hull when full |
| `roughseas:vesselintegrity` | client and server, first in both lists | the hull: how it floats and sinks, what cracks or breaks it, its leaks, mending, wreckage |
| `roughseas:mooring` | client and server | a stake rope and an anchor that hold in a storm, or don't |
| `roughseas:currentdrift` | client and server | the hull moved by the water: the waves' water, the current's drag, the waves' push and its lines' tension, and the swing about a held bow |
| `roughseas:burnout` | client and server | a hull burnt and hewn out of a row of logs, in the world, before it floats finished ([Built in place](#built-in-place)) |
| `roughseas:framing` | client and server | a vessel laid piece by piece where it lies: a bark hull stripped off a standing tree ([Framed in place](#framed-in-place)), or bundles bound together ([Bound in place](#bound-in-place)) |
| `roughseas:riggable` | client and server | a finished hull that a boom laid across it joins to a float, making it the vessel it is rigged into ([Rigged in place](#rigged-in-place)) |
| `roughseas:rigging` | client and server | that vessel, rigged piece by piece where it lies ([Rigged in place](#rigged-in-place)) |
| `roughseas:covering` | client and server | a hull bound from laths laid out on the ground, framed and covered with hides piece by piece where it lies ([Laid out on the ground](#laid-out-on-the-ground)) |
| `roughseas:droop` | client | ends that sag as a reed hull soaks ([Bound in place](#bound-in-place)) |
| `roughseas:propulsion` | client and server | what drives it when it is paddled, poled or rowed: the tools it takes, its hull's form for the water's drag on it, and the rower's strokes ([Driving it](#driving-it)) |
| `roughseas:cargo` | client and server, before `rideableaccessories` | holds: inventories opened by a click on their own boxes, their largest stacks drawn on the floor as small ground piles ([Holds in place of containers](For-Modders#holds-in-place-of-containers)) |
| `roughseas:windage` | client and server | the wind on it: drag on what stands above the water, and a set sail that drives it by the wind, held by its rig and drawn filling in its own frame ([Sailing by the wind](#sailing-by-the-wind)) |

Configure each one in your entity's `behaviorConfigs` under its code, as vanilla's behaviours are, and list it in `client.behaviors` and `server.behaviors` as the table says.

They build on each other, so add them in this order. `seaforces` is the vessel's contact with the sea, and the others act through it: the bilge fills from the waves it takes and swamps the hull only through it, and `mooring` holds against the water's pull `currentdrift` works out. A vessel with `seaforces` alone already rides the swell and takes the sea; `currentdrift` moves it by the water. Add `bilge` for a hull that holds water, `vesselintegrity` for one that floats by its mass and can crack and sink, `mooring` and `currentdrift` on both sides. `vesselintegrity` and `currentdrift` also work alone.

`vesselintegrity` goes first in both lists, before `creaturecarrier` and `rideableaccessories`.

Every key may carry a `sources` entry beside it, text about where each number came from. Our code doesn't read it; we keep a real-world source for every number there, and so can you.

Our [coracle](Coracle), its sources, its build, its frame and the voxel twins of its measured values left out, carries all five:

```json
"behaviorConfigs": {
    "roughseas:vesselintegrity": {
        "impactCapacity": 400,
        "parts": [{ "name": "hide", "material": "hide", "share": 9.4 }, { "name": "frame", "material": "wood-willow", "share": 7.7 }],
        "shell": {
            "kind": "skin", "material": "hide", "metres": 0.005,
            "backing": { "material": "wood-willow", "metres": 0.01 },
            "backedShare": 0.4, "bottomBackedShare": 0.49, "spanMetres": 0.3728
        },
        "depthMetres": 0.35,
        "waterplaneSquareMetres": 1.0625,
        "hullBoxMetres": { "from": [-0.742, 0, -0.53], "to": [0.742, 0.353, 0.53] },
        "seep": { "litresPerHourASeam": 13, "greaseDays": 2.5 },
        "cover": { "hides": { "prepared": 2 }, "seams": 0 },
        "repairs": [
            { "material": { "type": "item", "code": "game:hide-prepared-small", "stacksize": 1 }, "restores": 0.67, "saturation": 40, "seconds": 20 },
            { "material": { "type": "item", "code": "game:fat-rendered", "stacksize": 1 }, "restores": 0.5, "saturation": 20, "greases": true, "seconds": 10 }
        ],
        "wreckage": [{ "type": "item", "code": "roughseas:lath-oak-2-dry", "quantity": { "avg": 3, "var": 2 } }],
        "damage": { "material": "hide" }
    },
    "roughseas:seaforces": {
        "mastMetres": 0, "windageSquareMetres": 0.3516,
        "heave": 0.8, "capsizeBreakerMetres": 0.6,
        "rollResponse": 1.6, "pitchResponse": 1.6
    },
    "roughseas:bilge": {
        "capacityLitres": 272.2, "deckSquareMetres": 1.0666, "drainLitresPerHour": 0,
        "levelHeightsMetres": [0.081, 0.111, 0.141, 0.172, 0.202, 0.233, 0.263, 0.294]
    }
},
"client": {
    "behaviors": [
        { "code": "roughseas:vesselintegrity" }, { "code": "repulseagents" }, { "code": "passivephysicsmultibox" },
        { "code": "interpolateposition" }, { "code": "hidewatersurface", "hideWaterElement": "hideWater/*" },
        { "code": "selectionboxes" }, { "code": "creaturecarrier" }, { "code": "ropetieable" }, { "code": "roughseas:currentdrift" },
        { "code": "roughseas:seaforces" }, { "code": "roughseas:bilge" }, { "code": "roughseas:mooring" }
    ]
},
"server": {
    "behaviors": [
        { "code": "roughseas:vesselintegrity" }, { "code": "repulseagents" }, { "code": "passivephysicsmultibox" },
        { "code": "selectionboxes" }, { "code": "creaturecarrier" }, { "code": "roughseas:currentdrift" },
        { "code": "roughseas:seaforces" }, { "code": "roughseas:bilge" }, { "code": "roughseas:mooring" },
        { "code": "ropetieable" }
    ]
}
```

Every vessel of ours is a whole example, each number with its source: `assets/roughseas/entities/vessel/` in the mod. A vessel that must also work without Rough Seas keeps its entity file free of our codes and gets the behaviours by patch, with `"dependsOn": [{ "modid": "roughseas" }]` on each operation, so the game skips the patch when we are absent. Our `patches/storms-vessels.json` and `patches/vessels-vanilla-boats.json` give them to vanilla's raft and sailboat the same way; being ours, they need no `dependsOn`.

## Measured from the model

Our vessels carry no size copied by hand. `bin/model build` measures each vessel off its model and writes what it measures into the vessel's entity file in `entities/vessel/`; for vanilla's raft and sailboat it writes into our patches `vessels-hull-boxes.json`, `vessels-vanilla-boats.json`, `storms-vessels.json` and `propulsion-vessels.json`, the raft measured from vanilla's logs and the sailboat from vanilla's planking. Each object it writes into opens with a comment line, ``// Generated by `bin/model build` from the model; a build rewrites these values:``, and the keys it owns there. The next build overwrites a hand edit to one of them, so change the model or its builder in `bin/lib/hulls` instead; the other keys in the object stay as you write them.

What it writes, each object's comment line giving the full list:

- **The hull**: `roughseas:vesselintegrity`'s `hullBoxMetres`, and from it `hitboxSize` (the hull's width and height) and `passivephysicsmultibox`'s `collisionBoxes` (the hull box from its origin up, in squares as wide as the hull is, and an outrigger's float in as many squares as are left of vanilla's ten boxes); `weight`, each part's `share`, `swimmingOffsetY`, `depthMetres` and `waterplaneSquareMetres`.
- **The shell**: `backedShare` and `bottomBackedShare`, sampled over its skin against the members found behind it, `spanMetres`, a `frame`'s `spacingMetres` and `spanMetres`, and a carved or bundled shell's `metres`.
- **Strength and seams**: a reed hull's `impactCapacity` and an outrigger float lashing's, from the rope the model counts; a hide cover's `seep.litresPerHourASeam`, from the length of seam the model cuts; the float's `firewoodLitres`.
- **The bilge**: `capacityLitres`, `deckSquareMetres` and `levelHeightsMetres`.
- **The sea**: `windageSquareMetres`, its side above the water with one aboard; `mastMetres`, its masthead's height; the outrigger's `beamMetres`, across hull and float.
- **The wind**, for a vessel with `roughseas:windage`: its side and front above the water, their centre and the centre of its side below it, its sail's area set full and half, and its rig's points and spars, the outrigger's from `bin/lib/hulls/rig.py`, the sailboat's from vanilla's own yard, cloth, sheets and stays.
- **Driving it**: `sweepMetres`, `paddleAbeamMetres` (from its middle), `finSquareMetres` (a float's side below the water), `oars`' `spanMetres` and `bladeSquareMetres`, and the `pole`'s `handsAboveWaterMetres`, `plantAheadMetres`, `plantAsternMetres`, `plantAcrossMetres`, `drawnPoleMetres`, `drawnBedMetres` and `stance.riderOffset`.
- **The build**: `roughseas:burnout`'s measures, `roughseas:framing`'s `trunkBlocks`, `roughseas:rigging`'s `floatLogs`, `floatLengthMetres`, `floatOffMetres`, `floorMetres` and each stage's `raisesMetres` and `raisesLitres`, and `roughseas:covering`'s `start` and `overturnedPivotVoxels`.

**Units.** A length, an area or a volume is written twice: in metres, square metres or litres, and beside it in the shape's voxels, the key ending `Voxels`, `SquareVoxels` or `CubicVoxels` in place of `Metres`, `SquareMetres` or `Litres`: `depthMetres` and `depthVoxels`, `capacityLitres` and `capacityCubicVoxels`. The mod reads the metres; the voxels let you check a number against the model. Only `overturnedPivotVoxels`, a point on the shape, is read in voxels. A metre is 16 voxels, set once in `RoughSeas/Units/voxel.json`; `RoughSeas.Units.Voxels` converts.

**A vessel of yours** sets these keys by hand, in metres, and may leave the voxel twins out. Without `hullBoxMetres`, its hull is measured at runtime on the shape element its `HullAP` attachment point hangs from, as vanilla's raft and sailboat are on the `Hull` element our patch adds to their shapes.

## Taking the sea

`roughseas:seaforces`. Leave a key out and it takes the default, which for most of them is zero: a hull with no freeboard and no heave ships every crest. Set them all.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `beamMetres` | m | its `Hull` box's | the hull's width: how much of a crest it takes aboard, how hard the sea pulls on its mooring, how it rolls. Leave it out to take the `Hull` box's width; give it where the sea meets more than the hull, as the outrigger's float |
| `windageSquareMetres` | m² | 0 | what the wind catches, hull and rig; nothing reads it here yet. A vessel with `roughseas:windage` gives it there, where the wind pushes it ([Sailing by the wind](#sailing-by-the-wind)) |
| `mastMetres` | m | 0 | a mast's height above the hull; [lightning](Lightning) seeks it |
| `freeboardMetres` | m | 0 | the side above the water, empty, for a vessel that doesn't [float by its mass](#floating); ours leave it out |
| `heave` | 0–1 | 0 | the share of a crest's height the hull rises with; what is left climbs its side |
| `rollResponse` | × | 1 | how many times the water's slope across it it rolls on the drawn swell |
| `pitchResponse` | × | 1 | the same along it |
| `capsizeBreakerMetres` | m | none | the breaker that overturns it, empty |
| `capsizeBreakerBeams` | × | none | the same as a share of `beamMetres`, where the source gives it so: the raft's, the dugout's, the bark canoe's and the outrigger's is 1, a breaker as high as the hull is wide. A `capsizeBreakerMetres` given wins |
| `capsizeBreakerLengths` | × | none | the same as a share of its hull box's length: the sailboat's is 0.55, the share of a keelboat's length the Wolfson Unit's trials after the 1979 Fastnet found rolls it. `capsizeBreakerMetres` and `capsizeBreakerBeams` win |

A crest stands half a wave's height above still water, seven tenths when it breaks; the hull rises with `heave` of that. Water aboard adds to the roll, twice as much when full. A vessel that floats by its mass ships water over the freeboard it floats at; one with a fixed `freeboardMetres` loses it in proportion to its bilge, all of it when full.

**Capsizing is switched off** ([Capsizing](Surf-and-Swamping#capsizing)), so `capsizeBreakerMetres` and its shares do nothing today. It waits for heel in [Seamanship](Roadmap#seamanship): a hull will heel by its stability under any push from the side, the wind in a sail or a breaker on the beam, and overturn past the angle it can't come back from. What a vessel gives for that is not settled yet.

**Riding the drawn swell.** On a client, the vessel is posed on the surface under it, sampled at the ends and sides of its hull: its hull box's length and its `beamMetres`. A vessel without `roughseas:vesselintegrity` takes its collision box's longer side for its length. A vessel that floats by its mass follows that pose on its own heave and roll periods, from its draft, depth and beam ([How a hull answers](Reading-the-Sea#how-a-hull-answers)); one that doesn't takes the water's pose as it lies. This rides on vanilla's boat: the entity must be an `EntityBoat` or derive from it, and a class of yours that overrides `OnRenderFrame` calls the base, or it isn't posed. A vessel that can't, poses itself on the same surface through [the drawn sea](Reading-the-Sea#the-drawn-sea). Where no swell is drawn it rolls and pitches as vanilla rocks it, times `rollResponse` and `pitchResponse`.

**Its mass slows it.** For a vessel without `roughseas:propulsion`, and not under a sail of `roughseas:windage`, an `EntityBoat` makes its entity attribute `speedMultiplier` at its reference mass: its own `weight` and one 75 kg rider. Heavier, it is slower, and lighter a little faster ([why](The-Science-of-the-Hull#floating)): the hull in its own material, riders, gear, armour, containers and the water aboard all count. A vessel with `roughseas:propulsion`, or under such a sail, is slowed by its mass through its drag instead ([Driving it](#driving-it), [Sailing by the wind](#sailing-by-the-wind)). A swamped one makes no speed, and its riders are moved into the water beside it, holding on.

## Driving it

`roughseas:propulsion`. With it, a vessel goes as fast as its rower drives it against the water's drag on its hull ([how](The-Science-of-a-Body-in-the-Water#driving-it)), in place of vanilla's `speedMultiplier` and our old `turnSpeed`, which ours no longer carry. It needs `roughseas:vesselintegrity` for the hull's size, draft and mass, and `roughseas:seaforces` for the water's depth under it, which a pole needs. Under sail its rower's tool rests: a sail of `roughseas:windage` drives it by the wind ([Sailing by the wind](#sailing-by-the-wind)), and one without goes as vanilla sails it.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `bowDrag` | coefficient | 0 | a blunt bow's own drag, on its beam by its draft; a fine one adds none beyond the form factor |
| `sideDrag` | coefficient | 0.75 | the water's drag across it as it turns, on its length by its draft |
| `finSquareMetres` | m² | 0 | a keel's or a float's side, which the water meets across it too |
| `paddle` | stroke | none | how a paddle drives it: `paddling` along its side or `sculling` over its bow; none, and a paddle doesn't serve it |
| `oars` | | none | its own oars on thole pins: `stroke` (`rowing`), `bladeSquareMetres` of each, and `spanMetres` from blade to blade across it |
| `sweepMetres` | m | 0 | how far from its middle a held tool sweeps it round |
| `paddleAbeamMetres` | m | 0 | how far out from its middle a paddle's blade drives, the lever by which it turns the vessel away from its side |
| `pole` | | none | how a pole drives it: `stroke` (`pole` standing, `seatedpole` from the seat), `handsAboveWaterMetres`, how high the poler's hands are over the water at the end of the shove, so that a pole reaches that much less deep; `plantAheadMetres` and `plantAsternMetres`, where along the vessel the poler's upper hand plants the pole going ahead and astern, and `plantAcrossMetres`, where across it the pole's foot goes in, all from the point the vessel stands at, so the bed is sounded where the foot lands; `drawnPoleMetres` and `drawnBedMetres`, the pole and the bed the poler's strokes are drawn to, from which the drawn pole slides through the poler's hands to the bed; and `stance`, where the poler stands to pole: the `animation` they stand in, their `riderOffset` from their seat's attachment point in blocks and their `eyeHeight`. With no `stance`, the poler poles from the seat |
| `strokes` | | none | the rower's `ready`, `forwards` and `backwards` animations for each tool it takes: `pole`, `paddle`, and `tholeoar` for its own oars. A paddle worked on either side takes `paddle-starboard` and `paddle-port` instead, each with a `change` that carries it over to that side. A pole stroke starts and ends halfway through its recovery, and the pole drives it only in its shove, the stroke's `driveShare` that begins `(1 − driveShare) / 2` into it |

Our vessels' builders decide the stance from the hull: `stands` in `bin/lib/hulls/physics.py` stands the poler where the hull, with the poler standing, is at least as stiff as the narrowest canoe polers stand in ([why](The-Science-of-a-Body-in-the-Water#driving-it)), and the builder writes the stance's offset, the poler's hands' height and where the pole is planted into the vessel's JSON.

A vessel's length and beam are its hull box's: `hullBoxMetres`, or the `Hull` element. The rower's power and each stroke's rate, drive and efficiency are in `config/propulsion.json`, the drag law's constants in `config/vessels/drag.json`, each number with its source.

A tool is an item whose attributes carry `roughseas:propulsion`: `tool` is `pole` (its `metres`, or its `length` variant) or `paddle` (its `bladeSquareMetres`). Oars on thole pins are the vessel's own, given by its `oars`, not a tool in hand. A vessel that takes a held tool sets vanilla's `requiresPaddlingTool`, and the tool `paddlingTool`; under sail, a vessel of our class `roughseas:EntityVessel` is steered whatever its rider holds. Our poles and paddles carry `attachableToEntity` with the category `oar`, so a slot of that category, as vanilla's raft has, stows them.

## Sailing by the wind

`roughseas:windage`, on both sides, with `roughseas:currentdrift` and `roughseas:vesselintegrity`. It pushes the vessel by the wind where its physics runs, as the water's drag does ([Currents](#currents)): on the server while nobody steers it, on its rider's client while someone does. Our [outrigger](Outrigger-Canoe) carries it in its entity, vanilla's [sailboat](Sailboat) gets it by `patches/wind-vessels.json`.

- **The wind on it.** Each surface meets the wind at its own height, less the vessel's own velocity: the apparent wind. With its sail furled, what stands above the water takes the wind as drag, across its side and along its front, at their centre. Afloat only: a hull ashore takes no wind.
- **A set sail** drives it ahead and presses it to leeward by its `polar`, at the angle the apparent wind comes from; it takes the bare windage's place. Too close to the wind it lifts nothing, and the bare windage pushes again. A sail the wind meets on its wrong face is taken aback and only pressed. The vessel's own cross-flow drag gives its leeway, and the force's moment about `lateralCentreMetres` turns it ([how](The-Science-of-the-Hull#under-sail)).
- **The rig.** The sail's force reaches the hull through its fittings, each line's pull and each joint's push at the point it really acts. The sail trims itself: it settles where its weight, the wind and its sheet balance, within the turns its spars and lines leave free, never through a stay or the mast.
- **The sail's position** is vanilla's watched `sailPosition`, 0 furled, 1 half and 2 full, which a right-click on the `LowerMastAP` box steps through on an `EntityBoat` with `unfurlSails`. While it is set, our postfix on `EntityBoat.SeatsToMotion` takes vanilla's own drive out, so only its rider's turn stays vanilla's.
- **Drawn.** On a client the sail, its spars and its lines are a mesh built each frame from the rig's points, in the vessel's frame, so the cloth heaves with the hull and fills with the wind. The groups `drawn` names leave the shape once the vessel is built. Vanilla's sailboat's own cloth and yard (`SailUnfurled`, `SailHalf`, `SparORIGIN`) leave it by the same patch.
- **Shunting.** A crab claw with an end at each end of the hull changes tack by shunting: a right-click on the box of the other end's `steps` carries its tack, its mast's rake and its stays there, and a client draws the change over a second. Which end it is at is the watched int `roughseas:tacked`.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `windageSquareMetres` | m² | 0 | its side above the water with one aboard and its sail furled |
| `frontSquareMetres` | m² | 0 | the same seen from ahead |
| `windageCentreMetres` | m | 0, 0 | `[along, height]` of that side's centre, from the point the entity stands at |
| `lateralCentreMetres` | m | 0 | how far along it its side under the water is centred, the point the wind turns it about |
| `sail` | | none | `polar`, one of `config/wind.json`'s `sails`; `sailSquareMetres` set full and `halfSquareMetres` set half; `halfReach` and `halfSpan`, the share of the cloth's reach from its luff and of its span from its head that a half-set sail shows (default 1) |
| `rig` | | none | how the sail is held: `kind` `square`, or a crab claw when left out, with the points below in metres from the point the entity stands at, `[along, height, across]`, and the thickness each part is drawn at: `sparMetres`, `poleMetres`, `shoreMetres`, `lineMetres`, `rollMetres` (the furled or reefed cloth) |
| `drawn` | shape groups | none | the groups the renderer draws in place of once the vessel is built: our outrigger's `Mast`, `SailFurled` and `Stays` |
| `steps` | selection boxes | none | a crab claw's sail steps, one for each of its `ends` in order |
| `textures` | texture codes | none | the shape's textures the renderer draws with: `cloth`, `wood` and `cord` |

A crab claw's `rig` gives `ends`, one for each end its tack can stand at, the first at the bow; each has a `hoisted` and a `lowered` pose, the spars set and let down, of the points `tack`, `masthead`, `heel` (the mast's foot), `halyardBend` (where the halyard holds the yard), `peak` (the yard's head), `clew` (the boom's end), `bridle` (where the sheet holds the boom), `lead` (where the sheet comes to the hull), `sternPin` (the far end, where the back stay is made fast), `shroudFoot`, `shoreTop` and `shoreFoot`, each with `Metres` after its name; and `yardKilograms` and `boomKilograms`, what its spars weigh. A square `rig` gives `mastHeel`, `masthead`, `yardPort` and `yardStarboard`, the cloth's corners `headPort`, `headStarboard`, `clewPort` and `clewStarboard`, the sheets' `leadPort` and `leadStarboard`, and the stays' `forestayHead`, `forestayFoot`, `backstayHead` and `backstayFoot`, each with `Metres`: its yard turns about its mast as far as its stays let it.

`config/wind.json` holds the air's density, the wind's growth with height, a bluff body's drag, a sail's camber, its cloth's weight and the sails' polars: `headings`, `[degrees off the apparent wind, drive along the hull, side force across it]` over the dynamic pressure on the sail, or `liftAndDrag`, `[lift, drag]` trims the sail picks its best drive from. Every number carries its source there, and a patch can change it or add a sail. Every `logSeconds` it logs `[wind]` lines where the vessel's physics runs: the wind, the sail's force and moment, how its rig stands and the tension of its sheet, halyard and stays; and the server logs each shunt.

## The bilge

`roughseas:bilge`. A vessel without it holds no water: the sea washes through, as through our raft and reed boat.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `capacityLitres` | L | 0 | what it holds before it swamps |
| `deckSquareMetres` | m² | 0 | the open area rain falls into |
| `drainLitresPerHour` | L per game hour | 0 | what drains out by itself |
| `levelHeightsMetres` | m | none | the height above its origin of each level of water the shape draws, from the bottom up |

Whether a swamped vessel lies awash or goes to the bottom comes from its [buoyancy](#floating), not from the bilge. A vessel without `roughseas:vesselintegrity` never sinks.

[Bailing](Bailing) needs nothing from you: a bowl, a jug or a bucket bails any vessel with a bilge.

To show the water, give the shape a group `Bilge` with one child per level from the bottom up (`Bilge1`, `Bilge2`…), a texture `bilge`, list `Bilge` in the shape's `ignoreElements`, and give each level's height in `levelHeightsMetres`, one per child. Our dugout is the example; our builders write the heights from the shape. Without them the bilge works and the water is unseen.

## Built in place

`roughseas:burnout` builds a vessel the way a dugout was really made: from five logs of one wood lying end to end beside the water, burnt and hewn out a fire at a time. An axe Shift + right-clicked on the middle log of such a row takes the logs away and leaves the vessel there as a blank in its bark, drawn as the logs were: the entity of your vessel's code and the logs' wood. The rule for the row needs nothing from you: any vessel with this behaviour whose code ends in a wood (`dugout-{wood}`) is built from that wood's lying logs, and lying log sections, in every mod's woods.

The axe held on the blank strips its bark first, a piece a stroke, `barkPieces` of them; nothing else goes on a blank in its bark. Each hollowing then takes what vanilla's campfire takes: clay along the rim, dry grass, four of anything vanilla builds a firepit with (`firepitConstructable`), and a torch or firestarter held on it as on vanilla's firepit, a firestarter with its chance to fail. Once the fire burns out, an axe held on the blank hews out the char a stroke at a time; the last hewing finishes the vessel.

- **Clay.** A ball covers 25 voxels, as in vanilla's clay forming, and the rim takes as many voxels as the shape's `Clay{level}` group covers seen from above, one voxel thick: each fire's rim, as the hull narrows. Nothing to set: the count follows your shape. The clay stays on the rim through the fire and goes with the hewing that takes the rim down, so each fire needs its own. The first ball's kind (`blue`, `red`, `fire`) draws the clay with the entity's `clay-{kind}` texture, where it has one.
- **The fire** catches in its first hour, the grass and firewood still drawn; then its three thirds draw it burning, burnt thin and down to embers, each smouldering with vanilla's fire particles at its own rate, and lighting the ground round it as a pit kiln does. Whoever stands in the hollow while it burns is burnt as in vanilla's firepit.
- **The outside.** The blank is the whole trunk the logs make: each hewing also takes a sixth of `unhewnLitres` of sound wood off the blank's outside, with the strokes that hew out the char, and its depth and waterplane go from `blankDepthMetres` and `blankWaterplaneSquareMetres` to the finished hull's a sixth at a time. That wood weighs and floats until it is hewn away.
- **A fire without clay** is never refused. While the rim lacks any of its clay, the fire chars the rim at the hollow's rate, and once that char reaches `wallMetres` the blank is spoiled: it is drawn with every face of its wood texture charred, takes no more work, and breaks up as any vessel does.
- **Strokes.** A stroke breaks away a strip of the fire's char as wide as `axeEdgeMetres` across `hollowBeamMetres` of hollow, so a hewing takes the hollow's share of the hold over one such strip. The holder strikes every `secondsPerStroke`, and `hewSaturation` is spread over a hewing's strokes. Every axe in the game, a modded one too, hews.
- **Sections.** A hewing works the hull in the sections `hewnSectionsMetres` gives, spans along it from its bow, each end going with the section it closes. Its strokes fall in twice as many equal stages: the inside of one section a stage, from the bow, its char gone and its floor down to the next hollowing's, then the outside of one a stage, from the stern, down to the next hollowing's hull. The hull's hold and floor follow the inside's stages, its outside wood, depth, waterplane and rim the outside's, so nothing steps at a hewing's end. The holder swings vanilla's felling chop, retimed to the player animations `roughseas-hew` and `roughseas-hew-fp` of one blow every two seconds, which play the chop at the blow's frame (give your vessel a `secondsPerStroke` of 2, so that each stroke is one blow), and every client throws chips at that frame from the section being worked: charcoal up from the hollow, the vessel's planks off the side the holder stands at.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `hollowings` | count | 1 | the fires and hewings that hollow it out |
| `hollowDepthMetres` | m | 0 | from the rim to the floor of the finished hollow |
| `rimMetres` | m | 0 | the finished rim's height above the vessel's origin |
| `blankDepthMetres` | m | 0 | the trunk's height as the logs lay, where the first fire burns |
| `blankWaterplaneSquareMetres` | m² | 0 | the trunk's waterplane, floating |
| `unhewnLitres` | l | 0 | the trunk's wood outside the finished hull |
| `charMillimetresPerHour` | mm per game hour | 1 | how fast the fire chars its way down: one fire burns for the hollow's depth over `hollowings` at this rate |
| `clay` | item code | none | what guards the rim, wildcards allowed (ours `game:clay-*`) |
| `wallMetres` | m | 1 | the char on an unguarded rim that spoils the blank: its walls' thickness |
| `axeEdgeMetres` | m | 1 | the width of char a stroke breaks away |
| `hollowBeamMetres` | m | 1 | the width of the hollow a stroke sweeps across |
| `secondsPerStroke` | s | 1 | how often a held axe strikes |
| `hewSaturation` | saturation | 0 | what a whole hewing costs the one who hews |
| `barkPieces` | count | 0 | the strokes that strip the blank's bark, one piece of it each |
| `hollowLengthMetres` | m | 0 | how long the fire burns along it, where its smoke and flames rise |
| `hewnSectionsMetres` | m | none | the sections a hewing works one at a time: spans along it from its bow, as its hull box measures it, the first and last taking its ends |
| `hullBoxesMetres` | m | none | its hull box at each hollowing, `from` and `to` as `hullBoxMetres` gives them, round the trunk as it stands then: what the axe and every click aim at while it is built |

The vessel needs a `roughseas:bilge` too: its `capacityLitres` is the hollow that the build cuts out. At every stage it is a vessel: the wood not yet cut out weighs and floats as its hull does, backs its sides below the cut and its bottom against a blow, and its `roughseas:bilge` holds only what is cut, so a blank floats solid and a half-hollowed hull ships half a bilge. It cracks and breaks as a finished one, rain puts its fire out, and water in the hollow keeps the fire from being lit. It gets its seats only when finished. A vessel already in a world from before, with no build of its own, is finished.

Give the shape its stages as groups. `Bark` holds the bark's pieces in the order the axe strips them, `Bark1` to `Bark{barkPieces}`, over `Blank0`, the bare trunk. `Blank` (the wood still uncut), `Char` (its charred top), `Grass` (the tinder), `Wood` (the fire laid on it, `Wood{n}x1` to `x4`, a piece a firewood), `Fire` (the fire once it has caught, `Fire{n}At1` to `At3`, its thirds) and `Clay` (the rim's clay, its pieces in the order the balls cover them) each have one child per hollowing: `Blank0`, `Blank1`…, `Blank{n}` the whole trunk as it stands at that level, worked from the logs a sixth of the way to the hull at each. Each `Blank{n}` holds a group per section, `Blank{n}x1` to `x{sections}`, from the bow, and the hull's two ends, `Blank{n}Stern` at the first section's end and `Blank{n}Bow` at the last's; inside each section, `Blank{n}x{j}Inside` is what the next hewing's inside work takes away (the floor, or the bare logs). `Hewn{n}x{j}` is what the inside work leaves in its place, and `Char{n}x{j}` the char over each section. `Hewn{n}x{j}Step`, inside it, is the face the axe leaves where that section's floor meets the char of the next, shown only while the next is uncut. The hewn wood, on the floors and inside the walls, takes the shape's `hewn` texture. `Shell` is the finished hull, in the same sections and ends. The behaviour shows the ones the build is at and hides the rest, and only `Shell` once the vessel is finished; the item lists the stages in its `ignoreElements`. A spoiled blank's faces take the shape's `char` texture in place of `inside`, `bark` and `rings`, the log's own faces the blank starts in, and of `hewn`. Our dugout's builder, `bin/lib/hulls/dugout.py`, draws them.

A vessel built from a row of logs is too heavy to carry, so the row must lie by the water. Players push it in on foot, by vanilla's `repulseagents`, as they push any creature they walk into: give it that behaviour, `movable` as it is by default.

List `roughseas:burnout` before `creaturecarrier`, so that a click with clay, dry grass, firewood or a torch builds the vessel.

### Framed in place

`roughseas:framing` builds a vessel the way a bark canoe was really made: its bark comes off a standing tree in one sheet, and its frame goes into that sheet where it lies. A knife held on a standing trunk of your vessel's wood, at least `trunkBlocks` tall, for `barkSeconds`, strips the whole trunk and drops the vessel beside it at the start of its build: the entity of your vessel's code and that wood. Any vessel with this behaviour whose code ends in a wood (`barkcanoe-{wood}`) is stripped off that wood's grown logs, `{domain}:log-grown-{species}-ud`, and the trunk becomes our `roughseas:debarkedtrunk-{wood}`, a grown log of its tree with its bark gone, so that it still fells whole and holds its leaves. That block exists for the woods in `config/vessels/canoebarks.json`, every mod's included; a vessel of another wood is not stripped, and the server's debug log says so.

The vessel is then laid stage by stage, in the order of `stages`, each piece held on it for its stage's `seconds`. A stage's `work` says what lays a piece of it:

- `lath`: a lath, wet. Two to five logs lying end to end with a splitting wedge on each split into poles as long as the row once an axe has driven every wedge home, and an axe splits a pole into laths of its length in the crafting grid; laths soak in two days, in a barrel or bundled under stones on the bottom of any water, and dry again in two days. The first lath laid makes the vessel's frame of its wood, and laths of another wood are refused.
- `pole`: a pole, split from such a row. Like laths, the first makes the frame of its wood.
- `hide`: a hide of one of `hideStates`, which covers as many of the stage's pieces as small hides it is worth (small 1, medium 2, large 3, huge 5) and is held on it as many times `seconds`.
- `rope`: vanilla's rope, one at a time. A rope held on the vessel lashes it rather than tying itself to it (a Harmony prefix on `ItemRope.OnHeldInteractStart`), until its lashing is done. A hold kept on past its last lashing ties nothing; a fresh click ties it as vanilla does.
- `pitch`: hot pitch from any liquid container, or from the cooking pot it was cooked in, `litres` a piece; the pot is emptied with its last portion. Pitch that has set is refused.
- `hew`: any axe, which loses a point of durability a piece.
- `cut`: any knife, which loses a point of durability a cut. Vanilla's knife handles its own held step and release, so a Harmony prefix on `ItemKnife.OnHeldInteractStep` and `OnHeldInteractStop` hands them to the build while the vessel wants a cut.
- `cloth`: a piece of cloth.
- `plank`: a plank, `planks` the item code.

While a hold does work, the holder works with both hands; it stops when they let go or the piece is refused. Rope going in, a `rope` stage or the rope that binds a pile, a hide sewn on in a `hide` stage, and twine binding up lashings tie knots, our player animation `roughseas-tie`; every other piece plays vanilla's `knifescrape`, as on a hide scraped on the ground. An axe's hewing and driving keep their own swings.

Every piece of a `lath` or `pole` stage takes one at least as long as it, cut down; a shorter one is refused. The shape names each piece's length: its builder writes `"metres"` on the piece's group, measured along the run its frame texture draws (`stages()` in `bin/lib/hulls/shape.py` does it), and an item's length is its `length` variant.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `stages` | list | none | what it is laid with, in order: each a `group`, a `work`, `seconds` a piece, and the flags below |
| `group` | shape group | none | the group that draws the stage, one child per piece: `Frame1`, `Frame2`…; the count of children is the count of pieces. A look at the vessel shows the stage by the lang key `roughseas:shipyard-stage-{group}` in lower case, with the pieces laid and the pieces in all |
| `work` | `lath`, `pole`, `rope`, `pitch`, `hew`, `cut`, `cloth`, `plank` or `hide` | `lath` | what lays a piece |
| `seconds` | s | 0 | how long a piece is held on it: the real time of that work over sixty |
| `lines` | bool | false | its laths line the shell: the shell's `backing` backs it only where they are laid |
| `members` | bool | false | its laths are the members of the shell's `frame`: a broad load meets only those laid, spaced wider while some are missing |
| `sets` | bool | false | its laths are bent in wet: the vessel is finished only once the wettest of them has dried |
| `seals` | bool | false | the hold holds water only once the stage is done; until then water runs through it |
| `litres` | l | 0 | the pitch one piece takes |
| `optional` | bool | false | the vessel is finished without it, and it goes on any time after: an outrigger's washstrakes |
| `raisesMetres`, `raisesLitres` | m, l | 0 | how much higher the vessel's side and how much more its hold, once the stage is done, on top of its `depthMetres` and its bilge's `capacityLitres` |
| `part` | part name | none | the part of the vessel's `parts` its pieces make, weighing as far as they are laid, in the wood they are of; pieces of one wood only, and the vessel's wreckage of them comes in it |
| `texture` | texture code | none | the shape's texture that draws the pieces, redrawn in the wood they are of |
| `replaces` | shape group | none | a group whose pieces each piece of this stage takes away once laid, `{replaces}{n}` for `{group}{n}`; with `successive`, the one piece `{replaces}1` the first piece takes away: an outrigger's float logs, for the first pass hewn from them |
| `successive` | bool | false | each piece is the whole work again, a step further, and only the last laid shows: an outrigger's float, hewn down pass by pass |
| `follows` | stage or lifted group | none | a stage, or a group stages lift, whose progress this stage's pieces are drawn at: each piece shows from `{group}{n}At{k}`, `k` the pieces that stage, or the stages lifting that group, have laid, so a piece moves with what it rests on (an outrigger's booms settling onto the float as it is hewn, a currach's gunwale laths drawn together at its stern by its second end lashing). A stage may follow itself |
| `lifts` | shape group | none | a group this stage raises piece by piece, counted with every other stage that lifts it: `{lifts}{k}` shows while `k` of their pieces are laid (a bark canoe's skin, from flat through staked and raised to trimmed) |
| `metres`, `woodLitres`, `waterlineSquareMetres` | in the shape | none | written by the builder on each piece's group: the lath or pole it takes, and, on a `successive` stage and on the stage's own group for what lay before its first piece, the wood that is left and how wide it stands at the water |
| `bends` | stage group | none | a stage this one bends once its first piece is laid: from then on that stage's pieces show from the group `{bends}Bent`, `{bends}Bent1`, `{bends}Bent2`…, and no longer from their own: a coracle's lattice, bent up by its gunwale |
| `framePart` | part name | none | the part of the vessel's `parts`, by its `name`, that its laths or poles make: its share and its `reference` stay, the material is their wood, and only what is laid weighs and takes knocks. The shape's `frame` texture is drawn in that wood |
| `laths`, `poles`, `rope`, `pitch`, `cloth`, `planks`, `hides` | item codes | none | what each work takes, wildcards allowed (ours `roughseas:lath-*`, `roughseas:pole-*`, `game:rope`, `game:glueportion-pitch-*`, `game:cloth-*`, `game:plank-*`, `game:hide-*`) |
| `hideStates` | list | none | the hides' `type` variants a `hide` stage takes: ours every stage before leather, `raw`, `soaked`, `scraped`, `prepared`, `oiled`, `pelt` and `salted` |
| `barkSeconds` | s | 0 | how long the knife takes the bark off |
| `trunkBlocks` | count | 0 | the shortest trunk a sheet comes off |

At every stage it is a vessel: it floats and can be picked up, its build goes with it onto the item and back, and the item shows it as far as it is built, and it takes its seats only once finished. Shift + right-click picks it up with an empty hand or holding a piece of its build, as any carried vessel. Every build, `roughseas:framing`, `covering`, `rigging` and `burnout` alike, answers from its next step: the interaction help, the line a look at the vessel shows, and, for a right-click that does no work (an empty hand, or an item no step takes), an in-game error `roughseas-build-step` naming the step and what it needs, such as "Lash it with rope (Crossings bound: 1 of 3 ropes)". A vessel stripped off a tree gives the shape a group `Sheet`, the bark as it lies on the ground before the vessel's frame goes in, and a group `Skin`, the bark raised into the hull, one child a step: the behaviour shows the sheet until a piece is laid, then the step the vessel's `lifts` stage has reached. The group and each step carry `depthMetres`, how deep the vessel stands at it, keel to rim, as its builder measures it (the group's for the sheet): the vessel floats as deep as the step it shows. The item lists `Sheet` in its `ignoreElements`, and may list a stage's `replaces` group: the item of a vessel still being built shows both as far as the vessel is built. Our bark canoe's builder, `bin/lib/hulls/barkcanoe.py`, draws them.

### Rigged in place

`roughseas:riggable` on a finished hull and `roughseas:rigging` on the vessel it becomes build one vessel onto another, the way an outrigger was really made: a finished dugout, a float log alongside, booms lashed across both. One of the successor's `poles` held on a riggable hull, for the successor's first stage's `seconds`, with `floatLogs` logs of one wood lying end to end along its `floatOffMetres` off to either side, the middle one beside its middle where the model draws the float, lays its first boom: the logs are taken away and the hull is replaced by the entity of its `vessel` code and its own wood, at the start of its rigging, moved by what is left of a block so that the float the model draws lies exactly on the logs. What a hull keeps (its cracks, the water in it) goes with it, through the vessel behaviours that implement `IHandedOver`, turned end for end when the float lay on the side opposite the rigged model's. Riders get off, and what its accessory slots held drops beside it. A hull still being built is refused.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `vessel` | entity code | none | the vessel it becomes, without its wood (ours `roughseas:outrigger`); it carries `roughseas:rigging` |

`roughseas:rigging` is `roughseas:framing` started that way: the same `stages` and keys, its first stage begun with the boom laid, and no bark. On top of them:

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `floatLogs` | count | 0 | the logs its float is laid from |
| `floatLengthMetres` | m | 0 | its float's length, for the water it drags as the vessel heaves |
| `mast` | shape group | none | the stage whose first piece stands its mast: until it is laid the vessel has none, and draws no lightning |
| `floatOffMetres` | m | 0 | how far off its centreline they lie: the row is found through the block that far out, within half a block |
| `floorMetres` | m | 0 | the top of its hold's floor above its origin, its hull's: the bilge draws its water from there |
| `floatStage` | stage group | none | the `successive` stage that hews its float from its logs: the float weighs as the wood its last pass left, and stands at the water as wide, from the three logs to the finished float |
| `floatPart` | part name | none | the part of its `parts` its float makes: its share and its `reference` stay, the material is the float's wood. The shape's `float` texture is drawn in that wood |
| `rigged` | shape groups | none | groups shown only once every piece it needs is laid, such as vanilla's `SailUnfurled` and `SailHalf` on a vessel whose sail its shape draws; until then its sail stays furled. Our outrigger's sail is drawn by `roughseas:windage`, which waits for its build itself, so its list is empty |

A vessel already in a world from before, with no build of its own, is finished with every stage on, the `optional` ones too, its float, poles and pieces of its hull's wood. Our outrigger's builder, `bin/lib/hulls/outrigger.py`, draws it on the dugout's own hull.

`roughseas:floatlashing` beside `roughseas:rigging` gives its float lashings of their own, as a raft's ([An outrigger's float](Hull-Condition#an-outriggers-float)). A knock from the float's `side`, and a broad load (a blast, lightning) that lands outside its hull on that side, meets the float instead of the hull, and works the lashings loose by its share of `impactCapacity` times the share of the `lashing` stage laid: a float not yet lashed comes away at any knock. A point blow or a cut on the float only chips its wood. A knock that works them a stage looser plays the `lashed` hull's voice from `config/soundscape.json`, its harm, and its break once they give out. `repairs` bind them up again, as a hull's do. Once they give out the vessel is replaced by its `hull` vessel of its own wood, which takes its hull's cracks and bilge through `IHandedOver`; its float breaks into firewood, every `firewoodLitres` of what is left of it a piece, each laid pole comes away as a pole as long as its piece, its planks as planks of their wood, and each piece of a stage `keeps` names as that item, the rest of its lashings and seams parting. Its riders keep their seats in its `hull`, and water its hold had beyond the `hull`'s own spills over that gunwale. The state lives in its `roughseas:vessel` tree as `float/lashing`. Its shape draws the float as its lashings give in the groups `FloatLashingLoose`, `FloatLashingTorn` and `FloatLashingShifted` under `FloatLashing`, each with a `replaces` list of the sound rig's elements it is drawn instead of; one shows once every piece of its `lashing` stage is laid: loose from a tenth of the lashings gone, torn from a half, shifted from three quarters. Its item leaves out `FloatLashing`.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `side` | hull place | `StarboardSide` | the place its float stands off, in its shape's frame |
| `impactCapacity` | J | 0 | the knock its lashings, all laid, take before they give out |
| `hull` | entity code | none | the vessel it is left, without its wood (ours `roughseas:dugout`) |
| `keeps` | stage group → item code | none | what each piece of a stage comes away as |
| `firewoodLitres` | L | 0 | its float's wood a piece of firewood holds |
| `repairs` | | none | as the hull's |

`roughseas:rigging` takes `lashing` for this, the stage group that lashes its booms to its float.

### Bound in place

A block of class `roughseas:BlockBundles` is bundles lying where they were laid, the way a reed boat was really made: two long bundles side by side by the water, lashed together. It has the variants `port` (the plant the first bundle was bound from), `starboard` (`none`, or the plant of the second) and `axis` (`we`, `ns`): a bundle placed lies along the way its placer looks, and a second held on it lies beside the first, its `port` becoming the block's `starboard`. Ours takes the code of the vanilla tops item as the plant (`cattailtops`, `papyrustops`, `thatch`), so the vessel's `wreckage` gives each back as `game:{port}` and `game:{starboard}`. Rope held on the pair for the first stage's `seconds`, wherever it lies, takes the block away and leaves, where it lay, the entity `{vessel}-{port}-{starboard}`, each bundle on the side it lay, framed with that first stage's first piece laid: its rope the first lashing. Everything after is `roughseas:framing`, with no bark and no wood: our reed boat's stages are `Lashing` and `Tips`, which `replaces` its flat ends (`Flat1`, `Flat2`) with bound-up ones. Draw the block's `one` and `two` shapes from the same geometry as the vessel's `Blank` and `Flat` groups, as `bin/lib/hulls/reedboat.py` does, so the bundles become the vessel without moving.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `vessel` | entity code | none | in the block's `attributes`: the vessel the bundles are bound into, without its `port` and `starboard` variants (ours `roughseas:reedboat`); it carries `roughseas:framing`, whose first stage's `work` is `rope` |

`roughseas:droop` draws ends that stand up by the stiffness of their reed and sag as the hull soaks. Each end's group has one child per sag, `{end}Droop0` dry to `{end}Droop{n}` as soaked as the hull gets afloat, and the behaviour shows the one nearest the water its material has taken up against what it can take before, with nothing aboard, it no longer holds the hull up. The item lists every sag but `Droop0` in its `ignoreElements`.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `ends` | shape groups | none | the groups of the ends that sag (ours `Tips1`, `Tips2`) |

### Laid out on the ground

`roughseas:covering` builds a hide boat the way coracles and currachs were really made: laths laid out on the ground, bound into its first frame where they lie, then framed and covered with hides. It is `roughseas:framing` begun that way, with the same `stages` and keys and no bark.

Laths of every length, wet or dry, go down on the ground with Shift in vanilla's ground storage, as a pile ([For modders](For-Modders#poles-and-laths-piled-on-the-ground)). Rope held on a pile of exactly the vessel's `start`, so many wet laths of that length of one wood and nothing more, binds them, for its second stage's `seconds`: the pile is used up and the entity of its code is laid where it lay, along it, its first stage laid and its second begun, in the laths' wood. Any other pile stays a pile; a dry one of the right count is refused, saying the laths must be soaked. Vanilla's rope is ground-storable and would take a lath off the pile, so a Harmony prefix on `BlockEntityGroundStorage.OnPlayerInteractStart` hands rope's click on a pile it binds to the binding. Laths dry in the pile as in the hand.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `start` | object | none | the one pile that starts it: `{ "laths": 14, "metres": 2 }`, the count of laths and their length (our coracle 14 of 2 m, our currach 2 of 5 m). Our builders write it. Draw the pile's first layer as the vessel's first stage, as `bin/lib/hulls/coracle.py` draws the 2 m pile's first layer as the coracle's flat lattice, so the blank appears with nothing moved |
| `overturned` | bool | false | the vessel is built upside down, and is finished only once turned over: a right-click with an empty hand turns it, once every piece it needs is laid |
| `overturnedPivotVoxels` | voxels | none | `[x, y, z]`, a point on its shape the line along its length it is turned about runs through, so that its gunwale lies on the ground; our builder writes it beside `overturnedPivotMetres`, the same point in metres, which the mod doesn't read |

A `hide` stage waits until the frame has set: a hide is refused while what was laid wet is still wet. A vessel bound anew starts ungreased. One from before the build, with no build of its own, is finished, the right way up, and covered as its hull's `cover` says. Our coracle's and currach's builders, `bin/lib/hulls/coracle.py` and `currach.py`, draw them, and print the ropes their lashings take and the sections their covers come to.

## The hull

`roughseas:vesselintegrity`: the hull ([Hull condition](Hull-Condition)). Every blow lands on one of seven places and meets what really takes it ([How a hull takes a blow](Hull-Condition#how-a-hull-takes-a-blow)):

- **broad**, against the whole frame, `impactCapacity`, or on a shell with a `frame`, against the shell where it lands and the member behind it: knocks, rams, rapids, a throw ashore, and damage from the sources `config/vessels/damage.json` lists as `broadSources` (explosions and lightning);
- **point**, against the shell where it lands: any other damage the entity receives through vanilla, a weapon, a fall, a mod's cannonball, which needs no code of ours;
- **cut**, against the shell's fibres: slashing damage, and a swing with any tool that cuts wood faster than a hand;
- **slam**, a breaker's pressure, against the planking's bending strength.

A broad load cracks its place by its share of the capacity and breaks the hull up when it reaches all of it, less what a `bundle` shell crushes away first. On a shell with a `frame`, it opens the shell where it lands (a `skin` gives and opens nothing) and breaks the member behind it: the shell's opening and the member's breaking split the place with the member broken behind it, twice that stoves it in, and only what is left past that meets `impactCapacity` and can break the hull up; a point blow or a cut opens at most its place; a breaker that beats the planking stoves its place in. A `cracking` hull also breaks up once all seven places are stove in. How much damage counts as how hard a blow, and how much the material's strength varies from one blow to the next, are in `config/vessels/damage.json`, which a patch can change.

| Key | What it is |
| --- | --- |
| `model` | how the hull is made: `cracking` (the default), cracked place by place and leaking, as our wooden, bark and hide hulls; `lashed`, one lashing that blows work loose, which never floods, as our raft; `reed`, which soaks and tears, can't be mended, as our reed boat. A `lashed` or `reed` hull takes a point blow or a cut as a seventh of a place's share, the hull being one whole |
| `impactCapacity` | the energy in joules a broad load must reach to break up a hull of its reference material; a load of a third of it cracks its place a third of the way to stove in. On a shell with a `frame`, what a broad load leaves past stoving its place in must reach it. Without it, no broad load harms the hull |
| `shell` | what the hull's outside is, for point blows, cuts and breakers; see below. Without it, the frame's capacity resists point blows and cuts too, and no breaker harms the hull |
| `minImpactSpeed` | the speed in m/s a hull takes in its stride when it runs into something, a block or another hull: a knock below it is no knock, and only the energy above it counts. A knock against a block is read once, when the block first stops it, at the speed its position really changed by just before. Default 1 |
| `parts` | what it is built from, each `{ "material": "wood-{material}", "reference": "wood-oak", "share": 0.6 }`: the material, the one its capacity and `weight` were set for (the material itself when left out), and its share of that weight, in proportion to the other parts' (default 1); a `name` lets a build find the part whatever it is made of, and `"takesKnocks": false` keeps a part (a float, a rig) in the hull's weight and buoyancy but out of its strength. Each part scales the capacity by its strength against its reference's, and its share of the weight by its density, from `config/vessels/materials.json` |
| `depthMetres` | keel to gunwale amidships. With it the vessel floats by its mass; without it, at its `freeboardMetres` |
| `waterplaneSquareMetres` | the area it cuts at the water, for a hull with a hold. Leave it out for a solid one, a raft or a bundle, which presses down through its own depth |
| `hullBoxMetres` | `{ "from": [x, y, z], "to": [x, y, z] }` in metres from the point the entity stands at, x along its length, y up and z across: the finished hull its places, size, drag and heave are measured on. Without it, the element its `HullAP` attachment point hangs from, measured at runtime. Our builders write it round each finished hull, with `hullBoxVoxels` beside it |
| `seep` | `{ "litresPerHourASeam", "greaseDays" }` on a cracking hull: what each seam between hides lets in once ungreased, and how many days afloat a full greasing lasts |
| `cover` | `{ "hides": { "prepared": 2 }, "seams": 0 }` on a cracking hull: the hides it is covered with, by their `type` and the small hides' worth of each, and its seams, when no build says otherwise. A cover rots and soaks afloat by its hides, from `config/vessels/hides.json` ([Seeping and soaking](Hull-Condition#seeping-and-soaking)); a hull built with `roughseas:covering` is covered with the hides sewn on it |
| `soak` | `{ "kgPerDay", "dryKgPerDay", "rotShare", "saturationKg" }` on a reed hull: the water it takes up a day afloat, what it dries a day in full sun, the share of what dries that stays as rot, and the most it holds |
| `runsRapids` | true if rapids don't strike it |
| `repairs` | what mends it: `material` (an item stack), `restores` (per patch: how much of one place's crack it closes, or of a lashed hull's lashing), `saturation` (satiety it costs), `seconds` (how long a patch is held on the hull, the real time of that work over sixty, as a build's pieces are), `greases` (true if it greases the seams instead, `restores` then being the share of `greaseDays` it gives). Every material a vessel's `repairs` name carries our collectible behaviour `roughseas:framing`, which holds the patch on the hull as it holds a build's piece |
| `wreckage` | what it leaves when it breaks up, as a block's drops |
| `damage` | `material` (`wood`, `bark`, `reed` or `hide`), which picks the hull's [sounds](#sounds), and `textures`, the shape's textures that crack: for a hull whose shape draws no wounds, an overlay of `wood` or `reed` marks on each damaged place |

`repairsByType` and `wreckageByType` work as vanilla's `ByType` keys do.

**The shell.** `shell` says what a blow meets where it lands, with its `kind`:

| `kind` | What it is | A point blow and a cut | A breaker |
| --- | --- | --- | --- |
| `rigid` (the default) | planks, logs, bark | hole it where they land | bends the shell and its backing between the frames |
| `carved` | a hull hollowed from one log | as `rigid` | bends each side over, from the bottom up |
| `skin` | hide over laths | spring off it unless the backing is there, then break the backing; a cut tears the skin | bends the laths |
| `bundle` | bundled stems | spring off; a cut parts only the backing, its lashings; a broad load crushes the stems first, and only what is left meets `impactCapacity`, its lashings | does nothing |

Its keys: `material`, a row of `materials.json`; `metres`, its thickness; `spanMetres`, how far what a breaker bends reaches between what holds it (frames apart, a lath's length, a carved side's height); `backing`, `{ "material", "metres" }`, what lines or holds it; and `backedShare` and `bottomBackedShare`, the share of the sides and of the bottom the backing covers (1 for a lining, the laths' share of the area for a skin); and `frame`, `{ "material", "metres", "widthMetres", "spacingMetres", "spanMetres" }`, the laths or ribs behind the shell: their depth, breadth, spacing centre to centre and length between the members they cross. A broad load lands on one by their breadth over their spacing and breaks it by its `toughness` across that section; between two, it breaks both, and a `skin` with a material's `tensileStrength` and `elongation` first stretches across the gap until its pull breaks them. A build gives the frame and the backing the wood of its laths. A thicker shell of a tougher material holds more blows; a thicker one on closer frames holds bigger breakers. The material's `toughness` (and `cutToughness`, where an edge parts it differently) and `strength` come from its row in `config/vessels/materials.json`, and a `bundle`'s crushing from its `crushStrength` in MPa and `densificationStrain`, across the bundle's `metres` along the metre of it a block meets: add a row by patch for a material of your own. Our vessels each carry a `shell` with its sources; the coracle's is above. How the numbers combine: [The science of the hull](The-Science-of-the-Hull#hulls).

**Places.** The seven places, bow, side and quarter each side and the bottom, are taken from its hull box: `hullBoxMetres`, or the element its selection box `HullAP` hangs from, or the entity's selection box without either. A blow lands by where it comes from: a knock and an explosion by their direction, a weapon by where it hits, a breaker on the side the waves come from, rapids and a throw ashore on the bottom. The overlay's marks go on the shape's elements whose centre lies in a cracked place, worked out from the model: there is nothing to configure.

**Wounds.** A shape can draw each place's crack in the model itself, seen from outside and from aboard. Under a group `Wound{Place}`, beside a board of that place, it draws the crack at each stage it can reach, `Wound{Place}Scratched`, `Cracked`, `Split` and `StoveIn` (`Place` as `PortBow`, `StarboardSide`, `Bottom`); the vessel shows the one its crack has reached and hides the rest, with no key to set. A build stage that draws its own skin draws its own wounds, named with its level after the form: `Wound{Place}{Form}{level}` under `Wound{Place}{level}`, as our dugout's `WoundPortBowSplitBlank3`, hidden with the level. Our builders draw them through `bin/lib/hulls/wounds.py`: each crack through the wall along the grain, as long as the shell's `spanMetres`, a stove-in place broken inward. A frame under the skin breaks with it: the member nearest each place sits whole in `Wound{Place}Whole`, hidden from a split on, and beside it `Wound{Place}Frame` holds it broken, `Wound{Place}SplitFrame` and `Wound{Place}StoveInFrame`. The item leaves out the `Wound{Place}` and `Wound{Place}Frame` groups.

**Knocks.** A knock is vanilla's own collision: when the physics stops the hull against a block, the speed it loses into that block strikes it, so a glancing blow counts only the part pointing into the wall. It takes vanilla's passive physics on the entity (`passivephysicsmultibox`, or `passivephysics`). A drifting vessel is measured on the server and a steered one on its rider's client, which reports to the server.

**Rams.** Two vessels with `vesselintegrity` ram when their hulls first touch at a closing speed above `minImpactSpeed`, and only if they lie at the same height. Both take the same broad load, the lighter vessel's weight and the closing speed deciding how big ([why](The-Science-of-the-Hull#knocks)). A ram with a steered vessel is measured on its rider's client, one between two drifting vessels on the server. A vessel without `vesselintegrity` neither rams nor is rammed; a creature or a player only pushes a hull.

A knock strikes with ½ · mass · (speed² − `minImpactSpeed`²), where the mass is everything aboard:

- the hull, its `weight` in its own material;
- each seated passenger by its entity's `weight`, a player as [the person module](For-Modders#a-body-in-the-water) weighs them: the 75 kg body with its hotbar's tools and weapons and its armour, weighed from how much the armour slows its wearer (a worn item's `statModifiers.walkSpeed`, 83 kg for each 1 of slowdown);
- each accessory by the category it attaches as, at its empty weight (`basket` 2 kg, `crate` 12, `chest` 20); what they hold, and what a `roughseas:cargo` hold carries, doesn't count;
- the water in its `roughseas:bilge`, by how full it is: a full bilge counts all its weight, a half-full one half.

Give `impactCapacity` by the knocks the hull should take: our coracle's 400 J and the sailboat's 40,000 bracket the roster, and a hull of skin over laths gets a `frame` as well, and [Hull condition](Hull-Condition#knocks-and-landings) shows what a knock does to each.

**Leaks.** A cracking hull's cracks leak as a hole does, faster the bigger the crack and the deeper it lies under the sea outside, and slower as the water inside rises to meet it ([Leaks](The-Science-of-the-Hull#leaks)). A bottom crack lies at the keel, any other a quarter of the way up the side, so a light hull's side cracks may ride above the water until it is laden. The numbers are in `config/vessels/hull.json`: `dischargeCoefficient`, the share of a hole's area that flows (0.61); `crackSquareCentimetres`, a stove-in crack's hole (25), which a blow must break the shell around; `sideCrackShare`, how far up the side a side crack lies (0.25); and `soakedStrengthShare`, the share of its dry strength wet wood bends to breaking under (0.57).

**Carried as an item.** A vessel picked up carries its whole hull on the item, in the stack attribute `roughseas:vessel`, every crack and soaking in place, and shows its condition as the item's durability. That takes our item class `roughseas:ItemVessel` with a `durability`, and the item's code the same as the entity's. Shift and right-click picks it up when the entity's attributes carry `rightClickPickup: true`; a sunk one can't be lifted. A selection box `HullAP` gives the hull a box of its own for mending, apart from the seats.

## Floating

A vessel with `depthMetres` floats by Archimedes, on both sides; its draft is weighed twice a second. Its draft is its full mass over 1025 kg/m³ times `waterplaneSquareMetres`; a solid hull's is its depth times its mass over what its own volume of seawater weighs, no deeper than its depth. Its freeboard is what is left of `depthMetres`.

- **The full mass** is the hull, each rider's body, what they carry, the containers and the water aboard. A patch can change the weights: `containerKg` in `config/vessels/mass.json`, and a player's in `config/person.json` (`body.massKg`, `carried`).
- **A vessel reaches its draft by its own mass.** A change of load or of water aboard is a force on it and on the water it drags with it, damped by the water (`heaveDamping`, in the `response` block of `config/storms.json`). The water it drags is that of a flat plate of its waterplane: its `waterplaneSquareMetres` at the beam of its hull box, or that box itself for a solid hull, and an outrigger's float as a plate of its own. It sinks or rises to its new draft on the period [The Science of the Hull](The-Science-of-the-Hull#riding-the-swell) gives, and never past where it floated before. This takes the place of vanilla's buoyancy for it: its `boyancyMul` is 0 while it acts, and vanilla's drags, a flowing block's push and other entities' repulsion keep their effect along the water but not on its heave.
- **A rider saved in its seat** who hasn't loaded yet, as when a world opens with someone aboard, weighs the body's `massKg` until they do.
- **The hull's mass and volume** come from its `parts`. Its `weight` is the reference build's, and each part's share of it has the volume of that share in its `reference` material; in its own material that volume weighs by its density. So an ebony dugout has an oak dugout's shape and weighs as ebony.
- **Densities** are a row's `density` in `config/vessels/materials.json`, in kg/m³, with a `densitySource`. A row may carry a density alone, as `reed`, `hide` and `iron` do. A wood the table doesn't know weighs 610, the middle of the vanilla woods, takes the unknown wood's `strength` and `toughness` too, and the server log names each once.
- **Flooding.** When its freeboard reaches nothing, the sea fills its bilge. Flooded, or solid, it sinks when what the water doesn't hold up, the hull, what the riders carry and the containers, outweighs what its own volume of seawater weighs; riders' bodies are held up by the water. A sunk vessel throws off its riders, lies on the bottom, fills again as it is bailed, can't be picked up, and breaks up for its wreckage under an axe.
- **The line it is drawn at.** Vanilla's `swimmingOffsetY` places your shape in the water. We take that as where it floats with its reference build and one rider aboard, find its keel from that draft and put vanilla's swim line there, so vanilla's `Swimming` means its keel is wet. Set it so the shape looks right with one aboard; `bin/model build` sets ours from the draft it measures.

To give a vessel of yours draft and buoyancy: list its `parts` with materials the table knows (or add rows by patch), set `depthMetres` from keel to gunwale, and set `waterplaneSquareMetres` so that what its bilge holds settles it by its freeboard. Ours are measured that way ([Measured from the model](#measured-from-the-model)): a full bilge with one aboard brings the gunwale to the water. Leave `freeboardMetres` off its `seaforces`.

## Sounds

A vessel with our behaviours sounds without a key of its own. Its hull speaks by its `model` and its `damage.material`, from the voices in `config/soundscape.json` under `hull.models`: a `cracking` hull as wood, bark or hide, a `lashed` one by its lashings, a `reed` one with a crunch. A material without a voice sounds as wood. A blow that leaves its place no worse to see knocks, and one that does cracks or strains, played where the blow landed and louder the harder it was; a break-up breaks from the middle of the hull. With `roughseas:seaforces`, each wave slaps, a breaker slams and water shipped into a hull with a `roughseas:bilge` splashes in, from the windward side. Its bilge sloshes and each crack trickles from where it is, as loud as it leaks, played by each client from what the bilge and the hull already sync. Afloat, it splashes once for each crest of the drawn sea that passes it, from the side the waves come from and as loud as that wave is high. Every sound, and how loud it plays for its cause, is in `config/soundscape.json`, which a patch can change.

For sounds of your own, the server raises events on the behaviours:
- `EntityBehaviorVesselIntegrity.Struck`: the place struck, the blow over what resisted it, and what it did (`Harmed` when its place came to a worse stage, `BrokenUp`, or null otherwise). `PointAt(place)` gives the place's point to play at.
- `EntityBehaviorVesselIntegrity.BrokeUp`: as the hull breaks into wreckage.
- `EntityBehaviorSeaForces.Took`: each wave it takes, with its height, whether it broke on the vessel, the water it shipped and the direction it came from.

## Mooring

`roughseas:mooring`, on both sides, with `roughseas:seaforces` and `roughseas:currentdrift`; it has no keys. It holds a vessel tied to a stake, and one at anchor ([Mooring](Mooring), [Anchoring](Anchoring)).

- **A stake.** Vanilla's `ropetieable` on both sides: one rope from the vessel to a fence post or a growing trunk. A vessel refuses a second rope. The rope holds the vessel's bow at its own length, which the player sets with the rope's tool modes.
- **An anchor.** An accessory slot (`rideableaccessories`) that takes vanilla's anchor, as vanilla's sailboat has, with the anchor's `rodeMetres`, `rope` and `kilograms` attributes. Spare rode is any item with a `rodeMetres` attribute in the same accessories, such as our anchor warp.

Each line only pulls, at its end, and stretches by its rope's material: its tension is a force on the hull, solved exactly over each tick as a spring on the hull's mass, with the water's steady pull shared by the taut lines. A vessel reaching a line's end at speed jerks it and springs back, by its speed and its mass, and the highest tension within each tick is what the line is judged by. A line pulled past its breaking load parts; an anchor pulled past what it holds on the bed drags, and holds the vessel back by its friction as it goes. It all runs where the vessel's physics runs: on the server for a vessel nobody steers, on its rider's client for one being steered, which reports a parted line to the server. At anchor, a vessel ships less water, though a breaker slams it in full. With no player near, a storm can throw it ashore.

## Currents

`roughseas:currentdrift`, on both sides. It moves a vessel afloat by the water, where its physics runs: on the server while nobody steers it, on the rider's client while someone does, which reports the set to the server so the server carries on from it. The velocity the water has given the hull (its set) is on top of the way it makes through the water. The set is the drawn waves' water at the hull, as `roughseas:seaforces` reads it, plus what the forces give it over the hull's mass and the water it drags with it: the water's drag on the hull's whole velocity through the water (less the part its own way meets, which its rower pays), the waves' push on a hull moving through them ([How the waves move a hull](The-Science-of-a-Body-in-the-Water#how-the-waves-move-a-hull)), the tension of its `mooring` lines, and the wind's push of `roughseas:windage`, whose moment also turns a hull no line holds by the bow. A rower's speed through the water is what it would be in still water, and a collision that stops the hull stops the set on that axis. The entity must be an `EntityBoat` with vanilla's passive physics (`passivephysics`, or `passivephysicsmultibox`).

The drag is `HullDrag`, the one `roughseas:propulsion` drives against, from `vesselintegrity`'s live draft and mass and hull box. A vessel with `roughseas:propulsion` configures its form there ([Driving it](#driving-it)). One without it, as vanilla's sailboat is, takes the same keys under `roughseas:currentdrift`:

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `bowDrag` | | `drag.json` | a blunt bow's own drag on beam by draft |
| `sideDrag` | | `drag.json` | cross-flow drag on length by draft |
| `finSquareMetres` | m² | 0 | a keel's or float's side, added to length by draft |

A vessel without `vesselintegrity` has no draft to take drag on, and comes toward the water's speed at the rate of vanilla's water drag. Held by `mooring`, a vessel turns about its bow to lie along the stream, and the current's drag and the waves' push on it pull on the rope and the anchor. The waves at the hull are read by `roughseas:seaforces`, which a vessel needs for them.
