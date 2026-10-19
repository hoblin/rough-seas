Rough Seas builds its sea, its weather and its hulls from real measurements and the models scientists and engineers use, and bends them only where the game needs it. This page lists what each part stands on, and where we bent it. The other pages say what you meet at sea; this one says why it is so.

## The world's scale

- **Space.** Vintage Story draws the world small: 50,000 blocks from pole to equator, about 200 times less than the Earth's 10,000 km, and its climate zones are squeezed to fit (the world setting `polarEquatorDistance`). Rough Seas reads the same squeeze: a block across the world is 200 m of real distance in a default world, and 100 m in the *Rough Seas* playstyle, which sets 100,000 blocks from pole to equator. Storm sizes, speeds and birth rates are real distances and areas, turned into blocks by the world's own scale. Heights and depths are not squeezed: a block is a metre.
- **Time.** A day in the game is a day: storms live, travel and blow for as many days as real ones. Only the calendar is short, 12 months of 9 days. So the seasons follow the game's calendar, about 3.4 times faster than the real year, while each day's weather is a real day's.

## Wind

- **The Beaufort scale**, as the World Meteorological Organization keeps it, names the wind's strength everywhere in the mod. ([Beaufort scale](https://en.wikipedia.org/wiki/Beaufort_scale))
- **Background wind.** About 7 m/s over the open sea and 2–4 m/s over land are the means reanalyses and ship records give. Out of storms the wind averages 7 m/s at sea and 4 m/s ashore. ([Copernicus wind climatology](https://data.marine.copernicus.eu/product/WIND_GLO_PHY_CLIMATE_L4_MY_012_003))
- **Prevailing winds** follow the Earth's circulation by latitude: the trade winds, the westerlies and the polar easterlies.

## Waves

- **Growth.** Wave height and period come from the wind, the fetch (the open water upwind) and the depth, by Young and Verhagen's formula (1996), as the US Army Corps of Engineers' *Coastal Engineering Manual* (EM 1110-2-1100, part II-2) gives it. The fetch is the straight line upwind, as the manual recommends.
- **Duration.** The time the wind has blown becomes an equivalent fetch (manual, II-2-38). The sea answers to the wind of the last half hour to three days. A day of wind matches the sea the Beaufort scale describes for each force; three days raise a fully developed sea.
- **Depth.** Shallow water holds a wave's period down (manual, II-2-39).
- **Bent: the ceiling.** The formula gives a hurricane waves of 30 m and more, which no real sea reaches. Above 9 m the height bends softly toward 16 m and never passes it.
- **Every wave is different.** Single waves scatter around the significant height by the Rayleigh distribution. ([Significant wave height](https://en.wikipedia.org/wiki/Significant_wave_height))
- **Breaking.** A wave breaks where the water is shallower than 1.28 times its height (McCowan's limit).
- **Whitecaps.** How much of the sea is white at each force starts from Monahan's measured whitecap coverage and is raised a little, so whitecaps read from afar as they do on a real horizon. The look was matched against [NOAA's photographs](https://photolib.noaa.gov/) of each force.

## Gales: mid-latitude depressions

- **How many.** About 440 depressions cross the North Atlantic each year, 130 of them in winter (ERA5 reanalysis, 1979–2020; [Lodise et al. 2022](https://cdip.ucsd.edu/themes/media/docs/publications/journal_articles/JGROceans_Lodise.pdf), Table 1). Over the basin that is about one new depression per million km² a month in winter; ours are born at the same rate per day and area.
- **Where and when.** They follow the storm tracks, between 30° and 70° of latitude and densest near 50° ([Hoskins and Hodges 2002](https://doi.org/10.1175/1520-0469(2002)059%3C1041:NPOTNH%3E2.0.CO;2)). Spring brings 0.87 of winter's number, summer 0.65 and autumn 0.88 (Lodise et al.).
- **How strong.** Most peak near 21 m/s, and 3 to 5 percent reach hurricane force (QuikSCAT; [von Ahn et al. 2006](https://ocean.weather.gov/quikscat/WAF_VonAhn_Sienkiewicz.pdf)). A depression grows on the temperature difference between cold polar and warm subtropical air, which meteorologists call baroclinity. So its strength follows the north-south temperature gradient where it is born, as the game itself computes it with every mod that changes temperature. Winter's gradient is about twice summer's, and winter's gales come about eight times as often as summer's, within the five to ten times the real North Atlantic shows.
- **Shape.** Its strongest winds blow in a broad band 110 to 330 km from its centre (NOAA Ocean Prediction Center composites, [Mariners Weather Log 2005](https://vos.noaa.gov/MWL/april_05/cyclones.shtml)), and fade only toward its edge. Its edge lies 400 to 800 km from the centre: 300 to 500 km over the Mediterranean ([Schneidereit et al. 2010](https://mi-pub.cen.uni-hamburg.de/static_pages/fraedrich/fileadmin/files/forschung/theomet/docs/pdf/2010-Schneidereit-Ble-Frae-QJ.pdf)), over 900 km over the oceans (Rudeva and Gulev).
- **Life.** It moves at 15 to 55 km/h (Lodise et al.) and lives 2 to 5 days. It peaks near the middle of its life, deepening fastest in its last day before the peak, then slowly fills ([ERA5 composites, WCD 2021](https://wcd.copernicus.org/articles/2/1111/2021/); [WCD 2026](https://wcd.copernicus.org/articles/7/873/2026/)).

## Hurricanes

- **How many and where.** 6.4 Atlantic hurricanes a year ([NOAA AOML](https://www.aoml.noaa.gov/hrd/tcfaq/E10.html), 1981–2016), born at least 5° from the equator and mostly between 10° and 20°, over water of at least 26.5 °C ([NOAA AOML](https://www.aoml.noaa.gov/hrd/tcfaq/A15.html); Gray 1968), from June to November, peaking in September ([NHC climatology](https://www.nhc.noaa.gov/climo/)).
- **Shape.** The strongest wind rings a small eye, and gales reach three to six times further out ([Chavas and Emanuel 2010](https://dspace.mit.edu/handle/1721.1/64407)).
- **Life.** They move at 17 to 50 km/h, faster toward the poles ([NOAA AOML, HURDAT](https://www.aoml.noaa.gov/hrd/tcfaq/G16.html)). Real ones live 4 to 7 days, a week or more for the long-lived; ours live 3 to 7.

## Squalls

Squall lines move at 30 to 80 km/h; the Amazon's coastal lines run at 50 to 60 km/h and come every two days. How often squalls form at sea is not yet taken from measurements: there are few marine statistics of them, so their rate stays the one the mod started with.

## Knocks

- **Water in the bilge sloshes.** Water with a free surface doesn't stop with the hull: it runs on, piles against the bow a moment later and spends part of its energy in the splash. A hull brimful moves almost as one body, so the mod counts the water's weight by how full the bilge is. The part left free to slosh then peaks at half full, which is the [free surface effect](https://en.wikipedia.org/wiki/Free_surface_effect) naval architects guard against.
- **Armour's weight comes from its stride.** The game gives armour no weight, only a slower walk. The mod reads the weight back from that penalty, calibrated on plate: a complete field harness of the fifteenth century weighed 15 to 25 kg (Alan Williams, *The Knight and the Blast Furnace*, 2003), and the game's plate slows its wearer by 0.14 a piece. Another mod's armour weighs by its own penalty.

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

- **The world's scale** (×200 across, a block a metre high) and **the short calendar** are the game's own; we follow them.
- **The wave ceiling** of 16 m keeps a hurricane's sea on the world's scale.
- **Winter gales come too seldom.** At 47° north our open sea sees gale-force wind under 2 percent of a winter; the North Sea, among the stormiest seas, sees 7 to 14 percent. The births, seasons and strengths above come from measurements; how wide a depression's gale-force band is, and how long it blows at one place, are not yet measured for the mod.
- **Squalls' frequency** is the mod's own, not yet from data.
- **A stove-in crack is 25 cm²**, a hole 5.6 cm across, set for play rather than measured.
- **Reed weighs 200 kg/m³**, for play: a bundle of dry totora is spongy pith with air between the stems, and totora pressed into boards weighs 316. How fast it soaks (its own weight in water every 18 hours), dries (240 kg a sunny day) and rots (a third of what each drying drives out) is set so a reed boat lies on the bottom after about three days afloat and is past saving after about five soakings: history's pace, not a measurement.
- **A whole bamboo culm weighs 230 kg/m³**, for play: its wall of 630 kg/m³ taken a tenth of its diameter thick, hollow between the nodes.
- **A currach's seams** seep 30 litres a game hour once ungreased, and a greasing lasts two and a half days afloat: Aran crews dressed their currachs every few days, and the litres are ours.
- **The waterline lies level.** A hull doesn't heel yet, so a side crack leaks by how low she sits, not by which side the wind leans her to; heel comes in [Seamanship](Roadmap#seamanship). How fast a hull settles after a crest, 0.3 of critical in heave, is ours too.
- **Cargo weighs only its containers.** A player carries loads no real crew could lift, so what is in a basket or chest adds nothing to a knock; the containers count at their own empty weight. Mail, which the game slows less than its real 10 to 14 kg would, comes out light.
