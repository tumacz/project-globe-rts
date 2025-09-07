using UnityEngine;
using UnityEngine.Rendering;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;
using GlobeRTS.PlanetTerrain.Runtime.TerrainStreaming.Cache;

namespace GlobeRTS.PlanetTerrain.Runtime.TerrainStreaming.Creation
{
    /// <summary>
    /// Creates a tile (GO + Mesh + optional deformer). ALWAYS returns an inactive GO.
    /// Reuses GOs from the pool (TileCache). Uses one shared MPB for per-tile shader params.
    /// </summary>
    public sealed class TileFactory
    {
        /// <summary>Immutable configuration used to create a single tile.</summary>
        public readonly struct TileCreateConfig
        {
            public readonly TileKey Key;
            public readonly int MeshResolution;
            public readonly int TilesPerSide;
            public readonly float SphereRadius;
            public readonly Texture2D HeightMap;
            public readonly float HeightScale;
            public readonly float UniformScale;
            public readonly bool UseGpuDeformation;
            public readonly int CurrentVisStamp;

            public TileCreateConfig(
                in TileKey key,
                int meshResolution,
                int tilesPerSide,
                float sphereRadius,
                Texture2D heightMap,
                float heightScale,
                float uniformScale,
                bool useGpuDeformation,
                int currentVisStamp
            )
            {
                Key = key;
                MeshResolution = meshResolution;
                TilesPerSide = tilesPerSide;
                SphereRadius = sphereRadius;
                HeightMap = heightMap;
                HeightScale = heightScale;
                UniformScale = uniformScale;
                UseGpuDeformation = useGpuDeformation;
                CurrentVisStamp = currentVisStamp;
            }
        }

        private readonly Transform _parent;
        private readonly Material _material;
        private readonly ComputeShader _deformCS;
        private readonly TileCache _cache;

        public int StatsRentedFromPoolThisFrame { get; private set; }
        public int StatsNewCreatedThisFrame { get; private set; }
        public void ResetFrameStats() { StatsRentedFromPoolThisFrame = 0; StatsNewCreatedThisFrame = 0; }

        private readonly MaterialPropertyBlock _mpb = new();

        private static readonly int ID_P_SphereRadius = Shader.PropertyToID("_PT_SphereRadius");
        private static readonly int ID_P_HeightScale = Shader.PropertyToID("_PT_HeightScale");
        private static readonly int ID_P_UniformScale = Shader.PropertyToID("_PT_UniformScale");
        private static readonly int ID_P_TilesPerSide = Shader.PropertyToID("_PT_TilesPerSide");
        private static readonly int ID_P_TileXY = Shader.PropertyToID("_PT_TileXY");
        private static readonly int ID_P_FaceIndex = Shader.PropertyToID("_PT_FaceIndex");

        public TileFactory(Transform parent, Material material, ComputeShader deformComputeShader = null, TileCache cache = null)
        {
            _parent = parent;
            _material = material;
            _deformCS = deformComputeShader;
            _cache = cache;
        }

        private void ApplyPerTileMaterialParams(MeshRenderer mr, in TileCreateConfig cfg)
        {
            _mpb.Clear();
            _mpb.SetFloat(ID_P_SphereRadius, cfg.SphereRadius);
            _mpb.SetFloat(ID_P_HeightScale, cfg.HeightScale);
            _mpb.SetFloat(ID_P_UniformScale, cfg.UniformScale);
            _mpb.SetFloat(ID_P_TilesPerSide, cfg.TilesPerSide);
            _mpb.SetVector(ID_P_TileXY, new Vector4(cfg.Key.x, cfg.Key.y, 0f, 0f));
            _mpb.SetFloat(ID_P_FaceIndex, cfg.Key.face);

            if (mr != null) mr.SetPropertyBlock(_mpb);
        }

        /// <summary>Re-applies current global params to ALL tiles in cache (e.g., after scale/height change).</summary>
        public void ApplyGlobalMaterialParamsToAll(float sphereRadius, float heightScale, float uniformScale, int tilesPerSide)
        {
            if (_cache == null) return;

            foreach (var pair in _cache.Pairs)
            {
                var ck = pair.Key;
                var cc = pair.Value;

                // Use classic null checks for UnityEngine.Object
                if (cc?.go == null || cc.mesh == null) continue;

                // avoid GetComponent allocation
                if (!(cc.go.TryGetComponent(out MeshRenderer mr) && mr != null)) continue;

                _mpb.Clear();
                _mpb.SetFloat(ID_P_SphereRadius, sphereRadius);
                _mpb.SetFloat(ID_P_HeightScale, heightScale);
                _mpb.SetFloat(ID_P_UniformScale, uniformScale);
                _mpb.SetFloat(ID_P_TilesPerSide, tilesPerSide);
                _mpb.SetVector(ID_P_TileXY, new Vector4(ck.x, ck.y, 0f, 0f));
                _mpb.SetFloat(ID_P_FaceIndex, ck.face);

                mr.SetPropertyBlock(_mpb);
            }
        }

