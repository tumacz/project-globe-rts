using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.HeightData
{
    /// <summary>
    /// Fast bilinear sampler from a Texture2D raw buffer (CPU).
    /// Supports R8 and *32-bit* formats (RGBA32/ARGB32/BGRA32).
    /// For other formats or non-readable textures it falls back to GetPixelBilinear.
    /// Expects UVs as provided (no inversion here).
    /// </summary>
    public sealed class HeightSampler
    {
        private readonly Texture2D tex;
        private readonly int w, h;
        private readonly bool isR8, is32;
        private readonly NativeArray<byte> raw8;
        private readonly NativeArray<Color32> raw32;
        private readonly bool fallbackToGetPixelBilinear;

        public HeightSampler(Texture2D texture)
        {
            tex = texture;
            if (tex == null) { fallbackToGetPixelBilinear = true; return; }

            w = tex.width;
            h = tex.height;

            try
            {
                switch (tex.format)
                {
                    case TextureFormat.R8:
                        isR8 = true; is32 = false;
                        raw8 = tex.GetRawTextureData<byte>();
                        break;

                    case TextureFormat.RGBA32:
                    case TextureFormat.ARGB32:
                    case TextureFormat.BGRA32:
                        isR8 = false; is32 = true;
                        raw32 = tex.GetRawTextureData<Color32>();
                        break;

                    default:
                        fallbackToGetPixelBilinear = true;
                        break;
                }
            }
            catch
            {
                fallbackToGetPixelBilinear = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Sample01(float u, float v)
        {
            if (fallbackToGetPixelBilinear)
                return tex != null ? tex.GetPixelBilinear(u, v).r : 0f;

            u = Mathf.Clamp01(u) * (w - 1);
            v = Mathf.Clamp01(v) * (h - 1);

            int x = (int)u, y = (int)v;
            int x1 = (x + 1 < w) ? x + 1 : x;
            int y1 = (y + 1 < h) ? y + 1 : y;

            float tx = u - x, ty = v - y;

            int i00 = x + y * w;
            int i10 = x1 + y * w;
            int i01 = x + y1 * w;
            int i11 = x1 + y1 * w;

            float a00, a10, a01, a11;
            if (isR8)
            {
                a00 = raw8[i00]; a10 = raw8[i10]; a01 = raw8[i01]; a11 = raw8[i11];
            }
            else
            {
                a00 = raw32[i00].r; a10 = raw32[i10].r; a01 = raw32[i01].r; a11 = raw32[i11].r;
            }

            float a0 = a00 + (a10 - a00) * tx;
            float a1 = a01 + (a11 - a01) * tx;
            return (a0 + (a1 - a0) * ty) * (1f / 255f);
        }
    }
}