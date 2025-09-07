using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.HeightData
{
    public interface IHeightSampler
    {
        /// <summary> Returns elevation in the [0..1] range for given UV coordinates. </summary>
        float Sample01(float u, float v);

        /// <summary> Returns elevation offset/scaled in world units. </summary>
        float SampleElevationWorld(float u, float v, float heightScale, float uniformScale);
    }

    /// <summary>
    /// Simple Texture2D-based sampler using Texture2D.GetPixelBilinear.
    /// Expects UVs as provided (no inversion here) — keep any flips at the call site.
    /// </summary>
    public sealed class TextureHeightSampler : IHeightSampler
    {
        private readonly Texture2D _tex;

        public TextureHeightSampler(Texture2D tex) { _tex = tex; }

        public float Sample01(float u, float v)
        {
            if (_tex == null) return 0.5f;
            return _tex.GetPixelBilinear(u, v).r;
        }

        public float SampleElevationWorld(float u, float v, float heightScale, float uniformScale)
        {
            float h = Sample01(u, v);              // 0..1
            float elev = (h - 0.5f) * heightScale; // -h..h
            return elev * uniformScale;
        }
    }
}