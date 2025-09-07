using System.Collections.Generic;
using UnityEngine;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;

namespace GlobeRTS.PlanetTerrain.Runtime.VisibilityCulling
{
    /// <summary>
    /// Provides the set of visible tiles and per-face axis data for the cube.
    /// The implementation (FrustumCullingProvider) maintains its own precomputed centers/bounds and frustum logic.
    /// </summary>
    public interface ICullingProvider
    {
        /// <summary>True if the provider uses precomputed centers/bounds.</summary>
        bool PrecomputeEnabled { get; }

        /// <summary>Number of tiles per face side (0..TilesPerSide-1).</summary>
        int TilesPerSide { get; }

        /// <summary>
        /// Rebuilds/refreshes the precomputed data for the given sphere and height parameters.
        /// </summary>
        void RebuildPrecompute(
            float sphereRadius,
            Texture2D heightMap,
            float heightScale,
            float uniformGlobalScale,
            int tilesPerSide
        );

        /// <summary>
        /// Returns tile keys that are visible for the given camera (uses precompute and frustum test).
        /// </summary>
        IEnumerable<TileKey> GetVisibleKeys(Camera cam);

        /// <summary>
        /// Returns the axes for a specific face: localUp, axisA, axisB.
        /// </summary>
        void GetFaceAxes(byte face, out Vector3 localUp, out Vector3 axisA, out Vector3 axisB);
    }
}