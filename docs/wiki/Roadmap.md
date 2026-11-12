Where Rough Seas is heading. These are plans, not promises: the next release is planned in detail, the ones after it in outline, and the ideas further out are just that. Each release is named for what it adds to life at sea.

## 1.0 "Rough Seas"

The first release: the sea stops being flat, safe and empty.

- [Wind and waves](Wind-and-Waves) from the real Beaufort scale, and [storms](Storms) that cross the world: squalls, gales and hurricanes.
- [A sea you can see](The-Look-of-the-Sea): a swell as high as the waves, whitecaps by the Beaufort scale, surf on the shore, boats that ride it, and rain and clouds that follow the weather.
- [Lightning](Lightning) in warm storms, drawn to masts.
- [Currents](Currents) that turn the oceans and carry off a boat left alone.
- [Hulls](Hull-Condition) that wear and break by what they are made of, and are [mended](Mending) with it.
- [Surf, swamping](Surf-and-Swamping) and [bailing](Bailing); white-water [rapids](Rapids).
- [Anchors](Anchoring) that hold and [moorings](Mooring) that snap.
- Six new [vessels](Vessels) between the raft and the sailboat.
- The [Rough Seas playstyle and the Dangerous oceans setting](World-Settings).

## 2.0 "Undercurrent"

Rough Seas moves beneath vanilla, so other mods build on its sea as if it were the game's own.

