Rough Seas needs to know where the sea is, far beyond what is built. It asks the world. This page is for a mod that shapes the world's oceans or calls vanilla's ocean layer.

## The world's own layer

Rough Seas reads the world's own ocean layer, `GenMaps.oceanGen`, for the sea beyond what is built. It never builds a second one. The layer isn't thread-safe, so we lock its `GenLayer`, a prefix first and a finalizer last. Your calls to it take the same lock, from any thread.

## A mod that reshapes the oceans

What the layer's numbers mean is learned from the built world: where salt water actually lies at sea level. So a worldgen mod that reshapes the oceans needs nothing from us; the sea follows its map. [Terra Prety](Other-Mods#terra-prety) works this way.

The world's save keeps what was learned, as `roughseas:oceancalibration`; leave it alone.
