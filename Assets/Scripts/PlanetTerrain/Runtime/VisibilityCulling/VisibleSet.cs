using System.Collections.Generic;

namespace GlobeRTS.PlanetTerrain.Runtime.VisibilityCulling
{
    using GlobeRTS.PlanetTerrain.Runtime.Geometry;

    /// <summary>
    /// Mutable container for the current visible tile set.
    /// </summary>
    public sealed class VisibleSet
    {
        private readonly HashSet<TileKey> _set = new();

        public void Clear() => _set.Clear();
        public void Add(in TileKey key) => _set.Add(key);
        public bool Contains(in TileKey key) => _set.Contains(key);

        /// <summary>Enumerable view over the contained visible keys.</summary>
        public IEnumerable<TileKey> Items => _set;
    }
}