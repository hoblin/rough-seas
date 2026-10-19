Rough Seas needs to know where the sea is far beyond what is built: a wave grows over all the open water upwind, and a current turns over a whole ocean. It asks the world. This page is for a mod that shapes the world's oceans, or calls vanilla's ocean layer.

## The world's own layer

Beyond what is built, Rough Seas reads the world's own ocean layer, `GenMaps.oceanGen`, the one worldgen builds the coast from. It never builds a second one, so the sea we compute lies where the world will build it.

The layer isn't thread-safe, and worldgen and Rough Seas both call it. So from the moment we first read it, we lock it: a prefix on its `GenLayer` takes a lock first and a finalizer releases it last. Your calls to it take the same lock, from any thread, and need nothing else from you.

## A mod that reshapes the oceans

The layer gives numbers, not water, and which numbers mean sea depends on the world. We learn it from the built world: as players travel, we compare the layer with the columns around them, one every 32 blocks, salt water at sea level or land. Once we have seen 200 of each, we settle on the number that splits them best, and it never moves again. Until then we read the layer by a default of 128.

So a worldgen mod that reshapes the oceans needs nothing from us: the sea follows its map, as it does with [Terra Prety](Other-Mods#terra-prety). It should be there when the world is made, though: the world's save keeps what was learned, as `roughseas:oceancalibration`, and a mod added later meets a meaning settled without it. Leave that data alone.
