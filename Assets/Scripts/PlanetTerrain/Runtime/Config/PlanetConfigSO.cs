using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.Config
{
    /// <summary>
    /// F11: ScriptableObject that stores a planet/streaming profile.
    /// Lets you carry a consistent set of settings across scenes without touching the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "PlanetConfig", menuName = "GlobeRTS/Planet Config", order = 10)]
    public sealed class PlanetConfigSO : ScriptableObject
    {
        [Header("Core")]
        [Tooltip("Material used by sphere tiles.")]
        public Material sphereMaterial;

        [Tooltip("Default sphere radius (local). If 0, take the value from SphereShaper.")]
        public float sphereRadius = 0f;

        [Header("Grid / Mesh")]
        [Tooltip("Per-chunk mesh resolution (number of vertices per side).")]
        public int chunkMeshResolution = 64;

        [Range(1, 16)]
        [Tooltip("Virtual number of tiles per face side (logical tiling density).")]
        public int virtualTilesPerSide = 16;

        [Header("Height / Scale")]
        [Tooltip("Global heightmap texture used for elevation sampling.")]
        public Texture2D heightMap;

        [Tooltip("Height deformation scale (world units relative to sphere radius).")]
        public float heightScale = 0.02f;

        [Tooltip("Uniform global scale applied to the whole planet.")]
        public float uniformGlobalScale = 1f;

        [Header("GPU Deform")]
        [Tooltip("Enable GPU-based deformation (compute shader path).")]
        public bool useGpuDeformation = false;

        [Tooltip("Compute shader used for GPU deformation.")]
        public ComputeShader deformComputeShader;

        [Header("Streaming")]
        [Min(0)]
        [Tooltip("Max number of chunk creations per frame.")]
        public int maxCreatesPerFrame = 10;

        [Tooltip("Cache all generated chunks (may increase memory usage).")]
        public bool cacheAllGenerated = true;

        [Min(1)]
        [Tooltip("Maximum number of cached tiles when caching is limited.")]
        public int maxCachedTiles = 512;

        [Header("Culling / Updates")]
        [Tooltip("Precompute visibility where possible before spawning chunks.")]
        public bool precomputeCulling = true;

        [Tooltip("Evaluate visibility on demand during runtime updates.")]
        public bool onDemandCulling = true;

        [Range(0.01f, 1f)]
        [Tooltip("Minimum interval (seconds) between update passes under load.")]
        public float minUpdateInterval = 0.05f;

        [Range(0.05f, 2f)]
        [Tooltip("Update interval (seconds) when the system is idle.")]
        public float idleUpdateInterval = 0.5f;

        [Range(0f, 0.01f)]
        [Tooltip("Position epsilon relative to radius for change detection.")]
        public float posEpsRelative = 0.001f;

        [Range(0f, 5f)]
        [Tooltip("Angular epsilon (degrees) for camera rotation change detection.")]
        public float angEpsDeg = 0.25f;

        [Range(0f, 10f)]
        [Tooltip("Field-of-view epsilon (degrees) for FOV change detection.")]
        public float fovEpsDeg = 0.2f;

        [Header("Priority")]
        [Tooltip("Enable priority-based scheduling of chunk creation/updates.")]
        public bool usePriorityScheduling = true;

        [Range(0f, 1f)]
        [Tooltip("Weight for inter-face (which cube face) prioritization.")]
        public float faceWeight = 0.85f;

        [Range(0f, 1f)]
        [Tooltip("Weight for intra-face (within a face) prioritization.")]
        public float intraFaceWeight = 0.15f;

        [Header("Compatibility")]
        [Tooltip("OFF: use local radius (legacy camera compatibility). ON: use world radius (target behavior).")]
        public bool useWorldRadius = false;
    }
}