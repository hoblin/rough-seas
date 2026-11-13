What Rough Seas' wind and storms stand on, and where we bent it. The waves they raise are on [The science of the waves](The-Science-of-the-Waves).

## The world's scale

- **Space.** Vintage Story draws the world small: 50,000 blocks from pole to equator, about 200 times less than the Earth's 10,000 km, and its climate zones are squeezed to fit (the world setting `polarEquatorDistance`). Rough Seas reads the same squeeze: a block across the world is 200 m of real distance in a default world, and 100 m in the *Rough Seas* playstyle, which sets 100,000 blocks from pole to equator. Storm sizes, speeds and birth rates are real distances and areas, turned into blocks by the world's own scale. Heights and depths are not squeezed: a block is a metre.
- **Time.** A day in the game is a day: storms live, travel and blow for as many days as real ones. Only the calendar is short, 12 months of 9 days. So the seasons follow the game's calendar, about 3.4 times faster than the real year, while each day's weather is a real day's.

## Wind

- **The Beaufort scale**, as the World Meteorological Organization keeps it, names the wind's strength everywhere in the mod. ([Beaufort scale](https://en.wikipedia.org/wiki/Beaufort_scale))
- **Background wind.** About 7 m/s over the open sea and 2–4 m/s over land are the means reanalyses and ship records give. Out of storms the wind averages 7 m/s at sea and 4 m/s ashore. ([Copernicus wind climatology](https://data.marine.copernicus.eu/product/WIND_GLO_PHY_CLIMATE_L4_MY_012_003))
- **Prevailing winds** follow the Earth's circulation by latitude: the trade winds, the westerlies and the polar easterlies.

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
- **Winter gales come too seldom.** At 47° north our open sea sees gale-force wind under 2 percent of a winter; the North Sea, among the stormiest seas, sees 7 to 14 percent. The births, seasons and strengths above come from measurements; how wide a depression's gale-force band is, and how long it blows at one place, are not yet measured for the mod.
- **Squalls' frequency** is the mod's own, not yet from data.