- **Under vanilla's weather.** The mod's weather drives the game's own: its wind patterns, its rain and its weather events, which fire by their own conditions against the mod's rain and the live temperature. A mod written for plain Vintage Story reads the storm as vanilla weather; [For modders](For-Modders) says how.
- **One sea, computed on the server.** The server works out the wind, storms, currents and waves and sends each player the map around them, so a client and its server never disagree.
- **The world's own ocean.** The sea is read from the world's own map, whatever built it, never a copy of our own.
- **Waves from the water.** [Waves](Wind-and-Waves#waves) grow from the wind, the fetch, the depth and the time it has blown, as coastal engineers predict them, so a big lake raises a sea of its own.
- **Storms on real climatology.** Gales and hurricanes are born as often and where real ones are, from measurements; squalls keep the mod's own rate until marine statistics are found ([The science of the sea](The-Science-of-the-Sea)).
- **Tested beside other mods**, with recommendations for mod sets ([Beside other mods](Other-Mods)).
- **The wind on the coordinates readout**: where it blows from, its force and its speed, in every language the game speaks ([Wind and waves](Wind-and-Waves#wind)).
- **Vessels from any wood**, other mods' timber included: the [dugout](Dugout) and the [outrigger canoe](Outrigger-Canoe) from any tree, the [bark canoe](Birch-Bark-Canoe) from birch, elm or spruce bark.
- **Switched off for now:** [capsizing](Surf-and-Swamping#capsizing), unfinished. Capsizing comes back with heel in [Seamanship](#seamanship).

## 2.1

Hulls that take knocks as real ones do, and a guide for modders.

- **[Knocks](Hull-Condition) worked out as they really are.** A hull is struck only when it truly hits something solid, by the speed it loses into it, the way the game works out a fall. A hull counts its own weight, the people aboard with their gear and armor, the empty baskets, crates and chests it carries, and its water, which sloshes. A lagging server no longer breaks a boat on open water.
- **[For modders](For-Modders)**, one page per mechanic, with [Building a vessel](Building-a-Vessel) for vessels that ride our sea.
- **[Shipworks+](Other-Mods#shipworks)** tested beside us.

## 3.0 "Creak and Leak"

Boats no longer burst at a hidden zero: you hear and see where a knock landed, what it did, and why it is going down.

- **Hulls that crack, leak and flood.** A knock [cracks the hull](Hull-Condition) where it lands, and the crack leaks; a hull is lost by flooding, and only a severe blow breaks it up. Look at a vessel to read each cracked place, the leak in litres a minute and its freeboard.
- **Floating by mass.** Every vessel [floats by its mass](Surf-and-Swamping#how-it-floats), sits lower and runs slower loaded, and flooded floats awash or sinks by its wood and load. Hulls ride the swell on periods of their own mass.
- **Lashings, soaking and seams.** The [raft](Raft)'s lashings work loose and are bound up with flax twine, the [reed boat](Reed-Boat) soaks afloat and dries ashore, and the [currach](Hide-Currach)'s seams seep once their grease is gone. A [mend](Mending) patches the place you look at.
- **The sea you can hear.** Open water by its wind, whitecaps hissing from windward, surf on the shore, and a hull that creaks, cracks and trickles as it takes water. The recordings: [Sound credits](Sound-Credits).
- **[The drawn sea for other mods](Reading-the-Sea#the-drawn-sea).** Their vessels ride the swell a client draws, as ours do.
- **A major version**, because the hulls changed vessel keys the modder pages document: [what to change](For-Modders#hulls-that-crack-leak-and-flood).

## 3.1

Every blow harms a hull by what it is and what it meets.

- **[Rams](Hull-Condition#rams).** Vessels that run into each other strike both hulls, sharing the energy by their masses. Creatures and swimmers still only push.
- **[Blows, cuts and loads](Hull-Condition#how-a-hull-takes-a-blow).** A knock, a ram, rapids or lightning loads the whole frame; a club only the shell where it lands, by the wood's toughness and thickness; an axe cuts. Hide springs back unless a lath backs it. A planked hull breaks up once every place is stove in, so an axe can take a boat apart ashore.
- **No dice.** Each blow meets a piece of wood as strong as real timber scatters.
- **The reed boat tears.** Knocks, rapids and lightning tear it, a blade only at its lashings; *Reed torn* shows how far.
- **[Breakers slam](Surf-and-Swamping#breakers)** the windward side as a pressure on the planking: a spilling one gently, a plunging one nine times as hard, and on a steep beach most of them plunge. The skin boats are stove in by a storm's breakers; the raft stands any sea, and the sailboat all but a huge plunging crest.
- **[Thrown ashore](Mooring#thrown-ashore)**, a moored vessel hits the bottom at the speed of the breakers that threw it.
- **For modders:** a vessel's [`shell`](Building-a-Vessel#the-hull), and [what changed](For-Modders#blows-by-their-physics) for a vessel of yours.

## 3.2

One sea for everyone on a server, and calm water that sounds like calm water.

- **The same waves for every player.** Every client draws the same crests at the same moment, so a crewmate sees the wave your boat rides.
- **[One clock at sea](Reading-the-Sea#the-seas-time).** Waves, rain into a bilge, leaks, lightning and a hull soaking all keep the world's time: they stand still when time stands and race while you sleep, so a boat near you lives through the night with you.
- **Calm water that splashes.** No more creaking and knocking when you stand or swim in the shallows. A calm sea is quiet but for the splash of each passing wave, at your feet or at your hull, as loud as the wave is high. The shore laps with the game's own sound again, and the sea's roar rises from silence as the wind picks up.
- **For modders:** [what changed](For-Modders#one-sea-one-clock) for code that reads the time.

## 4.0 "Shipwright"

You build every new vessel where it lies, and move it by paddle, pole and oars, by the rower's own strength.

- **Vessels built in place.** Burn out and hew a dugout from a log, with a real fire, hewing you can see and clay. Strip a bark canoe off a standing tree and frame it where it falls. Bind a reed boat from two bundles of dried reeds. Lay laths for a coracle or a currach and cover them with hides that rot unless prepared. Rig an outrigger on a finished dugout. More: [Building a vessel](Building-a-Vessel).
- **Poles, laths and wedges.** Split a row of logs into poles with wedges, blow by blow, and the poles into laths; soak the laths so they bend. More: [Recipes](Recipes).
- **Paddle, pole and oars, by the rower.** Speed comes from the rower's strength and the hull. Paddle on both sides, changing sides as the boat veers. Pole standing, where the hull bears you, as far as the pole reaches the bottom. Row the currach facing its stern, on its thole pins. Paddle the raft kneeling. More: [Paddles, poles and oars](Paddles-Poles-and-Oars).
- **Hulls you can see.** Cracks show on the hull where they are. A blow on a framed hull lands on its skin and the frame behind. The outrigger's float rides on lashings of its own. More: [Hull condition](Hull-Condition).
- **Afloat by mass.** A vessel heaves by its own mass, and a rider's weight comes aboard as they sit down. More: [The science of the hull](The-Science-of-the-Hull).
- **Launching on foot.** Push a heavy vessel into the water yourself. More: [Building a vessel](Building-a-Vessel).
- **Under a crest.** Your eye goes under a crest that passes over it, and you hold your breath by the sea you see, on a server too. Both are the work of our first contributor, [rhino1998](https://mods.vintagestory.at/show/user/54FEACF9BA0FE0675CB8). More: [Reading the sea](Reading-the-Sea).
- **The handbook** rewritten, and every one of its 33 languages corrected.

## 4.1

- **A setting for each mechanic.** Water in the bilge, hull damage, lightning, mooring strain, currents, rower fatigue and the pole's depth each have a world setting of their own, so you choose how hard the sea is, one mechanic at a time. More: [World settings](World-Settings#the-mechanics).

## 4.2

- **Laths soaked under stones.** Weigh a stack of laths down with stones and lay it on the bottom of any water: in two days the laths are wet, with no barrel and no saw. More: [Recipes](Recipes#soaked-laths).

## 4.3

The sea moves your boat, and the rope holds it by physics.

- **The swell carries every hull** to and fro by its size, heading and draft, whether you sit in it or not. More: [Reading the sea](Reading-the-Sea#how-a-hull-answers).
- **Currents carry every vessel,** paddled and rowed too, and swing a moored one to lie along the stream. More: [Currents](Currents#drift).
- **A mooring rope that holds** at the length you set, and parts only by the jerk or the pull it can't bear; a boat takes one rope. More: [Mooring](Mooring).
- **An anchor that holds by its weight**, better on rock than on sand, and drags in a strong current or a storm. More: [Anchoring](Anchoring).
- Both the swell and the currents are the work of [rhino1998](https://mods.vintagestory.at/show/user/54FEACF9BA0FE0675CB8).

## 4.4

The sea reaches the shore, and you float in it.

- **[Surf on the shore.](The-Look-of-the-Sea#what-you-see)** Waves grow and turn toward the shore, break, run up the beach and slide back, wetting the sand; a bar far out breaks them too, and shelters the water behind it. Surf foam rides the breaking waves again.
- **Waves against a face.** A cliff, a quay or a wall you built stands a wave up against it or takes its spray.
- **A finer sea near you,** divided finer than a block, with short waves on the swell.
- **[Water under the beach](The-Look-of-the-Sea#water-under-the-beach)** as far up as the waves run: dig down to it and the hole fills.
- **[You float by your body](Swimming):** armour and hotbar tools weigh you down, you sink below about 6 m, and a breaking crest drives you under. The work of [rhino1998](https://mods.vintagestory.at/show/user/54FEACF9BA0FE0675CB8).
- **Boats take the waves where they are,** softer behind a bar, and a wave every wave period.
- **Vintage Story 1.22.0** and later.

## Next release

- **Switched off for now:** the open sea's sound, the surf and the whitecaps' hiss, which didn't match the water you see. They come back from real recordings ([Further out](#further-out)). The shore laps with the game's own sound, the game's rowing plays at its own volume, and every wave at a hull, every breaker and every knock still sounds.

## Seamanship

Sailing becomes sailing.

- **Sailing by the wind.** A sail drives a boat by where the wind blows from: fast with the wind on the quarter, slow close to it, and nowhere straight into it. On an island with the wind against you, you may wait for it to turn.
- **Leeway.** The wind pushes every vessel sideways, harder the stronger it blows and the more the vessel shows above water, sail and all.
- **Weathervaning.** A boat at [anchor](Anchoring) swings bow to the wind, and rides the waves the way real boats do.
- **A windsock** to read the wind.
- **Masts that break** in strong wind, by the strength of their wood.
- **Heel, and capsizing back.** A vessel heels as wind on its sail or a breaking wave on its beam leans it over, against its own stability. It ships water as the gunwale goes under, and past the angle it can't come back from it goes over. Sail and sea become two forces on one mechanic.

## The living sea

The sea becomes dangerous to you, not only to your boat.

- **Body in water.** Swimming wears you out, faster in cold water and on an empty stomach; exhausted, you sink. Cold water kills at the rate measured for real people in real seas, and a warm [current](Currents) off a cold coast can save your life.
- **Creatures.** Sharks that hunt swimmers and small boats and dodge your blows; whales; and, deep down, rust creatures.

## The deep

What lies below.

- **Diving.** A diving suit of Jonas tech, with air and light, to work on the seabed. The old trick of diving in plate armour with a lantern no longer works.
- **Harpoons** and **depth sounding**.
- **Shipwrecks**: rare wrecks on the deep seabed, holding what they carried.
- **Raising your own vessel** and what went down with it, after it [sank](Hull-Condition#when-it-is-lost).
- **Wandering traders** who sail the seas on a heading, make landfall where they meet a shore, camp on the beach for a day or two and sail on. Some sell maps to wrecks.
- **Metal boats.**

## Further out

Ideas not yet planned:

- **The sea's sound, redone** from real recordings: the open sea, the surf and the whitecaps, each heard where it is.
- **The sea's own warnings.** Fog over cold [currents](Currents), and life gathering where cold water wells up.

- **Crewed ships.** A big ship for several players: one at the helm, one on the sails, one bailing, one on the anchor, one on lookout. A single player can still sail it, slower and at more risk, and if nobody holds the helm, wind and current turn it.
- **The clockmaker's vessel.** The fastest boat of all, driven by a paddle wheel or a clockwork screw, which only a clockmaker can build and anyone can sail, the way only a hunter makes the best bows.
