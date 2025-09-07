using UnityEngine;
using System.Collections.Generic;

public class SphereFace
{
    public Mesh mesh;
    private Transform faceParentTransform;
    private readonly SphereTile[] tiles;
    private readonly int tilesPerSide;
    private readonly int tileMeshResolution;
    private Vector3 localup;
    private Vector3 axisA;
    private Vector3 axisB;

    private Material tileMaterial;
    private TileDeformer_GPU sharedGpuDeformer;

    private static bool sWarnedNoMasterForGpu;

    public SphereFace(int tilesPerSide, int tileMeshResolution, Vector3 localup)
    {
        this.tilesPerSide = tilesPerSide;
        this.tileMeshResolution = tileMeshResolution;
        this.localup = localup;

        axisA = new Vector3(localup.y, localup.z, localup.x);
        axisB = Vector3.Cross(localup, axisA);

        tiles = new SphereTile[tilesPerSide * tilesPerSide];
    }

    public void InitializeTiles(
        Transform parentTransform,
        Material tileMaterial,
        Texture2D globalHeightMapForFace,
        float globalHeightScaleForFace,
        float globalSphereRadiusForFace,
        float globalUniformGlobalScaleForFace,
        TileDeformer_GPU gpuDeformerInstance = null)
    {
        this.faceParentTransform = parentTransform;
        this.tileMaterial = tileMaterial;
        this.sharedGpuDeformer = gpuDeformerInstance;

        if (faceParentTransform == null)
        {
            Debug.LogError("[SphereFace] parentTransform is null - InitializeTiles aborted.");
            return;
        }

        string faceID = CleanVectorName(localup);

        // Collect existing children matching face prefix
        var existingChildren = new List<GameObject>();
        for (int j = 0; j < faceParentTransform.childCount; j++)
        {
            GameObject child = faceParentTransform.GetChild(j).gameObject;
            if (child.name.StartsWith($"SphereTile_Face_{faceID}"))
                existingChildren.Add(child);
        }

        // Prepare required names for this face
        var requiredNames = new HashSet<string>();
        for (int y = 0; y < tilesPerSide; y++)
            for (int x = 0; x < tilesPerSide; x++)
                requiredNames.Add($"SphereTile_Face_{faceID}_X{x}_Y{y}");

        // Remove stale children
        foreach (GameObject child in existingChildren)
        {
            if (!requiredNames.Contains(child.name))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Object.DestroyImmediate(child);
                else Object.Destroy(child);
#else
                Object.Destroy(child);
#endif
            }
        }

        bool gpuAvailable = (sharedGpuDeformer != null && sharedGpuDeformer.computeShader != null);
        if (!gpuAvailable && !sWarnedNoMasterForGpu)
        {
            Debug.LogWarning("[SphereFace] GPU deformation unavailable: master deformer is null or has no assigned ComputeShader. " +
                             "Skipped adding TileDeformer_GPU to tiles (this warning will only appear once).");
            sWarnedNoMasterForGpu = true;
        }

        // Create or reuse tiles
        for (int y = 0; y < tilesPerSide; y++)
        {
            for (int x = 0; x < tilesPerSide; x++)
            {
                int index = x + y * tilesPerSide;
                string tileName = $"SphereTile_Face_{faceID}_X{x}_Y{y}";

                Transform found = faceParentTransform.Find(tileName);
                GameObject tileGO = (found != null) ? found.gameObject : new GameObject(tileName);
                if (found == null)
                    tileGO.transform.SetParent(faceParentTransform, false);

                // MeshFilter (use TryGetComponent to avoid GC alloc when missing)
                if (!tileGO.TryGetComponent(out MeshFilter mf))
                    mf = tileGO.AddComponent<MeshFilter>();
                if (mf.sharedMesh == null)
                    mf.sharedMesh = new Mesh();

                // MeshRenderer (use TryGetComponent to avoid GC alloc when missing)
                if (!tileGO.TryGetComponent(out MeshRenderer mr))
                    mr = tileGO.AddComponent<MeshRenderer>();
                if (mr == null)
                {
                    Debug.LogError($"[SphereFace] Failed to add MeshRenderer to '{tileName}'. Skipping tile.");
                    continue;
                }
                if (tileMaterial != null)
                    mr.sharedMaterial = tileMaterial;

                // Optional GPU deformer
                TileDeformer_GPU tileDeformer = null;
                if (gpuAvailable)
                {
                    if (!tileGO.TryGetComponent(out tileDeformer))
                        tileDeformer = tileGO.AddComponent<TileDeformer_GPU>();
                    tileDeformer.computeShader = sharedGpuDeformer.computeShader;
                }
                else
                {
                    // If present, remove (editor-safe)
                    if (tileGO.TryGetComponent(out tileDeformer) && tileDeformer != null)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying) Object.DestroyImmediate(tileDeformer);
                        else Object.Destroy(tileDeformer);
#else
                        Object.Destroy(tileDeformer);
#endif
                        tileDeformer = null;
                    }
                }

                tiles[index] = new SphereTile(
                    mf.sharedMesh,
                    mr,
                    tileMaterial,
                    tileMeshResolution,
                    localup,
                    axisA,
                    axisB,
                    x, y, tilesPerSide,
                    globalHeightMapForFace,
                    globalHeightScaleForFace,
                    globalSphereRadiusForFace,
                    globalUniformGlobalScaleForFace,
                    gpuAvailable ? tileDeformer : null
                );
            }
        }
    }

    public void ConstructFaceMeshes(bool useGpuDeformation)
    {
        if (tiles == null) return;
        foreach (SphereTile tile in tiles)
            tile?.ConstructTileMesh(useGpuDeformation);
    }

    public void SetTileVisibility(bool visible)
    {
        if (tiles == null) return;
        foreach (SphereTile tile in tiles)
            tile?.SetVisibility(visible);
    }

    private string CleanVectorName(Vector3 v) =>
        v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");
}