        public CachedChunk Create(in TileCreateConfig cfg)
        {
            GameObject go = _cache != null ? _cache.RentFromPool() : null;
            MeshFilter mf;
            MeshRenderer mr;
            TileDeformer_GPU deformer;
            Mesh mesh;

            if (go != null)
            {
                StatsRentedFromPoolThisFrame++;
                go.name = $"Chunk_{cfg.Key}";
                go.transform.SetParent(_parent, true);
                go.SetActive(false);

                if (!go.TryGetComponent(out mf)) mf = go.AddComponent<MeshFilter>();
                if (!go.TryGetComponent(out mr)) mr = go.AddComponent<MeshRenderer>();
                go.TryGetComponent(out deformer);

                mesh = mf.sharedMesh;
                if (mesh == null) mesh = new Mesh();
                mesh.MarkDynamic();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = _material;

                if (cfg.UseGpuDeformation && _deformCS != null)
                {
                    if (deformer == null) deformer = go.AddComponent<TileDeformer_GPU>();
                    deformer.computeShader = _deformCS;
                    deformer.enabled = true;
                }
                else if (deformer != null)
                {
                    deformer.enabled = false;
                }
            }
            else
            {
                StatsNewCreatedThisFrame++;
                go = new GameObject($"Chunk_{cfg.Key}");
                go.transform.SetParent(_parent, true);
                go.SetActive(false);

                mf = go.AddComponent<MeshFilter>();
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _material;

                mesh = new Mesh();
                mesh.MarkDynamic();
                mf.sharedMesh = mesh;

                deformer = null;
                if (cfg.UseGpuDeformation && _deformCS != null)
                {
                    deformer = go.AddComponent<TileDeformer_GPU>();
                    deformer.computeShader = _deformCS;
                    deformer.enabled = true;
                }
            }

            FaceAxes.GetAxes(cfg.Key.face, out var localUp, out var axisA, out var axisB);

            var tile = new SphereTile(
                mesh,
                mr,
                _material,
                cfg.MeshResolution,
                localUp, axisA, axisB,
                cfg.Key.x, cfg.Key.y, cfg.TilesPerSide,
                cfg.HeightMap, cfg.HeightScale,
                cfg.SphereRadius, cfg.UniformScale,
                deformer
            );
            tile.ConstructTileMesh(cfg.UseGpuDeformation);

            ApplyPerTileMaterialParams(mr, in cfg);

            long sizeBytes = EstimateMeshBytes(mesh);

            return new CachedChunk
            {
                key = cfg.Key,
                go = go,
                mesh = mesh,
                deformer = deformer,
                isActive = false,
                lastAccessTime = Time.time,
                sizeBytes = sizeBytes,
                lastVisibleStamp = cfg.CurrentVisStamp
            };
        }

        public CachedChunk Create(
            in TileKey key,
            int meshResolution,
            int tilesPerSide,
            float sphereRadius, Texture2D heightMap, float heightScale, float uniformScale,
            bool useGpuDeformation,
            int currentVisStamp
        )
        {
            var cfg = new TileCreateConfig(
                key, meshResolution, tilesPerSide,
                sphereRadius, heightMap, heightScale, uniformScale,
                useGpuDeformation, currentVisStamp
            );
            return Create(in cfg);
        }

        /// <summary>Rough mesh memory footprint estimate in bytes (vertices + indices), no array allocations.</summary>
        private static long EstimateMeshBytes(Mesh m)
        {
            if (m == null) return 0;
            int v = m.vertexCount;

            bool hasTangents = m.HasVertexAttribute(VertexAttribute.Tangent);
            int perVertex = 12 + 12 + 8 + (hasTangents ? 16 : 0); // pos + normal + uv + tangents
            long vertsBytes = (long)v * perVertex;

            long indexBytes = 0;
            int subCount = m.subMeshCount;
            if (subCount > 0)
            {
                for (int i = 0; i < subCount; i++)
                {
                    long ic = (long)m.GetIndexCount(i);
                    int indexSize = (m.indexFormat == IndexFormat.UInt16) ? 2 : 4;
                    indexBytes += ic * indexSize;
                }
            }
            else
            {
                int[] tris = m.triangles;
                int indexCount = tris != null ? tris.Length : 0;
                int indexSize = (m.indexFormat == IndexFormat.UInt16) ? 2 : 4;
                indexBytes = (long)indexCount * indexSize;
            }

            return vertsBytes + indexBytes;
        }
    }
}