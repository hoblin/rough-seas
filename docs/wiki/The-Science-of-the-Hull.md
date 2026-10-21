What Rough Seas' hulls stand on: how they take a blow, float, leak and ride the swell, and where we bent it. The sea that strikes them is on [The science of the sea](The-Science-of-the-Sea).

## Knocks

- **Water in the bilge sloshes.** Water with a free surface doesn't stop with the hull: it runs on, piles against the bow a moment later and spends part of its energy in the splash. A hull brimful moves almost as one body, so the mod counts the water's weight by how full the bilge is. The part left free to slosh then peaks at half full, which is the [free surface effect](https://en.wikipedia.org/wiki/Free_surface_effect) naval architects guard against.
- **Armour's weight comes from its stride.** The game gives armour no weight, only a slower walk. The mod reads the weight back from that penalty, calibrated on plate: a complete field harness of the fifteenth century weighed 15 to 25 kg (Alan Williams, *The Knight and the Blast Furnace*, 2003), and the game's plate slows its wearer by 0.14 a piece. Another mod's armour weighs by its own penalty.
- **Rams.** Two hulls that meet lose the energy of their closing speed held by their reduced mass, ½ μ v², μ = m₁m₂ / (m₁ + m₂), as in any inelastic collision; each hull takes half.

## Hulls

- **A knock, a ram, rapids or lightning** loads the whole frame: its energy is weighed against the hull's capacity, and cracks its place by that share. A hull's capacity scales by its wood's modulus of rupture against oak's, from the [Wood Database](https://www.wood-database.com/): ebony 158 MPa, purpleheart 152, bamboo 130, birch 114, maple 109, walnut 101, oak 97, larch 90, acacia 87, pine 83, bald cypress 73, redwood 62, aged wood 49, kapok 36. A species with no published value counts as 93.5, the middle of the vanilla woods.
- **A blow on the shell** breaks it around a hole: its toughness times the hole's rim times the shell's thickness. Woods' toughness is their impact bending work at 12% moisture (Kollmann et al., in the Norwegian handbook *Wood – nature's most beautiful raw materials*, 1999): birch 94 kJ/m², beech 83, oak 75, ash 68, maple 65, elm 60, linden 50, pine and spruce 40; a wood not in the table counts as 60, their median. Willow takes aspen's, of its own family and weight, and thuja spruce's and pine's. In oak, a sailboat's 19 mm plank gives to about 250 J, a dugout's 5 cm wall to about 670 J, and the bark canoe's bark with its cedar lining to about 23 J. A weapon strikes with 200 J a point of its damage, a blast or a bolt with 1000 J a point.
- **A cut** spends its work on parting the fibres alone: softwood's work of fracture across the grain is 9.2 kJ/m² (*Nature* 252, 116, 1974) against 40 kJ/m² of impact bending work, 0.23 of it. Hide tears at 17 kJ/m² (pig skin by trouser tear, Comley and Fleck 2010); a lashing, unmeasured as rope, as flax in epoxy, 5.7 kJ/m². Bark splits along its grain as a thick birch veneer, at birch's work of fracture, 656 J/m² (*Wood Science and Technology*, 2024).
- **No two pieces of wood are alike.** Clear wood scatters around its table value with a coefficient of variation of 16% in bending strength, 25% in impact bending and 34% in toughness (Wood Handbook, FPL-GTR-282, table 5-6), lognormally; every blow meets its own piece.

## Breakers

- **A breaker's pressure.** A spilling roller presses as a stream does, ½ρc². A plunging crest slams a flat panel at 1.2 times its speed with a coefficient of 2π: ½ρ·2π·(1.2c)² (DNV-RP-C205, 2010, §8.7.2.5 and §8.8.1.2). In deep water a spilling breaker presses about 6 kPa at 1 m high, 23 kPa at 4 m and 56 kPa at 10 m, a plunging one nine times that. Which breakers spill and which plunge: [Waves](The-Science-of-the-Sea#waves).
- **It bends the planking.** A strip under an even pressure breaks at its modulus of rupture times thickness² over 6 k span², k being 1/12 for a plank fastened at both ends, 1/8 for a lath propped at its ends and 1/2 for a dugout's side standing free from the bottom. The sailboat's planks are 19 mm on frames 24 cm apart, as Herreshoff's rules (1927) give a yacht her size. Wet wood bends to 0.57 of its dry strength, the mean over eleven woods (Wood Handbook). In oak, the coracle's laths give at 21 kPa, the currach's at 32, the bark canoe at 60, the dugout's sides at 164, the outrigger's at 209 and the sailboat's planks at 705; the raft's logs at about 3 MPa.

## Floating

- **Archimedes.** A hull sinks until it pushes aside its own weight of seawater, 1025 kg/m³, so her draft is her full mass over 1025 times her waterplane, the area she cuts at the water. Each vessel's depth and waterplane come from its real form: the dugout a 4.5 m log 0.75 m wide with rounded ends, the currach a 4 m hull 1.1 m wide and full amidships. A raft and a reed boat are solid, pressed down through their own thickness.
- **What each material weighs.** Woods by their average dried weight in the [Wood Database](https://www.wood-database.com/): oak 675 kg/m³, pine 550, ebony 955, ekki 1065; kapok by CIRAD's Tropix, red mangrove by Galvão et al. (2025). Hide counts as dry leather, 860 kg/m³, and cast-iron ballast 7200 ([Engineering ToolBox](https://www.engineeringtoolbox.com/density-solids-d_1265.html)); birch bark 560 (*Silva Fennica*, article 5113). A wood of no known species weighs 610, the middle of the vanilla woods. A hull's weight is given for her reference build, oak for a wooden one, and the same shape in another wood weighs by its density.
- **Ballast.** A keelboat carries about two fifths of her weight as ballast to stand up to her sail, so the sailboat does, in iron, and an oak one flooded goes down.
- **A body floats awash.** A human body is about as dense as seawater, so the water holds up the people aboard a flooded hull; their gear, their armour and the containers weigh on it.
- **Speed.** At the same paddling power, drag grows with the wetted area, which grows as displacement to the ⅔, and with speed squared: speed goes as displacement to the −2/9. A hull's speed follows her full mass against her reference build with one 75 kg rider.

## Leaks

- **Torricelli's law.** Water runs in through a hole at √(2 g h), h the depth of the hole under the waterline. Through a sharp-edged hole the jet narrows, and the flow is 0.61 of the hole's area times that speed: a contraction of 0.62 by a velocity coefficient of 0.98 (US Bureau of Reclamation, *Water Measurement Manual*, chapter 2).
- **Against a real table.** A hull stove in at one place leaks like a 5.6 cm hole: three litres a second 20 cm down. A hard knock's crack, a quarter of that, lets in 56 litres a minute a foot down, a little over what [Landfall Navigation](https://landfallnavigation.com/)'s flooding table gives a 2.5 cm hole there: 45 litres a minute. A crack under the water already inside her leaks by the difference between the sea outside and the water in her, so a bottom crack lets in as much while she fills as when she was dry.
- **Where a side cracks.** Rocks and quays meet a side at the turn of the bilge, a quarter of the way up from the keel: above the water in a light boat, below it once she is laden.
- **Reed.** A bundle of reed soaks: on Lake Titicaca the totora *balsas* are dried ashore between trips and last months, and Heyerdahl's papyrus *Ra II*, with nowhere to dry, settled about 10 cm a day crossing the Atlantic.

## Riding the swell

- **Heave and pitch.** A hull bobs on a natural period of 2π √((M + A) / (ρ g A_w)), its mass M and the water A it drags with it, about its own mass for a small open boat (Aalto University's seakeeping notes). For a hull of straight sides that is 2π √(2 × draft / g): about 0.6 s for the bark canoe, 1.3 s for an oak dugout and 2.3 s for the sailboat.
- **Roll.** The US Coast Guard's rule for small fishing vessels, 46 CFR 28.575, gives the roll period as 2 C B / √GM, with C = 0.373 + 0.023 B/d − 0.00043 L from her beam B, draft d and length L; GM is that of a box hull with her centre of gravity at half her depth. A small hull without bilge keels rolls on for several swings, at a damping of 0.06 to 0.1 of critical; ours rolls at 0.1.
- So a light canoe follows a long swell as the water lies, while a heavy, deep hull lets a short chop pass under her and rolls on its own longer period.

## Where we bent reality, and what is not yet real

- **A stove-in crack is 25 cm²**, a hole 5.6 cm across, set for play rather than measured; a blow must break the shell around it.
- **A point of weapon damage is 200 J**, not yet measured: an iron axe's swing comes to 600 J, more than the 67 to 312 J measured for real strikes.
- **What backs a hide or binds a reed boat** is read off the models: how much of a side the laths cover, how far apart the lashings lie. The bark canoe's bark is 5 mm, for play.
- **Reed weighs 200 kg/m³**, for play: a bundle of dry totora is spongy pith with air between the stems, and totora pressed into boards weighs 316. How fast it soaks (its own weight in water every 18 hours), dries (240 kg a sunny day) and rots (a third of what each drying drives out) is set so a reed boat lies on the bottom after about three days afloat and is past saving after about five soakings: history's pace, not a measurement.
- **A whole bamboo culm weighs 230 kg/m³**, for play: its wall of 630 kg/m³ taken a tenth of its diameter thick, hollow between the nodes.
- **A currach's seams** seep 30 litres a game hour once ungreased, and a greasing lasts two and a half days afloat: Aran crews dressed their currachs every few days, and the litres are ours.
- **The waterline lies level.** A hull doesn't heel yet, so a side crack leaks by how low she sits, not by which side the wind leans her to; heel comes in [Seamanship](Roadmap#seamanship). How fast a hull settles after a crest, 0.3 of critical in heave, is ours too.
- **Cargo weighs only its containers.** A player carries loads no real crew could lift, so what is in a basket or chest adds nothing to a knock; the containers count at their own empty weight. Mail, which the game slows less than its real 10 to 14 kg would, comes out light.
