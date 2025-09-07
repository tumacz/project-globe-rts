using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.Geometry
{
    /// <summary>
    /// Precomputed, world-space data for a tile used by streaming/culling.
    /// </summary>
    public struct PrecomputedTile
    {
        public Vector3 centerWS;
        public Bounds boundsWS;
    }
}