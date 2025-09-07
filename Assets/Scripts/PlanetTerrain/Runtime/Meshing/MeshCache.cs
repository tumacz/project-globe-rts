using System.Collections.Generic;
using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.Meshing
{
    public static class MeshCache
    {
        private static readonly Dictionary<int, int[]> _triangles = new();
        private static readonly Dictionary<int, Vector2[]> _percents = new();

        /// <summary>
        /// Returns the triangle index buffer for an r x r grid (identical topology for any tile with the same resolution).
        /// </summary>
        public static int[] GetTriangles(int res)
        {
            if (_triangles.TryGetValue(res, out var tris))
                return tris;

            int quads = (res - 1) * (res - 1);
            tris = new int[quads * 6];
            int ti = 0;

            for (int y = 0; y < res - 1; y++)
            {
                int row = y * res;
                for (int x = 0; x < res - 1; x++)
                {
                    int i = row + x;
                    // tri 0
                    tris[ti++] = i;
                    tris[ti++] = i + res + 1;
                    tris[ti++] = i + res;
                    // tri 1
                    tris[ti++] = i;
                    tris[ti++] = i + 1;
                    tris[ti++] = i + res + 1;
                }
            }

            _triangles[res] = tris;
            return tris;
        }

        /// <summary>
        /// Returns an array of UV-like percents ([0..1]x[0..1]) for all vertices of an r x r grid,
        /// indexed as i = x + y * res.
        /// </summary>
        public static Vector2[] GetPercents(int res)
        {
            if (_percents.TryGetValue(res, out var p))
                return p;

            p = new Vector2[res * res];
            float inv = 1f / (res - 1);

            int i = 0;
            for (int y = 0; y < res; y++)
            {
                float py = y * inv;
                for (int x = 0; x < res; x++)
                {
                    p[i++] = new Vector2(x * inv, py);
                }
            }

            _percents[res] = p;
            return p;
        }
    }
}