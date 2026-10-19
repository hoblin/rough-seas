A vessel becomes one of ours by data, not code: a few behaviours on its entity, configured in JSON. They float it by its mass, take the sea for it (waves over the side, breakers on the hull, a ride on the drawn swell), hold its bilge, crack, flood and sink its hull, moor it and drift it with the current. Vanilla's raft and sailboat get theirs by patch. This page is for a mod that adds a vessel, or makes its own vessels ride our sea.

## A vessel in our sea

A vessel meets [both of our seas](Reading-the-Sea#two-seas): the sea state on the server, which decides what happens to her, and the drawn sea on a client, which shows her riding it.

On the server, once a wave period, every vessel in a sea of 0.3 m or more meets a wave. The wave is drawn from the sea state there, most lower than the significant height and a few much higher ([Every wave is different](Wind-and-Waves#every-wave-is-different)). A hull rises with part of the crest. What is left above its side comes aboard, into its bilge, or washes through a hull that has none. A wave that breaks strikes the hull too, on its windward side, harder the higher it is and the wider the hull. The player's view of all of it is [Surf and swamping](Surf-and-Swamping).

On both sides, a vessel with a hull of ours floats by its full mass: hull, riders, their gear and armour, containers and the water aboard settle it lower, and it floods when its side goes under. On a client, it rides the drawn sea: lifted with the surface under it, rolled and pitched with its slope, on periods of its own mass. Water aboard rolls it harder and slows it, and a full bilge swamps it.

Waves come aboard and strike only with [Dangerous oceans](World-Settings#dangerous-oceans) on. With it off the sea still runs, the swell is still drawn, currents still carry vessels, and cracks still leak.

## The behaviours

| Code | Sides | What it does |
| --- | --- | --- |
| `roughseas:seaforces` | client and server | the vessel's contact with the sea: waves aboard, breaker strikes, the ride on the drawn swell, its mass slowing it, the settling it draws. Mooring needs it |
| `roughseas:bilge` | client and server | the water aboard: from waves, leaks and rain, bailed out, swamping the hull when full |
| `roughseas:vesselintegrity` | client and server, first in both lists | the hull: how it floats and sinks, what cracks or breaks it, its leaks, mending, wreckage |
| `roughseas:mooring` | server | a stake rope and an anchor that hold in a storm, or don't |
| `roughseas:currentdrift` | server | the drift of an unsteered vessel with the current and the wind |

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
| `beamMetres` | m | 0 | the hull's width: how much of a crest it takes aboard, how hard a breaker strikes it, how hard the sea pulls on its mooring, how it rolls |
| `windageSquareMetres` | m² | 0 | what the wind catches, hull and rig: how hard the wind pulls on its mooring |
| `mastMetres` | m | 0 | a mast's height above the hull; [lightning](Lightning) seeks it |
| `freeboardMetres` | m | 0 | the side above the water, empty, for a vessel that doesn't [float by its mass](#floating); ours leave it out |
| `heave` | 0–1 | 0 | the share of a crest's height the hull rises with; what is left climbs its side |
| `rollResponse` | × | 1 | how many times the water's slope across it it rolls on the drawn swell |
| `pitchResponse` | × | 1 | the same along it |
| `capsizeBreakerMetres` | m | none | the breaker that overturns it, empty |

A crest stands half a wave's height above still water, seven tenths when it breaks; the hull rises with `heave` of that. Water aboard adds to the roll, twice as much when full. A vessel that floats by its mass ships water over the freeboard it floats at; one with a fixed `freeboardMetres` loses it in proportion to its bilge, all of it when full.

**Capsizing is switched off** for now ([Capsizing](Surf-and-Swamping#capsizing)). Give `capsizeBreakerMetres` anyway: it comes back in [Seamanship](Roadmap#seamanship).

**Riding the drawn swell.** On a client, the vessel is posed on the surface under it, sampled at the ends and sides of a square as wide as its collision box's longer side. A vessel that floats by its mass follows that pose on its own heave and roll periods, from its draft, depth and beam ([How a hull answers](Reading-the-Sea#how-a-hull-answers)); one that doesn't takes the water's pose as it lies. This rides on vanilla's boat: the entity must be an `EntityBoat` or derive from it, and a class of yours that overrides `OnRenderFrame` calls the base, or it isn't posed. A vessel that can't, poses itself on the same surface through [the drawn sea](Reading-the-Sea#the-drawn-sea). Where no swell is drawn it rolls and pitches as vanilla rocks it, times `rollResponse` and `pitchResponse`.

**Its mass slows it.** An `EntityBoat` makes its entity attribute `speedMultiplier` at its reference mass: its own `weight` and one 75 kg rider. Heavier, it is slower, and lighter a little faster ([why](The-Science-Behind-It#floating)): the hull in its own material, riders, gear, armour, containers and the water aboard all count. A swamped one makes no speed, and its riders are moved into the water beside it, holding on.

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

## The hull

`roughseas:vesselintegrity`: the hull ([Hull condition](Hull-Condition)). Every blow is weighed against the hull's capacity, by chance, harder blows harming more often and more, and lands on one of seven places. Breakers, rapids, and any damage the entity receives through vanilla (weapons, explosions, lightning, falling) all strike it. A mod's cannonball needs no code of ours: the damage it deals becomes a blow, where it hits.

| Key | What it is |
| --- | --- |
| `model` | how the hull is made: `cracking` (the default), cracked place by place and leaking, as our wooden, bark and hide hulls; `lashed`, one lashing that knocks work loose, which never floods, as our raft; `reed`, which knocks don't harm and which soaks, can't be mended, as our reed boat |
| `impactCapacity` | the energy in joules one blow must reach to be likely to harm a hull of its reference material. Required for a `cracking` or `lashed` hull; on a `reed` hull, which knocks don't harm, it sets how hard a knock sounds |
| `minImpactSpeed` | the speed in m/s a hull takes in its stride when it runs into something: a knock below it is no knock, and only the energy above it counts. Default 1 |
| `parts` | what it is built from, each `{ "material": "wood-{material}", "reference": "wood-oak", "share": 0.6 }`: the material, the one its capacity and `weight` were set for (the material itself when left out), and its share of that weight, in proportion to the other parts' (default 1). Each part scales the capacity by its strength against its reference's, and its share of the weight by its density, from `config/vessels/materials.json` |
| `depthMetres` | keel to gunwale amidships. With it the vessel floats by its mass; without it, at its `freeboardMetres` |
| `waterplaneSquareMetres` | the area it cuts at the water, for a hull with a hold. Leave it out for a solid one, a raft or a bundle, which presses down through its own depth |
| `hullBox` | `[fromX, fromY, fromZ, toX, toY, toZ]` in the shape's voxels: the hull its places are measured on, when its `Hull` element reaches over more than the hull (our outrigger's covers its float). By default, the `Hull` element |
| `seep` | `{ "litresPerHour", "greaseDays" }` on a cracking hull: what its seams let in once ungreased, and how many days afloat a full greasing lasts |
| `soak` | `{ "kgPerDay", "dryKgPerDay", "rotShare", "saturationKg" }` on a reed hull: the water it takes up a day afloat, what it dries a day in full sun, the share of what dries that stays as rot, and the most it holds |
| `runsRapids` | true if rapids don't strike it |
| `repairs` | what mends it: `material` (an item stack), `restores` (per patch: how much of one place's crack it closes, or of a lashed hull's lashing), `saturation` (satiety it costs), `greases` (true if it greases the seams instead, `restores` then being the share of `greaseDays` it gives) |
| `wreckage` | what it leaves when it breaks up, as a block's drops |
| `damage` | how the cracks show: `textures` (the shape's textures that crack) and `material` (`wood`, `bark`, `reed` or `hide`), which also picks the hull's [sounds](#sounds) |

`repairsByType` and `wreckageByType` work as vanilla's `ByType` keys do.

**Places.** The seven places, bow, side and quarter each side and the bottom, are taken from the hull's selection box `HullAP`, or the entity's selection box without one. A blow lands by where it comes from: a knock and an explosion by their direction, a weapon by where it hits, a breaker on the windward side, rapids and a throw ashore on the bottom. The damage marks go on the shape's elements whose centre lies in a cracked place, worked out from the model: there is nothing to configure.

**Knocks.** A knock is vanilla's own collision: when the physics stops the hull against a block, the speed it loses into that block strikes it, so a glancing blow counts only the part pointing into the wall. It takes vanilla's passive physics on the entity (`passivephysicsmultibox`, or `passivephysics`). A drifting vessel is measured on the server and a steered one on its rider's client, which reports to the server. Entities don't knock a hull: two vessels, or a vessel and a creature, only push each other apart.

A knock strikes with ½ · mass · (speed² − `minImpactSpeed`²), where the mass is everything aboard:

- the hull, its `weight` in its own material;
- each seated passenger by its entity's `weight`, a player with 10 kg of everyday gear and its armour, weighed from how much the armour slows its wearer (a worn item's `statModifiers.walkSpeed`, 60 kg for each 1 of slowdown);
- each accessory by the category it attaches as, at its empty weight (`basket` 2 kg, `crate` 12, `chest` 20); what they hold doesn't count;
- the water in its `roughseas:bilge`, by how full it is: a full bilge counts all its weight, a half-full one half.

Give `impactCapacity` by the knocks the hull should take: our coracle's 400 J and dugout's 30,000 bracket the roster, and [Hull condition](Hull-Condition#knocks-and-landings) turns a capacity into the chance a knock does harm. A blow whose harm alone would stove in its place breaks the hull up; cracks that add up to it only leak.

**Leaks.** A cracking hull's cracks leak as a hole does, faster the bigger the crack and the deeper it lies under the sea outside, and slower as the water inside rises to meet it ([Leaks](The-Science-Behind-It#leaks)). A bottom crack lies at the keel, any other a quarter of the way up the side, so a light hull's side cracks may ride above the water until she is laden. The numbers are in `config/vessels/hull.json`: `dischargeCoefficient`, the share of a hole's area that flows (0.61); `crackSquareCentimetres`, a stove-in crack's hole (25); and `sideCrackShare`, how far up the side a side crack lies (0.25).

**Carried as an item.** A vessel picked up carries its whole hull on the item, in the stack attribute `roughseas:vessel`, every crack and soaking in place, and shows its condition as the item's durability. That takes our item class `roughseas:ItemVessel` with a `durability`, and the item's code the same as the entity's. Shift and right-click picks it up when the entity's attributes carry `rightClickPickup: true`; a sunk one can't be lifted. A selection box `HullAP` gives the hull a box of its own for mending, apart from the seats.

## Floating

A vessel with `depthMetres` floats by Archimedes, on both sides, twice a second. Its draft is its full mass over 1025 kg/m³ times `waterplaneSquareMetres`; a solid hull's is its depth times its mass over what its own volume of seawater weighs, no deeper than its depth. Its freeboard is what is left of `depthMetres`.

- **The full mass** is the hull, each rider's body, their gear and armour, the containers and the water aboard, by the weights in `config/vessels/mass.json` (`playerGearKg`, `armorKgPerWalkSpeed`, `crewKg` and `containerKg`), which a patch can change.
- **The hull's mass and volume** come from its `parts`. Its `weight` is the reference build's, and each part's share of it has the volume of that share in its `reference` material; in its own material that volume weighs by its density. So an ebony dugout has an oak dugout's shape and weighs as ebony.
- **Densities** are a row's `density` in `config/vessels/materials.json`, in kg/m³, with a `densitySource`. A row may carry a density alone, as `reed`, `hide`, `bark` and `iron` do. A wood the table doesn't know weighs 610, the middle of the vanilla woods, and the server log names it once.
- **Flooding.** When its freeboard reaches nothing, the sea fills its bilge. Flooded, or solid, it sinks when what the water doesn't hold up, the hull, gear, armour and containers, outweighs what its own volume of seawater weighs; riders' bodies are held up by the water. A sunk vessel throws off its riders, lies on the bottom, fills again as it is bailed, can't be picked up, and breaks up for its wreckage under an axe.
- **The line it is drawn at.** Vanilla's `swimmingOffsetY` places your shape in the water. We take that as the line it floats at with its reference build and one rider aboard, and settle it from there by the difference in draft. Set it so the shape looks right with one aboard.

To give a vessel of yours draft and buoyancy: list its `parts` with materials the table knows (or add rows by patch), set `depthMetres` from keel to gunwale, and set `waterplaneSquareMetres` so that what its bilge holds settles it by its freeboard. Ours are set that way: a full bilge with one aboard brings the gunwale to the water. Leave `freeboardMetres` off its `seaforces`.

## Sounds

A vessel with our behaviours sounds without a key of its own. Its hull speaks by its `model` and its `damage.material`, from the voices in `config/soundscape.json` under `hull.models`: a `cracking` hull as wood, bark or hide, a `lashed` one by its lashings, a `reed` one with a crunch. A material without a voice sounds as wood. A blow it shrugs off knocks and a blow that harms it cracks or strains, played where the blow landed and louder the harder it was; a break-up breaks from the middle of the hull. With `roughseas:seaforces`, each wave slaps, a breaker slams and water shipped into a hull with a `roughseas:bilge` splashes in, from the windward side. Its bilge sloshes, each crack trickles from where it is, as loud as it leaks, and the sea laps at it, played by each client from what the bilge and the hull already sync. Every sound, and how loud it plays for its cause, is in `config/soundscape.json`, which a patch can change.

For sounds of your own, the server raises events on the behaviours:
- `EntityBehaviorVesselIntegrity.Struck`: the place struck, the blow over the hull's capacity, and what it did (`Harmed`, `BrokenUp`, or null when shrugged off). `PointAt(place)` gives the place's point to play at.
- `EntityBehaviorVesselIntegrity.BrokeUp`: as the hull breaks into wreckage.
- `EntityBehaviorSeaForces.Took`: each wave she takes, with its height, whether it broke on her, the water she shipped and the direction it came from.

## Mooring

`roughseas:mooring`, on the server, with `roughseas:seaforces`; it has no keys. It holds a vessel tied to a stake, and one at anchor ([Mooring](Mooring), [Anchoring](Anchoring)).

- **A stake.** Vanilla's `ropetieable` on both sides: a rope from the vessel to a fence post.
- **An anchor.** An accessory slot (`rideableaccessories`) that takes vanilla's anchor, as vanilla's sailboat has. Spare rode is any item with a `rodeMetres` attribute in the same accessories, such as our anchor warp.

At anchor, a vessel ships less water and takes milder blows; tied to a stake, it takes the sea in full. In a storm the sea's pull on it grows with its beam and its windage, and a rope can snap. With no player near, a storm can throw it ashore.

A rider's client steers a vessel, and the server can't hold one it doesn't move: a steered vessel drags its anchor along.

## Currents

`roughseas:currentdrift`, on the server; it has no keys. An unsteered vessel in the water drifts with the [current](Currents) and a little with the wind; a sunk one stays put. It needs vanilla's passive physics on the entity (`passivephysics`, or `passivephysicsmultibox`). A rider's client moves a steered vessel, so the current doesn't reach it: a mod that sails its own vessels adds the current to its own motion, read from `CurrentAt` ([Reading the sea](Reading-the-Sea)).
