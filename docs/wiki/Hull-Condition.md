Every vessel has a hull, and a knock cracks it where it lands. A crack leaks, the leaks fill the bilge, and a hull is lost by flooding far more often than by breaking: only a really severe blow breaks one up at once. How much a hull takes depends on what it is made of, and every source of harm, from a rock to a lightning bolt, is weighed the same way.

## Reading it

Look at a vessel to read its hull. A sound one says *Hull: sound*; a cracked one lists each cracked place and how bad it is:

| State | Crack | Hole it leaks through |
| --- | --- | --- |
| *scratched* | a tenth to a quarter | up to 6 cm² |
| *cracked* | a quarter to a half | up to 12 cm² |
| *split* | a half to three quarters | up to 19 cm² |
| *stove in* | three quarters or more | up to 25 cm², a hole 5.6 cm across |

![A hide currach stove in on her port side and cracked on her bottom, leaking 30 litres a minute](images/mechanics/damaged-currach.webp)

Below it: how many litres a minute she leaks, and her [freeboard](Surf-and-Swamping#how-she-floats) in centimetres, the side she shows above the water; *Awash* when the sea comes over it, *Sunk* when she lies on the bottom.

The hull shows each crack where it is, marked in its own material: first light scratches, then wood splitting along the grain, bark tearing or hide holed. A vessel you pick up carries its hull with it, every crack in place, and its durability on the item shows how much is left.

The [raft](Raft) and the [reed boat](Reed-Boat) are not cracked: the raft reads *Lashings* in percent, the reed boat *Reed waterlogged*, and their marks spread over the whole of them.

## Where a blow lands

A hull has seven places: the port and starboard bow, the port and starboard side, the port and starboard quarter, and the bottom. The bow and the quarter are the outer 30% of her length at each end.

- **A knock** lands where she hit: within 45° of her length, on the bow or the quarter of that side; otherwise on the side.
- **Rapids**, and a storm that throws her ashore, strike the bottom.
- **A breaking wave** strikes the side the wind comes from.
- **A weapon** strikes where it hits; **an explosion**, the side facing it.

Each harming blow cracks its own place further, and a second knock elsewhere cracks elsewhere.

## How a hull takes a blow

Every blow is turned into energy, in joules, and weighed against what the hull can take, its *capacity*:

- the bigger the blow against the capacity, the likelier it does harm at all, and the deeper the crack it leaves;
- a blow as big as the capacity harms the hull about two times in three, and then cracks its place by half to one and a half times a whole stove-in crack: about one time in three it breaks the hull up outright;
- a blow far below the capacity mostly glances off, or scratches; one far above it breaks the vessel up.

A hull breaks up only when one blow alone would stove in its place. Cracks that add up to stove in, however many, don't break it: they let in the sea.

So a small knock on a big hull is nothing, while the same knock may hole a light one, and no hull is ever quite safe or quite doomed: there is always some luck in it.

## What it is made of

A hull's capacity comes from its build, and for a wooden hull also from its wood. Each wood counts by its *modulus of rupture*, the bending strength measured for that timber in the [Wood Database](https://www.wood-database.com/), against oak:

| Wood | Strength, MPa | Against oak |
| --- | --- | --- |
| ebony | 158 | 1.63 × |
| purpleheart | 152 | 1.56 × |
| bamboo (raft) | 130 | 1.34 × |
| birch | 114 | 1.18 × |
| maple | 109 | 1.12 × |
| walnut | 101 | 1.04 × |
| oak | 97 | 1 |
| larch | 90 | 0.93 × |
| acacia | 87 | 0.90 × |
| pine | 83 | 0.86 × |
| bald cypress | 73 | 0.75 × |
| redwood | 62 | 0.64 × |
| aged wood (raft) | 49 | 0.5 × |
| kapok | 36 | 0.37 × |

A wood from another mod counts by its species when the table knows it: Wildcraft's elm by elm's 65 MPa, its mahogany by mahogany's 81 (the full list is in [Beside other mods](Other-Mods#wildcraft-trees-and-shrubs)). A species the table doesn't know counts as 93.5 MPa, the middle of the vanilla woods, and the server log names it once.

An ebony sailboat takes more than four times the knocks of a kapok one. The [raft](Raft), [sailboat](Sailboat), [dugout](Dugout) and [outrigger canoe](Outrigger-Canoe) are built of a chosen wood; the [coracle](Coracle) and [hide currach](Hide-Currach) have a single build each, and the [bark canoe](Birch-Bark-Canoe) is as strong whichever bark it is built of. Their capacities, in oak, are compared on [Vessels](Vessels).

The wood weighs too: a hull of a light wood floats higher and paddles a little faster than oak, one of a heavy wood lower and slower, and one heavier than seawater goes down when she floods ([Surf and swamping](Surf-and-Swamping#how-she-floats)).

## What harms a hull

### Knocks and landings

Running into rock or the shore strikes the hull with the energy of the speed it loses into the obstacle: a head-on ram loses all of it, while a hull glancing along a shore loses only the part that pointed into it. The first 1 m/s the hull takes in its stride, so easing in to a landing is always safe, and only the energy above it counts.

The energy grows with everything aboard:

- the vessel itself;
- each rider, with about 10 kg of everyday gear, and armour by how much it slows its wearer: full plate adds about 25 kg;
- each basket, crate or chest at its own empty weight; what is in them doesn't count;
- water in the bilge, by how full it is. A half-full bilge sloshes and brings only half its weight to bear, while a swamped one hits with all of it. Bail before you land.

Vessels nudging each other do the hull no harm.

What one rider's knock does, losing 2 or 4 m/s at once:

| Vessel | Chance of harm at 2 m/s | at 4 m/s | A harming knock at 4 m/s |
| --- | --- | --- | --- |
| [Dugout](Dugout) | 2% | 11% | scratches |
| [Outrigger canoe](Outrigger-Canoe) | 3% | 15% | scratches |
| [Sailboat](Sailboat) | 7% | 31% | cracks |
| [Birch-bark canoe](Birch-Bark-Canoe) | 6% | 28% | cracks |
| [Hide currach](Hide-Currach) | 8% | 35% | cracks |
| [Raft](Raft) | 22% | 70% | mostly shakes it apart |
| [Coracle](Coracle) | 29% | 82% | mostly breaks it up |

An oak raft paddled flat out into a rock comes apart in four or five rams. A sailboat under full sail breaks up on about every other ram, and a harming ram she survives leaves her bow split or stove in.

The [reed boat](Reed-Boat) shrugs off every knock: reed bundles give and spring back.

### Waves

Breaking waves strike the windward side, harder the higher they run and harder still on a flooded hull. That is on [Surf and swamping](Surf-and-Swamping#breakers); waves that reach a vessel left [moored by the shore](Mooring#thrown-ashore) can throw it on its bottom while you are away.

### Rapids

White water batters the bottom of every hull but one, every second it is in it. See [Rapids](Rapids).

### Weapons, explosions and lightning

A swing at a hull counts 200 joules for each point of damage the weapon does; an iron axe takes an oak raft apart in about six swings, and a bare fist harms one now and then. Explosions and [lightning](Lightning) count 1000 joules a point, so a bomb or a bolt does far more.

## Leaks

A crack lets the sea in faster the deeper it lies under the waterline. A crack in the bottom always leaks. One in the bow, a side or the quarter sits a quarter of the way up the side: on a light boat riding high it barely leaks, or not at all, while a loaded boat sits low enough for it to pour.

What one stove-in place lets in, with one aboard and a dry bilge, in a hull of oak where it has a wood:

| Vessel | Bilge | Stove-in bottom | Stove-in side | The bottom fills her in |
| --- | --- | --- | --- | --- |
| [Coracle](Coracle) | 250 litres | 107 litres a minute | 21 | under two minutes |
| [Dugout](Dugout) | 450 litres | 184 | 130 | two minutes |
| [Outrigger canoe](Outrigger-Canoe) | 600 litres | 186 | 98 | two and a half minutes |
| [Birch-bark canoe](Birch-Bark-Canoe) | 600 litres | 84 | none | four minutes |
| [Sailboat](Sailboat) | 1500 litres | 325 | 242 | four minutes |
| [Hide currach](Hide-Currach) | 1500 litres | 86 | none | eight minutes |

Each leak quickens as the water aboard settles her, and a bottom only cracked by a hard knock lets in about a third as much. A bucket bails about 200 litres a minute ([Bailing](Bailing)): it keeps up with one bad leak in most boats, but enough cracks outpace any bailing, and then without [mending](Mending) she is done.

## Seeping and soaking

A wooden hull barely wears: the dugout, the outrigger, the bark canoe and the sailboat need no upkeep. Two hulls change just by floating, counted in game hours afloat, wherever they are:

- The [hide currach](Hide-Currach)'s seams seep 30 litres a game hour into her bilge once the grease is gone. Rendered fat keeps them tight for two and a half days afloat; look at her to see when the next greasing is due.
- The [reed boat](Reed-Boat) soaks up 80 kg of water a day afloat, sitting deeper as it does, and lies on the bottom after about three days. Ashore under the open sky, in daylight and no rain, it dries out, but each drying leaves a third of what it drove out behind as rot that never dries. Soaked through and dried about five times, it sinks the moment it is launched.

A [raft](Raft) has no hull to fill: a knock works its lashings loose instead, and once they are all loose it comes apart.

## Keeping it whole

A hull is [mended](Mending) one patch at a time, with the vessel's own material, on the place you look at.

## When she is lost

A flooded hull lies awash or goes to the bottom, by what she is built of and what she carries ([Swamping](Surf-and-Swamping#swamping)). A sunk vessel lies on the bottom whole, her cargo still aboard: dive down to it, or break her up there with an axe for her wreckage. Raising her is planned for [The deep](Roadmap#the-deep).

A vessel broken up, by one severe blow, by its lashings coming loose or by an axe on the bottom, is gone. What it was built from scatters on the water as wreckage, and like any dropped item it lasts about ten minutes:

| Vessel | Wreckage |
| --- | --- |
| [Raft](Raft) | a few logs (or bamboo stakes) and maybe a rope |
| [Sailboat](Sailboat) | about 40 planks, a few support beams, ropes and linen |
| [Reed boat](Reed-Boat) | a handful of reed tops |
| [Dugout](Dugout) | firewood |
| [Coracle](Coracle) | a few sticks |
| [Birch-bark canoe](Birch-Bark-Canoe) | some bark and a stick |
| [Hide currach](Hide-Currach) | sticks and a couple of leathers |
| [Outrigger canoe](Outrigger-Canoe) | a few planks, a rope and some linen |

Whatever it carried goes with it.

With [Dangerous oceans](World-Settings#dangerous-oceans) off, waves and lightning do no harm; knocks, rapids, weapons and explosions still crack a hull, reed still soaks and hide still seeps.
