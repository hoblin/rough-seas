What Rough Seas' wind, waves and storms stand on, and where we bent it. The hulls are on [The science of the hull](The-Science-of-the-Hull).

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
- **Breaking.** A wave breaks where the water is shallower than 1.28 times its height (McCowan's limit), or out at sea once it is a seventh as high as it is long, as steep as a wave can stand (Michell 1893). A breaker is as long as that steepness makes it, and runs at the speed of a wave that long, no faster than the sea's own waves, slowed over a shallow bed by Eckart's dispersion (1952).
- **Spilling and plunging.** In deep water about 2% of breakers plunge, the rest spill (Gemmrich and Farmer 1999). On a sloping bed the surf similarity parameter decides: a wave plunges when the bed's slope over the root of its steepness lies between 0.5 and 3.3 (Battjes 1974), and its crest lands 4.0 − 9.25 times the slope of its height shoreward of where it broke (Galvin 1969); further in, what is left is its bore. What a breaker does to a hull: [Breakers](The-Science-of-the-Hull#breakers).
- **Whitecaps.** How much of the sea is white at each force starts from Monahan's measured whitecap coverage and is raised a little, so whitecaps read from afar as they do on a real horizon. The look was matched against [NOAA's photographs](https://photolib.noaa.gov/) of each force.

## How the waves carry a hull

The water under a wave moves in a circle: forward under the crest, back in the trough, by `a ω e^(k z)` for a wave of amplitude `a`, frequency `ω` and wavenumber `k`, at `z` under the surface (linear wave theory; Dean and Dalrymple, Water Wave Mechanics, 1991). A hull feels the water's pressure summed over its underwater body (the Froude-Krylov force), so the velocity it takes is that one averaged over the body: `e^(k lift) (1 - e^(-k T)) / (k T)` over its draft `T` under the height it heaves to, and `sinc(k_L L / 2) sinc(k_B B / 2)` over its length `L` and beam `B`, with `k_L` and `k_B` the wave's number along and across its heading. A floating hull displaces its own mass, so the pressure on it grows with its mass as its inertia does, and it takes that velocity whatever it weighs. A loaded hull differs from a light one only by its deeper draft and its slower heave.

Read where the hull is each step, this velocity carries it a little further forward than back: the Stokes drift, `a^2 ω k` for a small body at the surface (Stokes 1847). Averaging over a hull makes a large one drift slower than a small one. Calvert et al. (2021, JFM 915:A73) measured the opposite effect as well, from a variable submergence that lags the surface; it isn't modelled, because its size hangs on a heave damping we haven't measured.

## A body in the water

A [swimmer](Swimming) is one reference body, a man of 1.75 m (Fryar et al. 2021) and 75 kg, the same crew a vessel counts. Its volume by height comes from segment masses (de Leva 1996), densities (Dempster 1955) and landmarks (Drillis and Contini 1966), and its tissue's volume from its fat by the two-compartment model (Siri 1961). The lungs hold the end of a breath out and half a breath more (Quanjer et al. 1993). That makes 70.9 L of tissue and 3.66 L of air. It floats in the sea with about 1.2 L clear, the water at its face, and sinks slowly in fresh water, as measured bodies do: at the end of a breath out 69% of 98 men floated in seawater and 7% in fresh water (Donoghue and Minnigerode 1977).

The equation up and down, with V what lies under the drawn surface and a and w_w the water's own acceleration and velocity:

```
(m + m_a) dw/dt = ρ g V - m g + (ρ V + m_a) a - ½ ρ Cd A |w - w_w| (w - w_w)
```

This is the linear wave force on a body small against the wave (Journée and Massie 2001, 6.3.3). Across, the same form takes the water's horizontal motion, with a broadside drag area. Added mass is a disc of the body's own section, m_a = (8/3) ρ a³ (Brennen 1982), at the waterline while the surface cuts the body and at its widest under water: a little at the neck, a fifth or so of the body at the chest. The drag areas are 0.25 m² along the body and 0.45 m² broadside, from skydivers' fall speeds (FAI); nothing was found measured for a body moving up and down in water.

Held air shrinks by Boyle's law at the chest's depth, lungs × p₀ / (p₀ + ρ g d), so the body turns negative with the chest about 7 m down, and sinks from there.

What a player carries follows the game's line: armour, and what the hotbar's items call a tool or weapon by their own type. Full plate in the game slows a walker by 0.42, and tested field armour weighs 35 ± 5 kg (Askew et al. 2012), so armour weighs 83 kg to the unit of that penalty. Tools are by real analogues, listed in `config/person.json`, all taken as steel.

A crest breaks over a swimmer at the vessels' rate. Its plume holds a mean of about 20% air just after breaking and 1% after one wave period (Lamarre and Melville 1994), not the 24% peak measured at one gauge 0.2 m down at sea (Lamarre and Melville 1992). It reaches 0.6 wave heights down, twice the depth of the air's centre after a period (Lamarre and Melville 1994). Echosounders find bubbles far deeper (Derakhti et al. 2024), too dilute to matter to a body. Its roller runs at 0.75 of the breaker's speed (Rapp and Melville 1990) for about a second (Callaghan 2013).

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

## Where we bent reality, and what is not yet real

- **The world's scale** (×200 across, a block a metre high) and **the short calendar** are the game's own; we follow them.
- **The wave ceiling** of 16 m keeps a hurricane's sea on the world's scale.
- **Winter gales come too seldom.** At 47° north our open sea sees gale-force wind under 2 percent of a winter; the North Sea, among the stormiest seas, sees 7 to 14 percent. The births, seasons and strengths above come from measurements; how wide a depression's gale-force band is, and how long it blows at one place, are not yet measured for the mod.
- **Squalls' frequency** is the mod's own, not yet from data.
