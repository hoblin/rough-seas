A vessel becomes one of ours by data, not code: a few behaviours on its entity, configured in JSON. They float it by its mass, take the sea for it (waves over the side, breakers on the hull, a ride on the drawn swell), hold its bilge, crack, flood and sink its hull, moor it and drift it with the current. Vanilla's raft and sailboat get theirs by patch. This page is for a mod that adds a vessel, or makes its own vessels ride our sea.

## A vessel in our sea

A vessel meets [both of our seas](Reading-the-Sea#two-seas): the sea state on the server, which decides what happens to her, and the drawn sea on a client, which shows her riding it.

On the server, once a wave period, every vessel in a sea of 0.3 m or more meets a wave. The wave is drawn from the sea state there, most lower than the significant height and a few much higher ([Every wave is different](Wind-and-Waves#every-wave-is-different)). A hull rises with part of the crest. What is left above its side comes aboard, into its bilge, or washes through a hull that has none. A wave that breaks slams the hull too, on its windward side, with a pressure that grows with its speed and is far greater if it plunges. The player's view of all of it is [Surf and swamping](Surf-and-Swamping).

On both sides, a vessel with a hull of ours floats by its full mass: hull, riders, their gear and armour, containers and the water aboard settle it lower, and it floods when its side goes under. On a client, it rides the drawn sea: lifted with the surface under it, rolled and pitched with its slope, on periods of its own mass. Water aboard rolls it harder and slows it, and a full bilge swamps it.

The sea harms a vessel only with [Dangerous oceans](World-Settings#dangerous-oceans) on. With it off, no wave comes aboard or strikes, no rain falls into a bilge, storms neither pull at a mooring nor throw a vessel ashore, and lightning leaves the hull alone. The sea still runs, the swell is still drawn, currents still carry vessels, knocks against rock and rams between vessels still crack a hull, and cracks still leak.

## The behaviours

| Code | Sides | What it does |
| --- | --- | --- |
| `roughseas:seaforces` | client and server | the vessel's contact with the sea: waves aboard, breaker strikes, the ride on the drawn swell, its mass slowing it, the settling it draws. Mooring needs it |
| `roughseas:bilge` | client and server | the water aboard: from waves, leaks and rain, bailed out, swamping the hull when full |
| `roughseas:vesselintegrity` | client and server, first in both lists | the hull: how it floats and sinks, what cracks or breaks it, its leaks, mending, wreckage |
| `roughseas:mooring` | server | a stake rope and an anchor that hold in a storm, or don't |
| `roughseas:currentdrift` | server | the drift of an unsteered vessel with the current and the wind |
| `roughseas:burnout` | client and server | a hull burnt and hewn out of a row of logs, in the world, before it floats finished ([Built in place](#built-in-place)) |
| `roughseas:launch` | client and server | a push from the shore that slides a heavy vessel into the water ([Built in place](#built-in-place)) |
| `roughseas:framing` | client and server | a bark hull stripped off a standing tree and framed piece by piece where it lies ([Framed in place](#framed-in-place)) |
| `roughseas:riggable` | client and server | a finished hull that a boom laid across her joins to a float, making her the vessel she is rigged into ([Rigged in place](#rigged-in-place)) |
| `roughseas:rigging` | client and server | that vessel, rigged piece by piece where she lies ([Rigged in place](#rigged-in-place)) |

Configure each one in your entity's `behaviorConfigs` under its code, as vanilla's behaviours are, and list it in `client.behaviors` and `server.behaviors` as the table says.

They build on each other, so add them in this order. `seaforces` is the vessel's contact with the sea, and the others act through it: the bilge fills from the waves it takes and swamps the hull only through it, and `mooring` holds against the pull it measures. A vessel with `seaforces` alone already rides the swell and takes the sea. Add `bilge` for a hull that holds water, `vesselintegrity` for one that floats by its mass and can crack and sink, and `mooring` and `currentdrift` on the server. `vesselintegrity` and `currentdrift` also work alone.

`vesselintegrity` goes first in both lists, before `creaturecarrier` and `rideableaccessories`, so that a right-click with mending material mends the hull rather than seating the player.

Every key may carry a `sources` entry beside it, text about where each number came from. Our code doesn't read it; we keep a real-world source for every number there, and so can you.

Our [coracle](Coracle), its sources left out, carries all five:

```json
"behaviorConfigs": {
    "roughseas:vesselintegrity": {
        "impactCapacity": 400,
        "parts": [{ "material": "hide", "share": 0.5 }, { "material": "wood-willow", "share": 0.5 }],
        "shell": {
            "kind": "skin", "material": "hide", "metres": 0.005,
            "backing": { "material": "wood-willow", "metres": 0.03 },
            "backedShare": 0.15, "bottomBackedShare": 0.44, "spanMetres": 0.52
        },
        "depthMetres": 0.27,
        "waterplaneSquareMetres": 1.25,
        "repairs": [{ "material": { "type": "item", "code": "game:hide-prepared-small", "stacksize": 1 }, "restores": 0.67, "saturation": 40 }],
        "wreckage": [{ "type": "item", "code": "game:stick", "quantity": { "avg": 3, "var": 2 } }],
        "damage": { "textures": ["hide"], "material": "hide" }
    },
    "roughseas:seaforces": {
        "beamMetres": 1.4, "mastMetres": 0, "windageSquareMetres": 0.5,
        "heave": 0.8, "capsizeBreakerMetres": 0.6,
        "rollResponse": 1.6, "pitchResponse": 1.6
    },
    "roughseas:bilge": { "capacityLitres": 250, "deckSquareMetres": 1.5, "drainLitresPerHour": 0 }
},
"client": {
    "behaviors": [
        { "code": "roughseas:vesselintegrity" }, { "code": "repulseagents" }, { "code": "passivephysicsmultibox" },
        { "code": "interpolateposition" }, { "code": "hidewatersurface", "hideWaterElement": "hideWater/*" },
        { "code": "selectionboxes" }, { "code": "creaturecarrier" }, { "code": "ropetieable" },
        { "code": "roughseas:seaforces" }, { "code": "roughseas:bilge" }
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

## Taking the sea

`roughseas:seaforces`. Leave a key out and it takes the default, which for most of them is zero: a hull with no freeboard and no heave ships every crest. Set them all.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `beamMetres` | m | 0 | the hull's width: how much of a crest it takes aboard, how hard the sea pulls on its mooring, how it rolls |
| `windageSquareMetres` | m² | 0 | what the wind catches, hull and rig: how hard the wind pulls on its mooring |
| `mastMetres` | m | 0 | a mast's height above the hull; [lightning](Lightning) seeks it |
| `freeboardMetres` | m | 0 | the side above the water, empty, for a vessel that doesn't [float by its mass](#floating); ours leave it out |
| `heave` | 0–1 | 0 | the share of a crest's height the hull rises with; what is left climbs its side |
| `rollResponse` | × | 1 | how many times the water's slope across it it rolls on the drawn swell |
| `pitchResponse` | × | 1 | the same along it |
| `capsizeBreakerMetres` | m | none | the breaker that overturns it, empty |

A crest stands half a wave's height above still water, seven tenths when it breaks; the hull rises with `heave` of that. Water aboard adds to the roll, twice as much when full. A vessel that floats by its mass ships water over the freeboard it floats at; one with a fixed `freeboardMetres` loses it in proportion to its bilge, all of it when full.

**Capsizing is switched off** ([Capsizing](Surf-and-Swamping#capsizing)), so `capsizeBreakerMetres` does nothing today. It waits for heel and sailing by the wind in [Seamanship](Roadmap#seamanship): a hull will heel by its stability under any push from the side, the wind in a sail or a breaker on the beam, and overturn past the angle it can't come back from. What a vessel gives for that is not settled yet.

**Riding the drawn swell.** On a client, the vessel is posed on the surface under it, sampled at the ends and sides of a square as wide as its collision box's longer side. A vessel that floats by its mass follows that pose on its own heave and roll periods, from its draft, depth and beam ([How a hull answers](Reading-the-Sea#how-a-hull-answers)); one that doesn't takes the water's pose as it lies. This rides on vanilla's boat: the entity must be an `EntityBoat` or derive from it, and a class of yours that overrides `OnRenderFrame` calls the base, or it isn't posed. A vessel that can't, poses itself on the same surface through [the drawn sea](Reading-the-Sea#the-drawn-sea). Where no swell is drawn it rolls and pitches as vanilla rocks it, times `rollResponse` and `pitchResponse`.

**Its mass slows it.** An `EntityBoat` makes its entity attribute `speedMultiplier` at its reference mass: its own `weight` and one 75 kg rider. Heavier, it is slower, and lighter a little faster ([why](The-Science-of-the-Hull#floating)): the hull in its own material, riders, gear, armour, containers and the water aboard all count. A swamped one makes no speed, and its riders are moved into the water beside it, holding on.

## The bilge

`roughseas:bilge`. A vessel without it holds no water: the sea washes through, as through our raft and reed boat.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `capacityLitres` | L | 0 | what it holds before it swamps |
| `deckSquareMetres` | m² | 0 | the open area rain falls into |
| `drainLitresPerHour` | L per game hour | 0 | what drains out by itself |

Whether a swamped vessel lies awash or goes to the bottom comes from its [buoyancy](#floating), not from the bilge. A vessel without `roughseas:vesselintegrity` never sinks.

[Bailing](Bailing) needs nothing from you: a bowl, a jug or a bucket bails any vessel with a bilge.

To show the water, give the shape a group `Bilge` with one child per level from the bottom up (`Bilge1`, `Bilge2`…), a texture `bilge`, and list `Bilge` in the shape's `ignoreElements`. Our dugout is the example. Without the group the bilge works and the water is unseen.

## Built in place

`roughseas:burnout` builds a vessel the way a dugout was really made: from five logs of one wood lying end to end beside the water, burnt and hewn out a fire at a time. Dry grass laid with Shift on the middle log of such a row, where vanilla would start a campfire, takes the logs away and leaves the vessel there as a blank, the grass its first fire's base: the entity of your vessel's code and the logs' wood. The rule for the row needs nothing from you: any vessel with this behaviour whose code ends in a wood (`dugout-{wood}`) is built from that wood's lying logs, and lying log sections, in every mod's woods.

Each hollowing then takes, in the hollow, what vanilla's campfire takes: clay along the rim, dry grass, four of anything vanilla builds a firepit with (`firepitConstructable`), and a torch or firestarter. Once the fire burns out, an axe held on the blank hews out the char a stroke at a time; the last hewing finishes her.

- **Clay.** A ball covers 25 voxels, as in vanilla's clay forming, and the rim takes as many voxels as the shape's `Clay` group covers seen from above, one voxel thick. Nothing to set: the count follows your shape.
- **A fire without clay** is never refused. While the rim lacks any of its clay, the fire chars the rim at the hollow's rate, and once that char reaches `wallMetres` the blank is spoiled: she is drawn with every face of her wood texture charred, takes no more work, and breaks up as any vessel does.
- **Strokes.** A stroke breaks away a strip of the fire's char as wide as `axeEdgeMetres` across `hollowBeamMetres` of hollow, so a hewing takes the hollow's share of the hold over one such strip. The holder strikes every `secondsPerStroke`, and `hewSaturation` is spread over a hewing's strokes. Every axe in the game, a modded one too, hews.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `hollowings` | count | 1 | the fires and hewings that hollow her out |
| `hollowDepthMetres` | m | 0 | from the rim to the floor of the finished hollow |
| `rimMetres` | m | 0 | the rim's height above the vessel's origin, where the first fire burns |
| `charMillimetresPerHour` | mm per game hour | 1 | how fast the fire chars its way down: one fire burns for the hollow's depth over `hollowings` at this rate |
| `clay` | item code | none | what guards the rim, wildcards allowed (ours `game:clay-*`) |
| `wallMetres` | m | 1 | the char on an unguarded rim that spoils the blank: her walls' thickness |
| `axeEdgeMetres` | m | 1 | the width of char a stroke breaks away |
| `hollowBeamMetres` | m | 1 | the width of the hollow a stroke sweeps across |
| `secondsPerStroke` | s | 1 | how often a held axe strikes |
| `hewSaturation` | saturation | 0 | what a whole hewing costs the one who hews |

The vessel needs a `roughseas:bilge` too: its `capacityLitres` is the hollow that the build cuts out. At every stage she is a vessel: the wood not yet cut out weighs and floats as her hull does, backs her sides below the cut and her bottom against a blow, and her `roughseas:bilge` holds only what is cut, so a blank floats solid and a half-hollowed hull ships half a bilge. She cracks and breaks as a finished one, rain puts her fire out, and water in the hollow keeps it from being lit. She gets her seats only when finished. A vessel already in a world from before, with no build of its own, is finished.

Give the shape its stages as groups. `Blank` (the wood still uncut), `Char` (its charred top), `Wood` (the fire laid on it) and `Fire` (its embers once lit) each have one child per hollowing from the rim down: `Blank0`, `Blank1`…; `Clay`, the clay along the rim, is one group. The behaviour shows the ones the build is at and hides the rest, and all of them once she is finished; the item lists them in its `ignoreElements`. A spoiled blank's faces take the shape's `char` texture in place of `inside`. Our dugout's builder, `bin/lib/hulls/dugout.py`, draws them.

`roughseas:launch` lets anyone ashore push a vessel with Shift and an empty hand. A push slides her as far as one step of a person's push moves her mass against the ground's friction ([why](The-Science-of-the-Hull#building-a-dugout)), away from the one pushing.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `pushNewtons` | N | 0 | how hard one person pushes |
| `strideMetres` | m | 0 | how far a push carries |
| `friction` | coefficient | 0 | of the hull sliding on the shore |

List `roughseas:burnout` and `roughseas:launch` before `creaturecarrier`, so that a click with clay, dry grass, firewood or a torch builds her, and a push with Shift doesn't seat the player.

### Framed in place

`roughseas:framing` builds a vessel the way a bark canoe was really made: its bark comes off a standing tree in one sheet, and its frame goes into that sheet where it lies. A knife held on a standing trunk of your vessel's wood, at least `trunkBlocks` tall, for `barkSeconds`, strips the whole trunk and drops the vessel beside it at the start of her build: the entity of your vessel's code and that wood. Any vessel with this behaviour whose code ends in a wood (`barkcanoe-{wood}`) is stripped off that wood's grown logs, `{domain}:log-grown-{species}-ud`, and the trunk becomes our `roughseas:debarkedtrunk-{wood}`, a grown log of its tree with its bark gone, so that it still fells whole and holds its leaves. That block exists for the woods in `config/vessels/canoebarks.json`, every mod's included; a vessel of another wood is not stripped, and the server's debug log says so.

She is then laid stage by stage, in the order of `stages`, each piece held on her for its stage's `seconds`. A stage's `work` says what lays a piece of it:

- `lath`: a lath, wet. Five logs lying end to end with a splitting wedge on each split into laths when an axe drives a wedge; laths soak in a barrel and dry again in two days. The first lath laid makes her frame of its wood, and laths of another wood are refused.
- `pole`: a pole. Three logs lying end to end with a splitting wedge on each split into poles. Like laths, the first makes her frame of its wood.
- `rope`: vanilla's rope, one at a time. A rope held on her lashes her rather than tying itself to her (a Harmony prefix on `ItemRope.OnHeldInteractStart`), until her lashing is done.
- `pitch`: hot pitch from any liquid container, `litres` a piece. Pitch that has set is refused.
- `hew`: any axe, which loses a point of durability a piece.
- `cloth`: a piece of cloth.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `stages` | list | none | what she is laid with, in order: each a `group`, a `work`, `seconds` a piece, and the flags below |
| `group` | shape group | none | the group that draws the stage, one child per piece: `Frame1`, `Frame2`…; the count of children is the count of pieces. A look at her shows the stage by the lang key `roughseas:shipyard-stage-{group}` in lower case, with the pieces laid and the pieces in all |
| `work` | `lath`, `pole`, `rope`, `pitch`, `hew` or `cloth` | `lath` | what lays a piece |
| `seconds` | s | 0 | how long a piece is held on her: the real time of that work over sixty |
| `lines` | bool | false | its laths line her shell: the shell's `backing` backs her only where they are laid |
| `sets` | bool | false | its laths are bent in wet: she is finished only once the wettest of them has dried |
| `seals` | bool | false | her hold holds water only once the stage is done; until then water runs through her |
| `litres` | l | 0 | the pitch one piece takes |
| `replaces` | shape group | none | a group whose pieces each piece of this stage takes away once laid, `{replaces}{n}` for `{group}{n}`: a float's blunt end, for its hewn one |
| `framePart` | part name | none | the part of her `parts`, by its `name`, that her laths or poles make: its share and its `reference` stay, the material is their wood, and only what is laid weighs and takes knocks. Her shape's `frame` texture is drawn in that wood |
| `laths`, `poles`, `rope`, `pitch`, `cloth` | item codes | none | what each work takes, wildcards allowed (ours `roughseas:lath-*`, `roughseas:pole-*`, `game:rope`, `game:glueportion-pitch-*`, `game:cloth-*`) |
| `barkSeconds` | s | 0 | how long the knife takes the bark off |
| `trunkBlocks` | count | 0 | the shortest trunk a sheet comes off |

At every stage she is a vessel: she floats and can be picked up, her build goes with her onto the item and back, and the item shows her as far as she is built, and she takes her seats only once finished. Give the shape a group `Sheet`, the bark as it lies on the ground before her frame goes in, and a group `Skin`, the bark raised into her hull: the behaviour shows the one or the other. The item lists `Sheet` in its `ignoreElements`. Our bark canoe's builder, `bin/lib/hulls/barkcanoe.py`, draws them.

### Rigged in place

`roughseas:riggable` on a finished hull and `roughseas:rigging` on the vessel she becomes build one vessel onto another, the way an outrigger was really made: a finished dugout, a float log alongside, booms lashed across both. A pole held on a riggable hull, for her `seconds`, with `floatLogs` logs of one wood lying end to end along her `floatMetres` off to either side, lays her first boom: the logs are taken away and she is replaced, where she lies, by the entity of her `vessel` code and her own wood, at the start of her rigging. What a hull keeps (its cracks, the water in it) goes with her, through the vessel behaviours that implement `IHandedOver`, turned end for end when the float lay on the side opposite the rigged model's. Riders get off, and what her accessory slots held drops beside her. A hull still being built is refused.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `vessel` | entity code | none | the vessel she becomes, without its wood (ours `roughseas:outrigger`) |
| `poles` | item code | none | what lays her first boom, wildcards allowed |
| `seconds` | s | 0 | how long it is held on her |

`roughseas:rigging` is `roughseas:framing` started that way: the same `stages` and keys, its first stage begun with the boom laid, and no bark. On top of them:

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `floatLogs` | count | 0 | the logs her float is laid from |
| `floatMetres` | m | 0 | how far off her centreline they lie: the row is found through the block that far out, within half a block |
| `floatPart` | part name | none | the part of her `parts` her float makes: its share and its `reference` stay, the material is the float's wood. Her shape's `float` texture is drawn in that wood |
| `rigged` | shape groups | none | groups shown only once she is finished, such as vanilla's `SailUnfurled` and `SailHalf` |

A vessel already in a world from before, with no build of its own, is finished, its float and poles of its hull's wood. Our outrigger's builder, `bin/lib/hulls/outrigger.py`, draws her on the dugout's own hull.

## The hull

`roughseas:vesselintegrity`: the hull ([Hull condition](Hull-Condition)). Every blow lands on one of seven places and meets what really takes it ([How a hull takes a blow](Hull-Condition#how-a-hull-takes-a-blow)):

- **broad**, against the whole frame, `impactCapacity`: knocks, rams, rapids, a throw ashore, and damage from the sources `config/vessels/damage.json` lists as `broadSources` (explosions and lightning);
- **point**, against the shell where it lands: any other damage the entity receives through vanilla, a weapon, a fall, a mod's cannonball, which needs no code of ours;
- **cut**, against the shell's fibres: slashing damage, and a swing with any tool that cuts wood faster than a hand;
- **slam**, a breaker's pressure, against the planking's bending strength.

A broad load cracks its place by its share of the capacity and breaks the hull up when it reaches all of it; a point blow or a cut opens at most its place; a breaker that beats the planking stoves its place in. A `cracking` hull also breaks up once all seven places are stove in. How much damage counts as how hard a blow, and how much the material's strength varies from one blow to the next, are in `config/vessels/damage.json`, which a patch can change.

| Key | What it is |
| --- | --- |
| `model` | how the hull is made: `cracking` (the default), cracked place by place and leaking, as our wooden, bark and hide hulls; `lashed`, one lashing that blows work loose, which never floods, as our raft; `reed`, which soaks and tears, can't be mended, as our reed boat. A `lashed` or `reed` hull takes a point blow or a cut as a seventh of a place's share, the hull being one whole |
| `impactCapacity` | the energy in joules a broad load must reach to break up a hull of its reference material; a load of a third of it cracks its place a third of the way to stove in. Without it, no broad load harms the hull |
| `shell` | what the hull's outside is, for point blows, cuts and breakers; see below. Without it, the frame's capacity resists point blows and cuts too, and no breaker harms the hull |
| `minImpactSpeed` | the speed in m/s a hull takes in its stride when it runs into something, a block or another hull: a knock below it is no knock, and only the energy above it counts. Default 1 |
| `parts` | what it is built from, each `{ "material": "wood-{material}", "reference": "wood-oak", "share": 0.6 }`: the material, the one its capacity and `weight` were set for (the material itself when left out), and its share of that weight, in proportion to the other parts' (default 1); a `name` lets a build find the part whatever it is made of. Each part scales the capacity by its strength against its reference's, and its share of the weight by its density, from `config/vessels/materials.json` |
| `depthMetres` | keel to gunwale amidships. With it the vessel floats by its mass; without it, at its `freeboardMetres` |
| `waterplaneSquareMetres` | the area it cuts at the water, for a hull with a hold. Leave it out for a solid one, a raft or a bundle, which presses down through its own depth |
| `hullBox` | `[fromX, fromY, fromZ, toX, toY, toZ]` in the shape's voxels: the hull its places are measured on, when its `Hull` element reaches over more than the hull. By default, the `Hull` element |
| `seep` | `{ "litresPerHour", "greaseDays" }` on a cracking hull: what its seams let in once ungreased, and how many days afloat a full greasing lasts |
| `soak` | `{ "kgPerDay", "dryKgPerDay", "rotShare", "saturationKg" }` on a reed hull: the water it takes up a day afloat, what it dries a day in full sun, the share of what dries that stays as rot, and the most it holds |
| `runsRapids` | true if rapids don't strike it |
| `repairs` | what mends it: `material` (an item stack), `restores` (per patch: how much of one place's crack it closes, or of a lashed hull's lashing), `saturation` (satiety it costs), `greases` (true if it greases the seams instead, `restores` then being the share of `greaseDays` it gives) |
| `wreckage` | what it leaves when it breaks up, as a block's drops |
| `damage` | how the cracks show: `textures` (the shape's textures that crack) and `material` (`wood`, `bark`, `reed` or `hide`), which also picks the hull's [sounds](#sounds) |

`repairsByType` and `wreckageByType` work as vanilla's `ByType` keys do.

**The shell.** `shell` says what a blow meets where it lands, with its `kind`:

| `kind` | What it is | A point blow and a cut | A breaker |
| --- | --- | --- | --- |
| `rigid` (the default) | planks, logs, bark | hole it where they land | bends the shell and its backing between the frames |
| `carved` | a hull hollowed from one log | as `rigid` | bends each side over, from the bottom up |
| `skin` | hide over laths | spring off it unless the backing is there, then break the backing; a cut tears the skin | bends the laths |
| `bundle` | bundled stems | spring off; a cut parts only the backing, its lashings | does nothing |

Its keys: `material`, a row of `materials.json`; `metres`, its thickness; `spanMetres`, how far what a breaker bends reaches between what holds it (frames apart, a lath's length, a carved side's height); `backing`, `{ "material", "metres" }`, what lines or holds it; and `backedShare` and `bottomBackedShare`, the share of the sides and of the bottom the backing covers (1 for a lining, the laths' share of the area for a skin). A thicker shell of a tougher material holds more blows; a thicker one on closer frames holds bigger breakers. The material's `toughness` (and `cutToughness`, where an edge parts it differently) and `strength` come from its row in `config/vessels/materials.json`: add a row by patch for a material of your own. Our vessels each carry a `shell` with its sources; the coracle's is above. How the numbers combine: [The science of the hull](The-Science-of-the-Hull#hulls).

**Places.** The seven places, bow, side and quarter each side and the bottom, are taken from the hull's selection box `HullAP`, or the entity's selection box without one. A blow lands by where it comes from: a knock and an explosion by their direction, a weapon by where it hits, a breaker on the windward side, rapids and a throw ashore on the bottom. The damage marks go on the shape's elements whose centre lies in a cracked place, worked out from the model: there is nothing to configure.

**Knocks.** A knock is vanilla's own collision: when the physics stops the hull against a block, the speed it loses into that block strikes it, so a glancing blow counts only the part pointing into the wall. It takes vanilla's passive physics on the entity (`passivephysicsmultibox`, or `passivephysics`). A drifting vessel is measured on the server and a steered one on its rider's client, which reports to the server.

**Rams.** Two vessels with `vesselintegrity` ram when their hulls first touch at a closing speed above `minImpactSpeed`, and only if they lie at the same height. Both take the same broad load, the lighter vessel's weight and the closing speed deciding how big ([why](The-Science-of-the-Hull#knocks)). A ram with a steered vessel is measured on its rider's client, one between two drifting vessels on the server. A vessel without `vesselintegrity` neither rams nor is rammed; a creature or a player only pushes a hull.

A knock strikes with ½ · mass · (speed² − `minImpactSpeed`²), where the mass is everything aboard:

- the hull, its `weight` in its own material;
- each seated passenger by its entity's `weight`, a player with 10 kg of everyday gear and its armour, weighed from how much the armour slows its wearer (a worn item's `statModifiers.walkSpeed`, 60 kg for each 1 of slowdown);
- each accessory by the category it attaches as, at its empty weight (`basket` 2 kg, `crate` 12, `chest` 20); what they hold doesn't count;
- the water in its `roughseas:bilge`, by how full it is: a full bilge counts all its weight, a half-full one half.

Give `impactCapacity` by the knocks the hull should take: our coracle's 400 J and dugout's 30,000 bracket the roster, and [Hull condition](Hull-Condition#knocks-and-landings) shows what a knock does to each.

**Leaks.** A cracking hull's cracks leak as a hole does, faster the bigger the crack and the deeper it lies under the sea outside, and slower as the water inside rises to meet it ([Leaks](The-Science-of-the-Hull#leaks)). A bottom crack lies at the keel, any other a quarter of the way up the side, so a light hull's side cracks may ride above the water until she is laden. The numbers are in `config/vessels/hull.json`: `dischargeCoefficient`, the share of a hole's area that flows (0.61); `crackSquareCentimetres`, a stove-in crack's hole (25), which a blow must break the shell around; `sideCrackShare`, how far up the side a side crack lies (0.25); and `soakedStrengthShare`, the share of its dry strength wet wood bends to breaking under (0.57).

**Carried as an item.** A vessel picked up carries its whole hull on the item, in the stack attribute `roughseas:vessel`, every crack and soaking in place, and shows its condition as the item's durability. That takes our item class `roughseas:ItemVessel` with a `durability`, and the item's code the same as the entity's. Shift and right-click picks it up when the entity's attributes carry `rightClickPickup: true`; a sunk one can't be lifted. A selection box `HullAP` gives the hull a box of its own for mending, apart from the seats.

## Floating

A vessel with `depthMetres` floats by Archimedes, on both sides, twice a second. Its draft is its full mass over 1025 kg/m³ times `waterplaneSquareMetres`; a solid hull's is its depth times its mass over what its own volume of seawater weighs, no deeper than its depth. Its freeboard is what is left of `depthMetres`.

- **The full mass** is the hull, each rider's body, their gear and armour, the containers and the water aboard, by the weights in `config/vessels/mass.json` (`playerGearKg`, `armorKgPerWalkSpeed`, `crewKg` and `containerKg`), which a patch can change.
- **The hull's mass and volume** come from its `parts`. Its `weight` is the reference build's, and each part's share of it has the volume of that share in its `reference` material; in its own material that volume weighs by its density. So an ebony dugout has an oak dugout's shape and weighs as ebony.
- **Densities** are a row's `density` in `config/vessels/materials.json`, in kg/m³, with a `densitySource`. A row may carry a density alone, as `reed`, `hide` and `iron` do. A wood the table doesn't know weighs 610, the middle of the vanilla woods, takes the unknown wood's `strength` and `toughness` too, and the server log names each once.
- **Flooding.** When its freeboard reaches nothing, the sea fills its bilge. Flooded, or solid, it sinks when what the water doesn't hold up, the hull, gear, armour and containers, outweighs what its own volume of seawater weighs; riders' bodies are held up by the water. A sunk vessel throws off its riders, lies on the bottom, fills again as it is bailed, can't be picked up, and breaks up for its wreckage under an axe.
- **The line it is drawn at.** Vanilla's `swimmingOffsetY` places your shape in the water. We take that as the line it floats at with its reference build and one rider aboard, and settle it from there by the difference in draft. Set it so the shape looks right with one aboard.

To give a vessel of yours draft and buoyancy: list its `parts` with materials the table knows (or add rows by patch), set `depthMetres` from keel to gunwale, and set `waterplaneSquareMetres` so that what its bilge holds settles it by its freeboard. Ours are set that way: a full bilge with one aboard brings the gunwale to the water. Leave `freeboardMetres` off its `seaforces`.

## Sounds

A vessel with our behaviours sounds without a key of its own. Its hull speaks by its `model` and its `damage.material`, from the voices in `config/soundscape.json` under `hull.models`: a `cracking` hull as wood, bark or hide, a `lashed` one by its lashings, a `reed` one with a crunch. A material without a voice sounds as wood. A blow that leaves its place no worse to see knocks, and one that does cracks or strains, played where the blow landed and louder the harder it was; a break-up breaks from the middle of the hull. With `roughseas:seaforces`, each wave slaps, a breaker slams and water shipped into a hull with a `roughseas:bilge` splashes in, from the windward side. Its bilge sloshes and each crack trickles from where it is, as loud as it leaks, played by each client from what the bilge and the hull already sync. Afloat, it splashes once for each crest of the drawn sea that passes it, from the side the waves come from and as loud as that wave is high. Every sound, and how loud it plays for its cause, is in `config/soundscape.json`, which a patch can change.

For sounds of your own, the server raises events on the behaviours:
- `EntityBehaviorVesselIntegrity.Struck`: the place struck, the blow over what resisted it, and what it did (`Harmed` when its place came to a worse stage, `BrokenUp`, or null otherwise). `PointAt(place)` gives the place's point to play at.
- `EntityBehaviorVesselIntegrity.BrokeUp`: as the hull breaks into wreckage.
- `EntityBehaviorSeaForces.Took`: each wave she takes, with its height, whether it broke on her, the water she shipped and the direction it came from.

## Mooring

`roughseas:mooring`, on the server, with `roughseas:seaforces`; it has no keys. It holds a vessel tied to a stake, and one at anchor ([Mooring](Mooring), [Anchoring](Anchoring)).

- **A stake.** Vanilla's `ropetieable` on both sides: a rope from the vessel to a fence post.
- **An anchor.** An accessory slot (`rideableaccessories`) that takes vanilla's anchor, as vanilla's sailboat has. Spare rode is any item with a `rodeMetres` attribute in the same accessories, such as our anchor warp.

At anchor, a vessel ships less water, though a breaker slams it in full; tied to a stake, it takes the sea in full. In a storm the sea's pull on it grows with its beam and its windage, and a rope can snap. With no player near, a storm can throw it ashore.

A rider's client steers a vessel, and the server can't hold one it doesn't move: a steered vessel drags its anchor along.

## Currents

`roughseas:currentdrift`, on the server; it has no keys. An unsteered vessel in the water drifts with the [current](Currents) and a little with the wind; a sunk one stays put. It needs vanilla's passive physics on the entity (`passivephysics`, or `passivephysicsmultibox`). A rider's client moves a steered vessel, so the current doesn't reach it: a mod that sails its own vessels adds the current to its own motion, read from `CurrentAt` ([Reading the sea](Reading-the-Sea)).
