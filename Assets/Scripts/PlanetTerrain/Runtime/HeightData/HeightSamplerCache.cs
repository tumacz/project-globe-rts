using System.Collections.Generic;
using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.HeightData
{
    /// <summary>Simple per-Texture2D sampler cache (avoid reconstructing every time).</summary>
    public static class HeightSamplerCache
    {
        private static readonly Dictionary<Texture2D, HeightSampler> _cache = new();

        public static HeightSampler Get(Texture2D tex)
        {
            if (tex == null) return null;
            if (_cache.TryGetValue(tex, out var s)) return s;
            s = new HeightSampler(tex);
            _cache[tex] = s;
            return s;
        }

        public static void Clear() => _cache.Clear();
    }
}