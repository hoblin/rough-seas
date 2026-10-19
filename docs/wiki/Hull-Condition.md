Every vessel has a hull, and the hull wears and breaks. How much it takes depends on what it is made of, and every source of harm, from a rock to a lightning bolt, is weighed the same way.

## Reading it

Look at a vessel to read its condition, with how much of the hull is left in percent:

| Condition | Left of the hull |
| --- | --- |
| *sound* | more than nine tenths |
| *scratched* | more than three quarters |
| *scarred* | more than half |
| *damaged, taking strain* | more than a quarter |
| *breaking up* | a quarter or less |

![A damaged hide currach, and "Hull: damaged, taking strain" in its hover text](images/mechanics/damaged-currach.webp)

The hull shows it too, each stage marked in its own material: first light scratches, then wood splitting along the grain, reed fraying, bark tearing or hide holed (on a bamboo raft only the words do). A vessel you pick up keeps its condition as durability on the item, and a damaged boat set back on the water is just as damaged.

## How a hull takes a blow

Every blow is turned into energy, in joules, and weighed against what the hull can take, its *capacity*:

- the bigger the blow against the capacity, the likelier it does harm at all, and the more it takes when it does;
- a blow as big as the capacity harms the hull about two times in three, and then takes half to one and a half times a sound hull's condition, so it breaks a sound hull about half the times it lands;
- a blow far below the capacity mostly glances off; one far above it breaks the vessel outright.

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

An ebony sailboat takes more than four times the knocks of a kapok one. The [raft](Raft), [sailboat](Sailboat), [dugout](Dugout) and [outrigger canoe](Outrigger-Canoe) are built of a chosen wood; the [reed boat](Reed-Boat), [coracle](Coracle) and [hide currach](Hide-Currach) have a single build each, and the [bark canoe](Birch-Bark-Canoe) is as strong whichever bark it is built of. Their capacities, in oak, are compared on [Vessels](Vessels).

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

| Vessel | Chance of harm at 2 m/s | at 4 m/s |
| --- | --- | --- |
| [Dugout](Dugout) | 2% | 11% |
| [Outrigger canoe](Outrigger-Canoe) | 3% | 15% |
| [Sailboat](Sailboat) | 7% | 31% |
| [Birch-bark canoe](Birch-Bark-Canoe) | 6% | 28% |
| [Hide currach](Hide-Currach) | 8% | 35% |
| [Reed boat](Reed-Boat) | 16% | 57% |
| [Raft](Raft) | 22% | 70% |
| [Coracle](Coracle) | 29% | 82% |

An oak raft paddled flat out into a rock breaks in four or five rams; a sailboat under full sail, in about two.

### Waves

Breaking waves strike the hull, harder the higher they run and harder still on a flooded hull. That is on [Surf and swamping](Surf-and-Swamping); waves that reach a vessel left [moored by the shore](Mooring#thrown-ashore) can strike it while you are away.

### Rapids

White water batters every hull but one, every second it is in it. See [Rapids](Rapids).

### Weapons, explosions and lightning

A swing at a hull counts 200 joules for each point of damage the weapon does; an iron axe breaks an oak raft in about six swings, and a bare fist harms one now and then. Explosions and [lightning](Lightning) count 1000 joules a point, so a bomb or a bolt does far more.

### Wear

Two hulls wear just by floating, counted in game hours afloat. Time on land pauses the wear, and a vessel left afloat far away wears on all the same.

- The [reed boat](Reed-Boat) soaks through: it loses a third of its condition a day and is gone in about three days.
- The [hide currach](Hide-Currach) loses a tenth a day as its hides dry out and its seams open; greasing it with rendered fat gives back what it wore, never what a knock took. Look at it to see when the next greasing is due.

The other vessels don't wear.

## Keeping it whole

A hull is [mended](Mending) one patch at a time, with the vessel's own material.

## When it breaks

A broken vessel is gone. What it was built from scatters on the water as wreckage, and like any dropped item it lasts about ten minutes:

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

Whatever it carried goes with it. Raising a sunk vessel and what it held is planned for [The deep](Roadmap#the-deep).

With [Dangerous oceans](World-Settings#dangerous-oceans) off, waves and lightning do no harm; knocks, rapids, weapons, explosions and wear still do.
