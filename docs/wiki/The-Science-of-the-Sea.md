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
- **Breaking.** Over a shallow bed the share of waves that break comes from the energy balance under [The shore](#the-shore), and those are the tallest of the sea, the ones a depth-limited Rayleigh distribution cuts off (Battjes and Janssen 1978). Out at sea a wave breaks once it is a seventh as high as it is long, as steep as a wave can stand (Michell 1893). A breaker is as long as that steepness makes it, and runs at the speed of a wave that long, no faster than the sea's own waves, slowed over a shallow bed by Eckart's dispersion (1952).
- **Spilling and plunging.** In deep water about 2% of breakers plunge, the rest spill (Gemmrich and Farmer 1999). On a sloping bed the surf similarity parameter decides: a wave plunges when the bed's slope over the root of its steepness lies between 0.5 and 3.3 (Battjes 1974), and its crest lands 4.0 − 9.25 times the slope of its height shoreward of where it broke (Galvin 1969); further in, what is left is its bore. What a breaker does to a hull: [Breakers](The-Science-of-the-Hull#breakers).
- **Whitecaps.** How much of the sea is white at each force starts from Monahan's measured whitecap coverage and is raised a little, so whitecaps read from afar as they do on a real horizon. The look was matched against [NOAA's photographs](https://photolib.noaa.gov/) of each force.

## Short waves

Waves between 8 m and half a metre long are drawn from the wind sea's equilibrium range: E(ω) = α g² ω⁻⁵, α = 0.0081 (Phillips 1958, *J. Fluid Mech.* 4; the constant as the Coastal Engineering Manual, EM 1110-2-1100 Part II-1, gives it). Integrated over a band of wavelengths L1 to L2 that is a variance of α (L2² − L1²) / (4 (2π)²): about 5 cm of root-mean-square height for 4–8 m waves and 0.6 cm for 0.5–1 m ones. A sea too low to hold them carries less, in proportion. A hull averages out what is shorter than itself, so the short waves don't rock a boat. Each level of the drawn mesh drops the waves shorter than twice its spacing.

## How the waves move a hull

The water under a wave moves in a circle: forward under the crest, back in the trough, by `a ω e^(k z)` for a wave of amplitude `a`, frequency `ω` and wavenumber `k`, at `z` under the surface (linear wave theory; Dean and Dalrymple, Water Wave Mechanics, 1991). Over a bed of depth `d` the exponential becomes `cosh k(d + z) / sinh kd`, so the same wave moves the water faster in the shallows. A hull feels the water's pressure summed over its underwater body (the Froude-Krylov force), so the velocity it reads is that one averaged over the body: in deep water `e^(k lift) (1 - e^(-k T)) / (k T)` over its draft `T` under the height it heaves to, and `sinc(k_L L / 2) sinc(k_B B / 2)` over its length `L` and beam `B`, with `k_L` and `k_B` the wave's number along and across its heading.

A hull moves by the sum of the forces on it, over its mass and the water it drags with it (its added mass, Lamb's). The water's pressure and the added mass push it by `(m + A) Du/Dt`, the water's own acceleration (Morison's inertia term), and a floating hull displaces its own mass, so that push gives it exactly the water's acceleration whatever it weighs. So the hull's velocity is the waves' water at its place, `u`, plus the rest, `r`, and only `r` changes by forces. With `v_c` the current and `w` the hull's own way, a rower's or a sail's, its velocity through the water is `s = r − v_c + w`, and:

```
(m + A) dr/dt = −(m + A) (s·∇)u − D(s) + D(w) + T
```

`−(s·∇)u` is the difference between the water's acceleration and the change of `u` along the hull's own path: nothing for a hull lying with the water, and on a wave's front face, where the water ahead runs slower than behind, a push down the face for a hull running with the wave. Near the wave's speed, `s ≈ c`, it is `−c ∂u/∂x = ∂u/∂t`, which in shallow water is `−g ∂η/∂x`: gravity along the face, the force that carries a surfer. `D` is the hull's drag on its velocity through the water ([The science of the hull](The-Science-of-the-Hull#driving-it)), less `D(w)`, what its own way meets and its rower already pays; `T` a line's tension. The current's pull is in `D` too, since the hull's velocity through the water counts it. Nothing is written for surf-riding: a hull held on a face near the wave's speed, where the push balances the drag, is carried toward the shore, and a slow hull lets the face pass under it. On the bench a sailboat running at 7 m/s through the water before a gale's 5.6 m swell onto a 1:8 beach was pushed on at up to 1.5 m/s² and went from 6.4 to 10.5 m/s over the ground on a face; a canoe paddled at 2.2 m/s let the faces pass.

The hull reads `u` where it is each step, so lying with the water it moves with it as a body in the water does, and each wave carries it a little further forward than back: the Stokes drift, `a^2 ω k` for a small body at the surface (Stokes 1847). Averaging over a hull makes a large one drift slower than a small one. Calvert et al. (2021, JFM 915:A73) measured the opposite effect as well, from a variable submergence that lags the surface; it isn't modelled, because its size hangs on a heave damping we haven't measured.

A hull's own way comes from vanilla's physics, worked out from its code: each tick vanilla moves a boat once, or in ten moves while her motion passes 0.1 blocks a tick (6 m/s), divides the motion by up to 1.25 when it is fast and one move long, and scales it by its water drag unless a rower takes that off. So a sailboat under full sail makes about 6.9 m/s through the water, not the 5.6 one move would give.

## The shore

The drawn waves read the seabed around the player, a block a cell: its depth under still water and its slope; the beach is negative depth. A wave feels the bed at its own scale, so its speed, its turning and the slope are read over a quarter of its length, while what breaks it is the depth under it. H0 is a wave's deep-water height and L0 = g T² / 2π the deep-water wavelength of the peak period T.

### Over the bed

- **Energy.** A wave's height comes from the energy carried to it, not from the depth where it stands. Each train's energy E = a² / 2 is carried along its crests by the steady balance div(E cg n) = −D, cg the group velocity and n the way the crests run (Battjes and Janssen 1978; SWAN). With nothing lost, a wave slowing over a shoaling bed grows as H = H0 √(cg0 / cg), Green's law H ∝ d^(−1/4) in shallow water (Dean and Dalrymple 1991); its wavenumber follows Eckart's dispersion (1952), k = k0 / √tanh(k0 d). The energy comes in from a coarser solution over the three regions around, on the depths and heights the server sends with the sea map, so a reef beyond the map still shelters the water behind it. Off land upwind a wave starts from the sea that stands there, as the fetch made it, not shoaled again from the deep.
- **Refraction.** Crests turn toward the shore by Snell's law, sin θ / c constant along a ray. Each wave's lag is the eikonal |∇φ| = k, solved over the bed, starting from the same coarser solution, so every player nearby sees the same crests. At the map's edge the two fields are added, not their phases mixed. A wave's direction is averaged over a quarter of its length to either side, since it can't turn on a shorter scale; rays that would cross behind an islet spread as diffraction spreads them.
- **Breaking.** A breaking sea loses D = α σ Qb E / (π B), B its energy over what the depth carries, σ its mean frequency, α = 1 (Battjes and Janssen 1978, shared among the trains by their energy as SWAN shares it). Qb, the share of waves breaking, solves 1 − Qb + B ln Qb = 0 and whitens the crests. Hrms is held to d / 1.28 (McCowan 1894, as SWAN applies it). A bar breaks the waves as a beach does, and the energy it takes stays lost behind it. Plunging waves ([Spilling and plunging](#waves)) steepen their crest to the cusp.
- **A broken wave's speed.** Waves in the surf run at c = √(g h) √(1 + 0.45 Hs / h), a field fit (Holland 2001, *IEEE Trans. Geosci. Remote Sens.* 39), so the depth that sets their speed is h + 0.45 Qb Hs. At the shoreline the bore runs no slower than the sheet it throws (below), U = √(2 g R): a depth of 2 R, R the significant wave's run-up.

### Run-up

R2% is the height above still water only 2% of the swash passes. The slope it climbs is read along the waves' path, up to 48 blocks inland, as a ramp from the waterline through the corner of the first block whose top the run-up reaches.

- **Sand and soil up to 1:7.** R2% = 1.1 (0.35 β √(H0 L0) + ½ √(H0 L0 (0.563 β² + 0.004))), β the slope, fitted over ten field experiments on natural beaches (Stockdon, Holman, Howd and Sallenger 2006, *Coastal Engineering* 53(7)). Where β / √(H0 / L0) is under 0.3, a flat dissipative beach, R2% = 0.043 √(H0 L0). A 4 m sea of 9 s on a 1:20 beach runs about 1 m up it.
- **Gravel, rock and slopes over 1:7.** Ru2% = 1.65 γf ξ Hm0, at most γf (4.0 − 1.5 / √ξ) Hm0, ξ the surf similarity parameter of the spectral period Tm−1,0 = T / 1.1 (EurOtop 2018, eq. 5.1, 5.2 and 4.2). The roughness γf is 1.0 for rock and sand, as a smooth impermeable slope, and 0.55 for gravel, as two layers of rock on an impermeable core (table 5.2).
- **1:1 and steeper.** EurOtop measured slopes only; such a shore runs up by one wave height, the crest of a linear clapotis.

### The sheet

- **Its shape.** A bore collapses at the shoreline and throws a sheet up the beach (Shen and Meyer 1963). Its front leaves at U = √(2 g R), the frictionless speed that climbs R, and moves ballistically along the slope, s = U t − ½ g sin β t², with tan β = R / X, X the farthest the run-up's sheet reaches followed block by block (below): so it stops where that sheet stops, on a flat beach as on a stepped one, and runs back down as it came. Behind it the sheet is a lens, h = (x_front − x)² / (9 g t²), thickest at the shoreline, of mean depth U² / 27 g. Each wave runs up R2% √(H / H2%), H2% the Rayleigh sea's 2% height, since run-up grows as √(H L0), H the wave's height when its bore reached the shoreline. A sheet lasts until it has run back, and the next one runs up over it.
- **Steps.** The front climbs a block's step only while its velocity head u² / 2g reaches over it. Between steps it loses speed to quadratic bed friction over the lens's mean depth, with a coefficient of 0.007 fitted to over 4,500 uprushes on a sandy beach (Puleo and Holland 2001, *Coastal Engineering* 43(1)). The measured run-up already includes that friction, so the front is thrown just fast enough to reach R. It stops where it can't climb, where the bed has drunk it, or when the next bore runs over it.
- **Soaking in.** The bed drinks the sheet at its hydraulic conductivity by Darcy's law, in m/s: sand 10⁻⁴, gravel 3·10⁻², soil 10⁻⁶, rock nothing (Freeze and Cherry 1979, *Groundwater*, table 2.2). On gravel little runs back; on rock all of it does.
- **Wet sand.** Wet sand has half the albedo of dry, 0.091 against 0.182 (Ångström, as Lekner and Dorf 1988 quote him, *Applied Optics* 27(7)). Ground is darkened by the share of time it stays wet: the time the sheet's mean depth takes to soak in, over the wave period, times the chance a wave reaches it, exp(−ln 50 (R / R2%)²).
- **The water under the shore** reaches as far as the sheet of the higher of two seas runs: today's, and the background wind's mean blowing fully built from the prevailing way. A loaded shore reads its sea again every 10 game minutes.

### Faces

A face is any side of a full block the sea or a sheet meets, a cliff and a beach step alike. A step steeper than 1:1 is a wall, and so is a second riser straight after the first: a sheet stops at its foot and the water under the shore doesn't pass it.

- **Reflection.** Kr = tanh(a ξ^b), a = 0.167 (1 − exp(−3.2 γf)), b = 1.49 (γf − 0.38)² + 0.86 (Zanuttigh and van der Meer 2008, *Coastal Engineering* 55(10)), ξ of the slope from 1.5 Hm0 below still water up to the face (EurOtop 2018, 5.3.4): nothing to speak of on a beach, all of it at a wall. The reflected wave leaves in phase at the face and stands with the incident one as a clapotis, its crest (1 + Kr) H / 2 square to the face and cos θ of that at an angle θ. It rises from the face no steeper than tan 30°, beside the 120° crest of Stokes' limiting wave (1880). Off a short face it thins as its width over √(L s), s the distance.
- **A breaking wave strikes.** It breaks at a face when Qb there is a tenth or more, or when it arrives whitecapped. The water it throws above its clapotis crest is EurOtop 2018's impulsive overtopping of a vertical wall, read as the water passing z = Rc: q / √(g H³) = 0.011 (H / (h s))^½ exp(−2.2 z / H) up to z / H = 1.35, and 0.0014 (H / (h s))^½ (z / H)^−3 above (eq. 7.7 and 7.8), the closest analogue measured for a crest breaking before a deep toe. A face lower than the crest takes no jet: the wave runs over it. Drops go no faster than 2.2 C, C the wave's celerity, the highest water speed Ikeda et al. measured up a wall under a plunging breaker: about 3 m/s under waves of 1.36 m/s (2012, 29th Symposium on Naval Hydrodynamics).
- **A stopped sheet strikes.** It throws all the water of its lens, the drops reaching at most its velocity head u² / 2g, by the same exp(−2.2 z / H), H that head.

### Foam and clear water

- **Foam.** Broken water foams the surface where it arrives, at sea, on the beach and at a face alike, and the foam's area then shrinks, e-folding in 3.1 s, the middle of the 1.4 to 4.8 s Callaghan, Deane and Stokes measured for whitecaps (2012, *JGR Oceans*). It arrives with the dominant wave's front over water, in the share breaking; with the swash's front over land, all of it; and one wave height wide at a face a broken wave strikes. So it is solid only on the front and breaks into patches and traces behind, and a line of it runs along the swash's tip. The wind's foam gives way to it where the waves break.
- **Clear shallow water.** The water's opacity is 1 − e^(−2 K d), light fading down to the bed and back up. K ranged 0.05 to 3.6 m⁻¹ over 94 coastal sites (Corcoran and Parrish 2021, *Photogrammetric Engineering & Remote Sensing* 87); no surf-zone measurement was found, and 1 m⁻¹, turbid coastal water, is taken.

## A body in the water

A [swimmer](Swimming) is one reference body, a man of 1.75 m (Fryar et al. 2021) and 75 kg, the same crew a vessel counts. Its volume by height comes from segment masses (de Leva 1996), densities (Dempster 1955) and landmarks (Drillis and Contini 1966), and its tissue's volume from its fat by the two-compartment model (Siri 1961). The lungs hold the end of a breath out and half a breath more (Quanjer et al. 1993). That makes 70.9 L of tissue and 3.66 L of air, 74.6 L in all. In seawater, 1025 kg/m³, that displaces 76.4 kg: the body floats with about 1.4 L clear, the water at its face, and has 1.4 kg to spare. In fresh water, 998 kg/m³, it displaces 74.4 kg and sinks slowly. Measured bodies do the same: at the end of a breath out 69% of 98 men floated in seawater and 7% in fresh water (Donoghue and Minnigerode 1977).

The equation up and down, with V what lies under the drawn surface and a and w_w the water's own acceleration and velocity:

```
(m + m_a) dw/dt = ρ g V - m g + (ρ V + m_a) a - ½ ρ Cd A |w - w_w| (w - w_w)
```

This is the linear wave force on a body small against the wave (Journée and Massie 2001, 6.3.3). The water's motion is the drawn surface's own: its rise and its acceleration are the time derivatives of the height drawn, the bed's gain, a broken wave's bore and the reflection included. Under the surface they fade as linear theory has them, sinh k(d + z) / sinh kd, e^(kz) in deep water, with the profile stretched over the water column up to the drawn surface (Wheeler 1970), so the surface moves exactly as drawn and the water stands still at the bed; the body reads them at the middle of its part under water. Across, the same form takes the water's horizontal motion, the one a hull takes ([How the waves move a hull](#how-the-waves-move-a-hull)): the change of the water's velocity at the body and `−(r·∇)u` for its own velocity `r` through the water, each times `(ρ V + m_a) / (m + m_a)`, which is less than one for a body that doesn't displace its whole mass.

The swimmer's own stroke is a thrust, as a rower's is: 38.9 N along where the swimmer looks, up while jump is held. That is the drag triathletes, the trained swimmers nearest an ordinary one measured, swam against at their 0.90 m/s, 35 W of it (Toussaint 1990, *Med. Sci. Sports Exerc.* 22(3)); at rest, tethered, trained swimmers pull more, 92 to 104 N on average (Neiva et al. 2011). A swimmer lies along the stroke, so its drag then is that of the same front crawl, Cd A = 2 D / (ρ v²) = 0.096 m², and it reaches about 0.9 m/s; still in the water, it drags broadside.

Added mass is the hulls' own model (Lamb, *Hydrodynamics*, §373). While the surface cuts the body, it is Lamb's disc of the body's section at the waterline, the free surface carrying half (Newman, *Marine Hydrodynamics*): (4/3) ρ a³ for a round one, about 11 kg with the chest at the water (a = 0.20 m) and half a kilogram with only the head out. Once the body is wholly under, it is Lamb's prolate spheroid of its own length and volume: 0.044 of its displaced water along its length, 3.3 kg, up and down, and 0.92 of it across, 70 kg, taken across by the share of the body under water. The drag areas are 0.25 m² along the body and 0.45 m² broadside, from skydivers' fall speeds (FAI); nothing was found measured for a body moving up and down in water.

Held air shrinks by Boyle's law at the chest's depth, lungs × p₀ / (p₀ + ρ g d). The body turns negative once its 3.66 L of air are squeezed to 2.26 L, with the chest about 6 m down, and sinks from there.

What a player carries follows the game's line: armour, and what the hotbar's items call a tool or weapon by their own type. Full plate in the game slows a walker by 0.42, and tested field armour weighs 35 ± 5 kg (Askew et al. 2012), so armour weighs 83 kg to the unit of that penalty. Tools are by real analogues, listed in `config/person.json`, all taken as steel, which keeps 87% of its weight in seawater: a knife (0.7 kg) floats the body lower, an axe (1.85 kg) outweighs its 1.4 kg to spare.

A crest breaks over a swimmer by the vessels' rule: a wave passes each dominant period, its height drawn from the Rayleigh spread; in surf it breaks for certain when it is among the tallest the breaking share cuts off ([Over the bed](#over-the-bed)), and elsewhere by the wind's whitecap share ([Breaking](#waves)). Its plume holds a mean of about 20% air just after breaking and 1% after one wave period (Lamarre and Melville 1994), not the 24% peak measured at one gauge 0.2 m down at sea (Lamarre and Melville 1992). It reaches 0.6 wave heights down, twice the depth of the air's centre after a period (Lamarre and Melville 1994). Echosounders find bubbles far deeper (Derakhti et al. 2024), too dilute to matter to a body. Its roller runs at 0.75 of the breaker's speed (Rapp and Melville 1990) for about a second (Callaghan 2013).

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
