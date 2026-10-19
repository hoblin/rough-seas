A vessel becomes one of ours by data, not code: a few behaviours on its entity, configured in JSON. They take the sea for it (waves over the side, breakers on the hull, a ride on the drawn swell), hold its bilge, wear and break its hull, moor it and drift it with the current. Vanilla's raft and sailboat get theirs by patch. This page is for a mod that adds a vessel, or makes its own vessels ride our sea.

## A vessel in our sea

On the server, once a wave period, every vessel in a sea of 0.3 m or more meets a wave. The wave is drawn from the [sea state](Reading-the-Sea) there, most lower than the significant height and a few much higher ([Every wave is different](Wind-and-Waves#every-wave-is-different)). A hull rises with part of the crest. What is left above its side comes aboard, into its bilge, or washes through a hull that has none. A wave that breaks strikes the hull too, harder the higher it is and the wider the hull. The player's view of all of it is [Surf and swamping](Surf-and-Swamping).

On a client, the vessel rides the drawn sea: lifted with the surface under it, rolled and pitched with its slope. Water aboard settles it lower, rolls it harder and slows it, and a full bilge swamps it.

Waves come aboard and strike only with [Dangerous oceans](World-Settings#dangerous-oceans) on. With it off the sea still runs, the swell is still drawn, and currents still carry vessels.

## The behaviours

| Code | Sides | What it does |
| --- | --- | --- |
| `roughseas:seaforces` | client and server | the vessel's contact with the sea: waves aboard, breaker strikes, the ride on the drawn swell, water aboard slowing it. Mooring needs it |
| `roughseas:bilge` | client and server | the water aboard: from waves and rain, bailed out, swamping the hull when full |
| `roughseas:vesselintegrity` | client and server, first in both lists | the hull's condition: what breaks it, wear, mending, wreckage |
| `roughseas:mooring` | server | a stake rope and an anchor that hold in a storm, or don't |
| `roughseas:currentdrift` | server | the drift of an unsteered vessel with the current and the wind |

Configure each one in your entity's `behaviorConfigs` under its code, as vanilla's behaviours are, and list it in `client.behaviors` and `server.behaviors` as the table says. `vesselintegrity` goes first in both lists, before `creaturecarrier` and `rideableaccessories`, so that a right-click with mending material mends the hull rather than seating the player. `seaforces` is what the others act through: `mooring` holds against what it measures, and a bilge's water slows, settles and swamps a vessel only through it. Take it first; `vesselintegrity` and `currentdrift` also work alone.

Every key may carry a `sources` entry beside it, text about where each number came from. Our code doesn't read it; we keep a real-world source for every number there, and so can you.

Our [coracle](Coracle), its sources left out, carries all five:

```json
"behaviorConfigs": {
    "roughseas:vesselintegrity": {
        "impactCapacity": 400,
        "repairs": [{ "material": { "type": "item", "code": "game:hide-prepared-small", "stacksize": 1 }, "restores": 0.67, "saturation": 40 }],
        "wreckage": [{ "type": "item", "code": "game:stick", "quantity": { "avg": 3, "var": 2 } }],
        "damage": { "textures": ["hide"], "material": "hide" }
    },
    "roughseas:seaforces": {
        "beamMetres": 1.4, "mastMetres": 0, "windageSquareMetres": 0.5,
        "freeboardMetres": 0.2, "heave": 0.8, "capsizeBreakerMetres": 0.6,
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

Every vessel of ours is a whole example, each number with its source: `assets/roughseas/entities/vessel/` in the mod. A vessel that must also work without Rough Seas gets the same behaviours by patch, with `"dependsOn": [{ "modid": "roughseas" }]` on each operation, as `patches/storms-vessels.json` and `patches/vessels-vanilla-boats.json` give them to vanilla's raft and sailboat.

## Taking the sea

`roughseas:seaforces`. Leave a key out and it takes the default, which for most of them is zero: a hull with no freeboard and no heave ships every crest. Set them all.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `beamMetres` | m | 0 | the hull's width: how much of a crest it takes aboard, how hard a breaker strikes it, how hard the sea pulls on its mooring |
| `windageSquareMetres` | m² | 0 | what the wind catches, hull and rig: how hard the wind pulls on its mooring |
| `mastMetres` | m | 0 | a mast's height above the hull; [lightning](Lightning) seeks it |
| `freeboardMetres` | m | 0 | the side above the water, empty |
| `heave` | 0–1 | 0 | the share of a crest's height the hull rises with; what is left climbs its side |
| `rollResponse` | × | 1 | how many times the water's slope across it it rolls on the drawn swell |
| `pitchResponse` | × | 1 | the same along it |
| `capsizeBreakerMetres` | m | none | the breaker that overturns it, empty |

A wave lifts a hull a crest of about half its height, seven tenths when it breaks. Water aboard takes off freeboard in proportion, up to all of it when the bilge is full, and adds to the roll, twice as much when full.

**Capsizing is switched off** in 2.0 ([Capsizing](Surf-and-Swamping#capsizing)). Give `capsizeBreakerMetres` anyway: it comes back in [Seamanship](Roadmap#seamanship).

**Riding the drawn swell.** On a client, the vessel is posed on the surface under it, sampled at the ends and sides of its collision box. This rides on vanilla's boat: the entity must be an `EntityBoat` or derive from it, and a class of yours that overrides `OnRenderFrame` calls the base, or it isn't posed. Where no swell is drawn it rolls and pitches as vanilla rocks it, times `rollResponse` and `pitchResponse`.

**Water aboard slows it.** An `EntityBoat` with a bilge loses speed as water adds to its weight, from its entity attribute `speedMultiplier` down to none when swamped. Its riders are moved into the water beside a swamped hull, holding on.

## The bilge

`roughseas:bilge`. A vessel without it holds no water: the sea washes through, as through our raft and reed boat.

| Key | Unit | Default | What it is |
| --- | --- | --- | --- |
| `capacityLitres` | L | 0 | what it holds before it swamps |
| `deckSquareMetres` | m² | 0 | the open area rain falls into |
| `drainLitresPerHour` | L per game hour | 0 | what drains out by itself |
| `sinks` | true or false | false | full, it goes to the bottom, as our ballasted sailboat does, instead of lying awash |

[Bailing](Bailing) needs nothing from you: a bowl, a jug or a bucket bails any vessel with a bilge.

To show the water, give the shape a group `Bilge` with one child per level from the bottom up (`Bilge1`, `Bilge2`…), a texture `bilge`, and list `Bilge` in the shape's `ignoreElements`. Our dugout is the example. Without the group the bilge works and the water is unseen.

## Hull condition

`roughseas:vesselintegrity`: the hull's condition, from sound to broken ([Hull condition](Hull-Condition)). Every blow is weighed against the hull's capacity, by chance, harder blows harming more often and more. Breakers, rapids, and any damage the entity receives through vanilla (weapons, explosions, lightning, falling) all strike it. A mod's cannonball needs no code of ours: the damage it deals becomes a blow.

| Key | What it is |
| --- | --- |
| `impactCapacity` | the energy in joules one blow must reach to be likely to harm a hull of its reference material. Required |
| `minImpactSpeed` | the speed in m/s a hull takes in its stride when it runs into something: a knock below it is no knock, and only the energy above it counts. Default 1 |
| `parts` | what it is built from: `{ "material": "wood-{material}", "reference": "wood-oak" }` scales the capacity by the material's strength against the reference's, from `config/vessels/materials.json` |
| `wearPerDay` | integrity lost per game day afloat, 0 for none |
| `runsRapids` | true if rapids don't strike it |
| `repairs` | what mends it: `material` (an item stack), `restores` (integrity per patch), `saturation` (satiety it costs), `greases` (true if it gives back only what wear took) |
| `wreckage` | what it leaves when it breaks, as a block's drops |
| `damage` | how the damage shows: `textures` (the shape's textures that crack) and `material` (`wood`, `bark`, `reed` or `hide`) |

`repairsByType` and `wreckageByType` work as vanilla's `ByType` keys do.

**Knocks.** A knock is vanilla's own collision: when the physics stops the hull against a block, the speed it loses into that block strikes it, so a glancing blow counts only the part pointing into the wall. It takes vanilla's passive physics on the entity (`passivephysicsmultibox`, or `passivephysics`). A drifting vessel is measured on the server and a steered one on its rider's client, which reports to the server. Entities don't knock a hull: two vessels, or a vessel and a creature, only push each other apart.

A knock strikes with ½ · mass · (speed² − `minImpactSpeed`²), where the mass is everything aboard:

- the entity's own `weight`;
- each seated passenger by its entity's `weight`, a player with 10 kg of everyday gear and its armour, weighed from how much the armour slows its wearer (a worn item's `statModifiers.walkSpeed`, 60 kg for each 1 of slowdown);
- each accessory by the category it attaches as, at its empty weight (`basket` 2 kg, `crate` 12, `chest` 20); what they hold doesn't count;
- the water in its `roughseas:bilge`, by how full it is: a full bilge counts all its weight, a half-full one half.

The gear, armour and container weights are in `config/vessels/impact.json`, which a patch can change. Give `impactCapacity` by the knocks the hull should take: our coracle's 400 J and dugout's 30,000 bracket the roster, and [Hull condition](Hull-Condition#knocks-and-landings) turns a capacity into the chance a knock does harm.

**Carried as an item.** A vessel picked up keeps its condition as the item's durability, as a tool keeps its wear. That takes our item class `roughseas:ItemVessel` with a `durability`, and the item's code the same as the entity's. Shift and right-click picks it up when the entity's attributes carry `rightClickPickup: true`. A selection box `HullAP` gives the hull a box of its own for mending, apart from the seats.

## Mooring

`roughseas:mooring`, on the server, with `roughseas:seaforces`; it has no keys. It holds a vessel tied to a stake, and one at anchor ([Mooring](Mooring), [Anchoring](Anchoring)).

- **A stake.** Vanilla's `ropetieable` on both sides: a rope from the vessel to a fence post.
- **An anchor.** An accessory slot (`rideableaccessories`) that takes vanilla's anchor, as vanilla's sailboat has. Spare rode is any item with a `rodeMetres` attribute in the same accessories, such as our anchor warp.

At anchor, a vessel ships less water and takes milder blows; tied to a stake, it takes the sea in full. In a storm the sea's pull on it grows with its beam and its windage, and a rope can snap. With no player near, a storm can throw it ashore.

A rider's client steers a vessel, and the server can't hold one it doesn't move: a steered vessel drags its anchor along.

## Currents

`roughseas:currentdrift`, on the server; it has no keys. An unsteered vessel in the water drifts with the [current](Currents) and a little with the wind. It needs vanilla's passive physics on the entity (`passivephysics`, or `passivephysicsmultibox`). A rider's client moves a steered vessel, so the current doesn't reach it: a mod that sails its own vessels adds the current to its own motion, read from `CurrentAt` ([Reading the sea](Reading-the-Sea)).
