using UnityEngine;
using System.Collections.Generic;

public class DynamicTerrainManager : MonoBehaviour
{
    [Header("Core SphereShaper Reference")]
    [Tooltip("Assign your main SphereShaper (which currently generates the full sphere) here. We will use its settings.")]
    [SerializeField] private SphereShaper baseSphereShaper;

    [Header("Viewer Settings")]
    [Tooltip("The Camera that represents the viewer.")]
    [SerializeField] private Camera viewerCamera;

    [Tooltip("The resolution to use for the mesh of visible chunks.")]
    [SerializeField] private int playModeChunkMeshResolution = 32;

    [Tooltip("The number of virtual tiles per side for each of the 6 faces. Determines how many sub-chunks each main face is divided into.")]
    [SerializeField, Range(1, 16)] private int virtualTilesPerSide = 4;

    [Header("Data Textures (from DynamicMeshDeformer or direct)")]
    [SerializeField] private Texture2D heightMap;
    [SerializeField] private float heightScale = 1f;
    [SerializeField] private float uniformGlobalScale = 1f;

    [Header("Deformation Method (Play Mode)")]
    [Tooltip("If true, GPU Compute Shader will be used for mesh deformation in Play Mode. Requires a Compute Shader assigned.")]
    public bool useGpuDeformationInPlayMode = true;
    [SerializeField] private ComputeShader playModeComputeShader;

    public float SphereRadius
    {
        get
        {
            if (baseSphereShaper != null)
            {
                return baseSphereShaper.SphereRadius;
            }
            Debug.LogError("DynamicTerrainManager: baseSphereShaper is null! Returning default radius 1f.");
            return 1f;
        }
    }

    private Dictionary<string, GameObject> activeChunks = new Dictionary<string, GameObject>();

    private Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

    private Plane[] frustumPlanes = new Plane[6];

    private void Awake()
    {
        if (baseSphereShaper == null)
        {
            Debug.LogError("DynamicTerrainManager: baseSphereShaper is not assigned! Please assign the main SphereShaper.");
            enabled = false;
            return;
        }

        if (viewerCamera == null)
        {
            Debug.LogError("DynamicTerrainManager: Viewer Camera is not assigned! Please assign the Camera.");
            enabled = false;
            return;
        }

        if (useGpuDeformationInPlayMode && playModeComputeShader == null)
        {
            Debug.LogError("DynamicTerrainManager: useGpuDeformationInPlayMode is true but Play Mode Compute Shader is not assigned!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        GeometryUtility.CalculateFrustumPlanes(viewerCamera, frustumPlanes);
        UpdateVisibleChunks();
    }

    private void UpdateVisibleChunks()
    {
        HashSet<string> chunksToDeactivate = new HashSet<string>(activeChunks.Keys);

        for (int faceIndex = 0; faceIndex < directions.Length; faceIndex++)
        {
            Vector3 localUp = directions[faceIndex];
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);

            for (int y = 0; y < virtualTilesPerSide; y++)
            {
                for (int x = 0; x < virtualTilesPerSide; x++)
                {
                    string chunkID = $"Chunk_Face_{CleanVectorName(localUp)}_X{x}_Y{y}";

                    float tileStartX = (float)x / virtualTilesPerSide;
                    float tileStartY = (float)y / virtualTilesPerSide;
                    float tileSize = 1f / virtualTilesPerSide;

                    Vector2 centerPercent = new Vector2(
                        tileStartX + tileSize * 0.5f,
                        tileStartY + tileSize * 0.5f
                    );

                    Vector3 pointOnUnitCube = localUp +
                                              (axisA * (centerPercent.x - 0.5f) * 2f) +
                                              (axisB * (centerPercent.y - 0.5f) * 2f);

                    Vector3 chunkWorldCenter = pointOnUnitCube.normalized * SphereRadius;

                    float maxPossibleElevation = (this.heightMap != null ? (this.heightScale * this.uniformGlobalScale) : 0f) / 2f;
                    float chunkApproxRadius = SphereRadius * (tileSize * 1.5f) + maxPossibleElevation;
                    Bounds chunkBounds = new Bounds(chunkWorldCenter, Vector3.one * chunkApproxRadius * 2);

                    bool isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, chunkBounds);

                    if (isVisible)
                    {
                        if (!activeChunks.ContainsKey(chunkID))
                        {
                            GameObject chunkGO = CreateAndGenerateChunk(
                                chunkID,
                                this.transform,
                                baseSphereShaper.sphereMaterial,
                                playModeChunkMeshResolution,
                                localUp, axisA, axisB, x, y, virtualTilesPerSide,
                                this.heightMap,
                                this.heightScale,
                                this.SphereRadius,
                                this.uniformGlobalScale,
                                useGpuDeformationInPlayMode,
                                playModeComputeShader
                            );
                            activeChunks.Add(chunkID, chunkGO);
                        }
                        chunksToDeactivate.Remove(chunkID);
                    }
                }
            }
        }

        foreach (string id in chunksToDeactivate)
        {
            if (activeChunks.TryGetValue(id, out GameObject chunkGO))
            {
                if (chunkGO != null)
                {
                    TileDeformer_GPU deformer = chunkGO.GetComponent<TileDeformer_GPU>();
                    if (deformer != null)
                    {
                        Object.Destroy(deformer);
                    }
                    Object.Destroy(chunkGO);
                }
                activeChunks.Remove(id);
            }
        }
    }

    private GameObject CreateAndGenerateChunk(string id, Transform parentTransform, Material material, int resolution,
                                              Vector3 localUp, Vector3 axisA, Vector3 axisB, int tileGridX, int tileGridY, int tilesPerSide,
                                              Texture2D globalHeightMapForTile, float globalHeightScaleForTile, float sphereRadiusForTile, float uniformGlobalScaleForTile,
                                              bool useGpu, ComputeShader cs)
    {
        GameObject chunkGO = new GameObject(id);
        chunkGO.transform.SetParent(parentTransform);

        MeshFilter meshFilter = chunkGO.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkGO.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        Mesh mesh = new Mesh();
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        TileDeformer_GPU deformer = null;
        if (useGpu)
        {
            deformer = chunkGO.AddComponent<TileDeformer_GPU>();
            deformer.computeShader = cs;
        }

        SphereTile tempTile = new SphereTile(
            mesh,
            meshRenderer,
            material,
            resolution,
            localUp, axisA, axisB,
            tileGridX, tileGridY, tilesPerSide,
            globalHeightMapForTile, globalHeightScaleForTile, sphereRadiusForTile, uniformGlobalScaleForTile,
            deformer
        );
        tempTile.ConstructTileMesh(useGpu);

        return chunkGO;
    }

    private string CleanVectorName(Vector3 v) => v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");

    private void OnDisable()
    {
        foreach (var entry in activeChunks)
        {
            if (entry.Value != null)
            {
                TileDeformer_GPU deformer = entry.Value.GetComponent<TileDeformer_GPU>();
                if (deformer != null)
                {
                    Object.Destroy(deformer);
                }
                Object.Destroy(entry.Value);
            }
        }
        activeChunks.Clear();
    }
}