using System.Collections.Generic;
using UnityEngine;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;

namespace GlobeRTS.PlanetTerrain.Runtime.VisibilityCulling
{
    /// <summary>
    /// Culling implementation: stores axes for the 6 faces, precomputed centers/bounds,
    /// and performs the frustum test (TestPlanesAABB). The DTM no longer computes planes/axes/bounds.
    /// </summary>
    public sealed class FrustumCullingProvider : ICullingProvider
    {
        // ---- configuration ----
        public bool PrecomputeEnabled { get; private set; }
        public int TilesPerSide { get; private set; }

        // ---- axes for 6 faces ----
        private readonly Vector3[] _axisA = new Vector3[6];
        private readonly Vector3[] _axisB = new Vector3[6];

        private struct PreTile { public Vector3 center; public Bounds bounds; }
        private Dictionary<TileKey, PreTile> _pre;

        private float _sphereRadius;
        private Texture2D _heightMap;
        private float _heightScale;
        private float _uniformScale;

        private readonly Plane[] _planes = new Plane[6];

        public FrustumCullingProvider(int tilesPerSide, bool usePrecompute)
        {
            TilesPerSide = Mathf.Max(1, tilesPerSide);
            PrecomputeEnabled = usePrecompute;

            for (int i = 0; i < 6; i++)
            {
                Vector3 up = FaceAxes.FaceUp[i];
                _axisA[i] = new Vector3(up.y, up.z, up.x);
                _axisB[i] = Vector3.Cross(up, _axisA[i]);
            }

            _pre = new(TilesPerSide * TilesPerSide * 6);
        }

        public void RebuildPrecompute(
            float sphereRadius,
            Texture2D heightMap,
            float heightScale,
            float uniformGlobalScale,
            int tilesPerSide
        )
        {
            _sphereRadius = sphereRadius;
            _heightMap = heightMap;
            _heightScale = heightScale;
            _uniformScale = uniformGlobalScale;
            TilesPerSide = Mathf.Max(1, tilesPerSide);

            _pre = new(TilesPerSide * TilesPerSide * 6);

            if (!PrecomputeEnabled) return;

            float maxElev = (_heightMap != null ? (_heightScale * _uniformScale) : 0f) * 0.5f;
            float invTiles = 1f / TilesPerSide;            // avoid repeated division
            float tileSize = invTiles;
            float halfRange = _sphereRadius * (tileSize * 1.5f) + maxElev;
            Vector3 boundsSize = Vector3.one * (halfRange * 2f);

            for (int face = 0; face < 6; face++)
            {
                Vector3 up = FaceAxes.FaceUp[face];
                Vector3 a = _axisA[face];
                Vector3 b = _axisB[face];

                for (int y = 0; y < TilesPerSide; y++)
                {
                    // precompute center y in [0,1]: (y + 0.5)/tiles
                    float fy = (y + 0.5f) * invTiles;
                    // map to [-1,1]: 2*fy - 1 → as (fy + fy - 1) to avoid an extra mul by const
                    float sy = fy + fy - 1f;

                    for (int x = 0; x < TilesPerSide; x++)
                    {
                        var key = new TileKey((byte)face, (ushort)x, (ushort)y);

                        float fx = (x + 0.5f) * invTiles;
                        float sx = fx + fx - 1f;

                        // order operands for fewer temporaries: scalar first, then vector mul, then adds
                        Vector3 pCube = a * sx + b * sy + up;

                        Vector3 center = pCube.normalized * _sphereRadius;

                        _pre[key] = new PreTile
                        {
                            center = center,
                            bounds = new Bounds(center, boundsSize)
                        };
                    }
                }
            }
        }

        public IEnumerable<TileKey> GetVisibleKeys(Camera cam)
        {
            if (cam == null) yield break;

            GeometryUtility.CalculateFrustumPlanes(cam, _planes);

            if (PrecomputeEnabled)
            {
                foreach (var kv in _pre)
                {
                    if (GeometryUtility.TestPlanesAABB(_planes, kv.Value.bounds))
                        yield return kv.Key;
                }
                yield break;
            }

            // On-the-fly path (no precompute)
            float maxElev = (_heightMap != null ? (_heightScale * _uniformScale) : 0f) * 0.5f;
            float invTiles = 1f / TilesPerSide;
            float tileSize = invTiles;
            float halfRange = _sphereRadius * (tileSize * 1.5f) + maxElev;
            Vector3 boundsSize = Vector3.one * (halfRange * 2f);

            for (int face = 0; face < 6; face++)
            {
                Vector3 up = FaceAxes.FaceUp[face];
                Vector3 a = _axisA[face];
                Vector3 b = _axisB[face];

                for (int y = 0; y < TilesPerSide; y++)
                {
                    float fy = (y + 0.5f) * invTiles;
                    float sy = fy + fy - 1f;

                    for (int x = 0; x < TilesPerSide; x++)
                    {
                        float fx = (x + 0.5f) * invTiles;
                        float sx = fx + fx - 1f;

                        Vector3 pCube = a * sx + b * sy + up;

                        Vector3 center = pCube.normalized * _sphereRadius;
                        var bounds = new Bounds(center, boundsSize);

                        if (GeometryUtility.TestPlanesAABB(_planes, bounds))
                            yield return new TileKey((byte)face, (ushort)x, (ushort)y);
                    }
                }
            }
        }

        public void GetFaceAxes(byte face, out Vector3 localUp, out Vector3 axisA, out Vector3 axisB)
        {
            int i = Mathf.Clamp(face, 0, 5);
            localUp = FaceAxes.FaceUp[i];
            axisA = _axisA[i];
            axisB = _axisB[i];
        }
    }
}
