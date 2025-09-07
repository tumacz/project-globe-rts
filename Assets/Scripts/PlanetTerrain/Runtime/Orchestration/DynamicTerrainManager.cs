using GlobeRTS.PlanetTerrain.Runtime.Config;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;
using GlobeRTS.PlanetTerrain.Runtime.TerrainStreaming.Cache;
using GlobeRTS.PlanetTerrain.Runtime.TerrainStreaming.Creation;
using GlobeRTS.PlanetTerrain.Runtime.Update;
using GlobeRTS.PlanetTerrain.Runtime.VisibilityCulling;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.Orchestration
{
    /// <summary>
    /// Orchestrator: culling -> create/activate -> sweep -> enforce cache capacity.
    /// Put-guard; pooling; TileCreateConfig; precompute rebuild; priorities;
    /// MPB; asserts + HUD; FaceAxes safety; PlanetConfigSO; centralized Activate/Deactivate.
    /// </summary>
    public class DynamicTerrainManager : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private SphereShaper baseSphereShaper;
        [SerializeField] private Camera viewerCamera;

        [Header("Config (F11)")]
        [SerializeField] private bool useConfig = false;
        [SerializeField] private PlanetConfigSO planetConfig;

        [Header("Grid / Mesh")]
        [SerializeField] private int playModeChunkMeshResolution = 32;
        [SerializeField, Range(1, 16)] private int virtualTilesPerSide = 4;

        [Header("Height / Scale")]
        [SerializeField] private Texture2D heightMap;
        [SerializeField] private float heightScale = 1f;
        [SerializeField] private float uniformGlobalScale = 1f;

        [Header("GPU Deform")]
        public bool useGpuDeformationInPlayMode = true;
        [SerializeField] private ComputeShader playModeComputeShader;

        [Header("Streaming")]
        [SerializeField, Min(0)] private int maxCreatesPerFrame = 2;

        [Header("Culling")]
        [SerializeField] private bool precomputeCulling = true;
        [SerializeField] private bool onDemandCulling = true;
        [SerializeField, Range(0.01f, 1f)] private float minUpdateInterval = 0.05f;
        [SerializeField, Range(0.05f, 2f)] private float idleUpdateInterval = 0.5f;
        [SerializeField, Range(0f, 0.01f)] private float posEpsRelative = 0.001f;
        [SerializeField, Range(0f, 5f)] private float angEpsDeg = 0.25f;
        [SerializeField, Range(0f, 10f)] private float fovEpsDeg = 0.2f;

        [Header("Cache")]
        [SerializeField] private bool cacheAllGenerated = false;
        [SerializeField, Min(1)] private int maxCachedTiles = 512;

        [Header("Debug / HUD")]
        [SerializeField] private bool showRuntimeHud = true;

        [Header("Compatibility")]
        [SerializeField, Tooltip("OFF: local radius (legacy camera compatibility). ON: world radius (target behavior).")]
        private bool useWorldRadius = false;

        [Header("F6 Priority Settings")]
        [SerializeField] private bool usePriorityScheduling = true;
        [SerializeField, Range(0f, 1f)] private float faceWeight = 0.85f;
        [SerializeField, Range(0f, 1f)] private float intraFaceWeight = 0.15f;

        public float SphereRadius => baseSphereShaper != null ? baseSphereShaper.SphereRadius : 1f;
        public float SphereRadiusWorld => SphereRadius * transform.lossyScale.x;
        public float EffectiveRadius => useWorldRadius ? SphereRadiusWorld : SphereRadius;

        private TileCache _cache;
        private TileFactory _factory;
        private ICullingProvider _culling;
        private CameraChangeDetector _detector;
        private TerrainUpdateScheduler _scheduler;

        private int _createdThisFrame, _queuedThisFrame, _drainedThisFrame;
        private int _visStamp = 0;
        private static readonly ProfilerMarker s_VisUpdate = new("DTM.VisibilityUpdate");

        private void Awake()
        {
            if (useConfig && planetConfig != null)
                ApplyConfig(planetConfig);

            if (!Validate()) { enabled = false; return; }

            if (!ValidateFaceAxesMapping())
            {
                Debug.LogError("[DTM/F10] FaceAxes mapping mismatch. Disabling DynamicTerrainManager to prevent corrupt streaming.");
                enabled = false;
                return;
            }

            if (Mathf.Abs(transform.lossyScale.x - transform.lossyScale.y) > 1e-4f ||
                Mathf.Abs(transform.lossyScale.x - transform.lossyScale.z) > 1e-4f)
            {
                Debug.LogWarning("[DTM] Non-uniform scale detected on planet root. Consider using uniform scale for predictable culling/geometry.");
            }
            if (heightScale > 0f && heightMap == null)
            {
                Debug.LogWarning("[DTM] HeightScale > 0 but HeightMap is null. Terrain will be flat.");
            }

            _cache = new(maxCachedTiles, cacheAllGenerated, debugLogs: false);

            var mat = (useConfig && planetConfig != null && planetConfig.sphereMaterial != null)
                ? planetConfig.sphereMaterial
                : (baseSphereShaper != null ? baseSphereShaper.sphereMaterial : null);

            var cs = (useConfig && planetConfig != null)
                ? planetConfig.deformComputeShader
                : playModeComputeShader;

            _factory = new TileFactory(transform, mat, useGpuDeformationInPlayMode ? cs : null, _cache);

            _culling = new FrustumCullingProvider(virtualTilesPerSide, precomputeCulling);
            RebuildCullingPrecompute();
            _detector = new CameraChangeDetector(
                viewerCamera,
                () => EffectiveRadius,
                minUpdateInterval, idleUpdateInterval,
                posEpsRelative, angEpsDeg, fovEpsDeg
            );

            _scheduler = new TerrainUpdateScheduler();
        }

        private void Start()
        {
            _detector.ForceOnce();
            DoVisibilityUpdate();
        }

        private bool Validate()
        {
            if (baseSphereShaper == null) { Debug.LogError("DTM: SphereShaper missing"); return false; }
            if (viewerCamera == null) { Debug.LogError("DTM: Viewer Camera missing"); return false; }
            if (useGpuDeformationInPlayMode && playModeComputeShader == null &&
                (!useConfig || planetConfig == null || planetConfig.deformComputeShader == null))
            {
                Debug.LogWarning("DTM: GPU deform is ON but no ComputeShader provided (falling back to CPU deformation).");
            }
            if (virtualTilesPerSide < 1)
            { Debug.LogError("DTM: virtualTilesPerSide must be >= 1"); return false; }
            return true;
        }

        private void ApplyConfig(PlanetConfigSO cfg)
        {
            if (cfg.sphereMaterial != null && baseSphereShaper != null)
                baseSphereShaper.sphereMaterial = cfg.sphereMaterial;

            if (cfg.sphereRadius > 0f && baseSphereShaper != null)
                baseSphereShaper.SphereRadius = cfg.sphereRadius;

            playModeChunkMeshResolution = cfg.chunkMeshResolution;
            virtualTilesPerSide = cfg.virtualTilesPerSide;

            heightMap = cfg.heightMap;
            heightScale = cfg.heightScale;
            uniformGlobalScale = cfg.uniformGlobalScale;

            useGpuDeformationInPlayMode = cfg.useGpuDeformation;
            playModeComputeShader = cfg.deformComputeShader;

            maxCreatesPerFrame = cfg.maxCreatesPerFrame;
            cacheAllGenerated = cfg.cacheAllGenerated;
            maxCachedTiles = cfg.maxCachedTiles;

            precomputeCulling = cfg.precomputeCulling;
            onDemandCulling = cfg.onDemandCulling;
            minUpdateInterval = cfg.minUpdateInterval;
            idleUpdateInterval = cfg.idleUpdateInterval;
            posEpsRelative = cfg.posEpsRelative;
            angEpsDeg = cfg.angEpsDeg;
            fovEpsDeg = cfg.fovEpsDeg;

            usePriorityScheduling = cfg.usePriorityScheduling;
            faceWeight = cfg.faceWeight;
            intraFaceWeight = cfg.intraFaceWeight;

            useWorldRadius = cfg.useWorldRadius;
        }

        public void RequestCullingUpdate(bool immediate = false) { if (immediate) _detector.ForceOnce(); }

        public void RebuildCullingPrecompute()
        {
            if (_culling == null) return;
            _culling.RebuildPrecompute(EffectiveRadius, heightMap, heightScale, uniformGlobalScale, virtualTilesPerSide);

            _factory.ApplyGlobalMaterialParamsToAll(EffectiveRadius, heightScale, uniformGlobalScale, virtualTilesPerSide);
        }

        private void OnValidate()
        {
            if (useConfig && planetConfig != null)
                ApplyConfig(planetConfig);

            if (Application.isPlaying && _culling != null)
            {
                RebuildCullingPrecompute();
                _detector?.ForceOnce();
            }
        }

        [ContextMenu("Rebuild Culling (F5)")]
        private void Ctx_RebuildCulling()
        {
            RebuildCullingPrecompute();
#if UNITY_EDITOR
            Debug.Log("[DTM] Rebuilt culling precompute via context menu.");
#endif
        }

        private const float kFaceDotTolerance = 0.999f;

        /// <summary>Verifies that FaceAxes.FaceUp[face] matches FaceAxes.GetAxes(face, ...) used in factory/culling.</summary>
        private bool ValidateFaceAxesMapping()
        {
            for (int face = 0; face < 6; face++)
            {
                FaceAxes.GetAxes((byte)face, out var up, out var axisA, out var axisB);

                var expected = FaceAxes.FaceUp[face].normalized;
                var got = up.normalized;
                float dot = Vector3.Dot(expected, got);

                if (dot < kFaceDotTolerance)
                {
                    Debug.LogError($"[DTM/F10] Face #{face} normal mismatch. expected={expected} got={got} dot={dot:F6}");
                    return false;
                }

                float o1 = Mathf.Abs(Vector3.Dot(up, axisA));
                float o2 = Mathf.Abs(Vector3.Dot(up, axisB));
                float o3 = Mathf.Abs(Vector3.Dot(axisA, axisB));
                if (o1 > 1e-3f || o2 > 1e-3f || o3 > 1e-3f)
                {
                    Debug.LogWarning($"[DTM/F10] Axes near-orthogonality check shows small drift on face #{face}: " +
                                     $"|up·A|={o1:E2}, |up·B|={o2:E2}, |A·B|={o3:E2}");
                }
            }
            return true;
        }

        private void Update()
        {
            if (onDemandCulling && !_detector.ShouldUpdate())
                return;

            DoVisibilityUpdate();
        }

        private struct KeyWithPriority { public TileKey Key; public float Score; }

        private float ComputePriority(in TileKey key)
        {
            var camFwdLocal = transform.InverseTransformDirection(viewerCamera.transform.forward).normalized;
            var faceNormal = FaceAxes.FaceUp[key.face];
            float faceScore = Mathf.Clamp01((Vector3.Dot(camFwdLocal, faceNormal) + 1f) * 0.5f);

            float mid = virtualTilesPerSide * 0.5f;
            float cx = key.x + 0.5f;
            float cy = key.y + 0.5f;
            float dx = cx - mid;
            float dy = cy - mid;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float maxDist = Mathf.Max(1e-3f, Mathf.Sqrt(2f) * Mathf.Max(mid, 0.5f));
            float intraScore = 1f - Mathf.Clamp01(dist / maxDist);

            return faceScore * faceWeight + intraFaceWeight * intraScore;
        }

        private void DoVisibilityUpdate()
        {
            using (s_VisUpdate.Auto())
            {
                _visStamp++;

                _createdThisFrame = 0;
                _queuedThisFrame = 0;
                _drainedThisFrame = 0;
                _factory.ResetFrameStats();
                _cache.ResetFrameStats();

                var visibleKeys = _culling.GetVisibleKeys(viewerCamera);

                List<KeyWithPriority> ordered = null;
                if (usePriorityScheduling)
                {
                    int visCount = (visibleKeys is ICollection<TileKey> coll) ? coll.Count : 64;
                    ordered = new(visCount);

                    foreach (var k in visibleKeys)
                    {
                        float s = _cache.Contains(k) ? float.PositiveInfinity : ComputePriority(in k);
                        ordered.Add(new KeyWithPriority { Key = k, Score = s });
                    }
                    ordered.Sort((a, b) => b.Score.CompareTo(a.Score));
                }

                int created = 0;

                if (usePriorityScheduling)
                {
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var key = ordered[i].Key;

                        if (_cache.TryGet(key, out _))
                        {
                            _cache.Activate(key, Time.time, _visStamp);
                            continue;
                        }

                        if (created < maxCreatesPerFrame)
                        {
                            CreateAndActivate(key);
                            created++;
                            _createdThisFrame++;
                        }
                        else
                        {
                            _scheduler.EnqueueIfAbsent(key);
                            _queuedThisFrame++;
                        }
                    }
                }
                else
                {
                    foreach (var key in visibleKeys)
                    {
                        if (_cache.TryGet(key, out _))
                        {
                            _cache.Activate(key, Time.time, _visStamp);
                        }
                        else
                        {
                            if (created < maxCreatesPerFrame)
                            {
                                CreateAndActivate(key);
                                created++;
                                _createdThisFrame++;
                            }
                            else
                            {
                                _scheduler.EnqueueIfAbsent(key);
                                _queuedThisFrame++;
                            }
                        }
                    }
                }

                _scheduler.Drain(maxCreatesPerFrame - created, k =>
                {
                    CreateAndActivate(k);
                    _createdThisFrame++;
                    _drainedThisFrame++;
                    return true;
                });

                _cache.SweepDeactivateStale(_visStamp, Time.time);
                _cache.EnforceCapacity();
            }
        }

        private void CreateAndActivate(in TileKey key)
        {
            var cfg = new TileFactory.TileCreateConfig(
                key,
                playModeChunkMeshResolution,
                virtualTilesPerSide,
                EffectiveRadius, heightMap, heightScale, uniformGlobalScale,
                useGpuDeformationInPlayMode,
                _visStamp
            );

            var created = _factory.Create(in cfg);

            bool putOk = _cache.Put(created);
            if (!putOk)
            {
                if (created.deformer != null) Destroy(created.deformer);
                if (created.go != null) Destroy(created.go);
                return;
            }

            _cache.Activate(key, Time.time, _visStamp);
        }

        private void OnDisable()
        {
            _cache.DestroyAll();
            _scheduler.Clear();
        }

#if UNITY_EDITOR
        [SerializeField] private bool debugLogs = false;
        private void OnGUI()
        {
            if (!showRuntimeHud) return;

            float scaleX = transform.lossyScale.x;
            int active = 0, cached = 0;
            foreach (var p in _cache.Pairs)
            {
                cached++;
                if (p.Value.isActive) active++;
            }

            string radiusMode = useWorldRadius ? "WORLD" : "LOCAL";
            string cfgName = (useConfig && planetConfig != null) ? planetConfig.name : "<none>";
            GUI.Label(new Rect(10, 10, 1750, 22),
                $"Tiles: active={active} cached={cached} queued={_scheduler.Count} | " +
                $"Radius(local)={SphereRadius:F3}  Radius(world)={SphereRadiusWorld:F3}  scale={scaleX:F3}  using={radiusMode} | " +
                $"Create:{_createdThisFrame}  Drain:{_drainedThisFrame}  Enq:{_queuedThisFrame} | " +
                $"FromPool:{_factory.StatsRentedFromPoolThisFrame}  NewGO:{_factory.StatsNewCreatedThisFrame}  Pooled:{_cache.StatsPooledThisFrame} | " +
                $"Config={cfgName}");
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, EffectiveRadius);
        }
    }
}