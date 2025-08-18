using UnityEngine;
using System.Collections.Generic;

public class SphereFace
{
    public Mesh mesh;
    private Transform faceParentTransform;
    private SphereTile[] tiles;
    private int tilesPerSide;
    private int tileMeshResolution;
    private Vector3 localup;
    private Vector3 axisA;
    private Vector3 axisB;

    private Material tileMaterial;

    private TileDeformer_GPU sharedGpuDeformer;

    public SphereFace(int tilesPerSide, int tileMeshResolution, Vector3 localup)
    {
        this.tilesPerSide = tilesPerSide;
        this.tileMeshResolution = tileMeshResolution;
        this.localup = localup;

        axisA = new Vector3(localup.y, localup.z, localup.x);
        axisB = Vector3.Cross(localup, axisA);

        tiles = new SphereTile[tilesPerSide * tilesPerSide];
    }

    public void InitializeTiles(Transform parentTransform, Material tileMaterial,
                                Texture2D globalHeightMapForFace, float globalHeightScaleForFace, float globalSphereRadiusForFace, float globalUniformGlobalScaleForFace,
                                TileDeformer_GPU gpuDeformerInstance = null)
    {
        this.faceParentTransform = parentTransform;
        this.tileMaterial = tileMaterial;
        this.sharedGpuDeformer = gpuDeformerInstance;

        string faceID = CleanVectorName(localup);

        var existingChildren = new List<GameObject>();
        for (int j = 0; j < faceParentTransform.childCount; j++)
        {
            GameObject child = faceParentTransform.GetChild(j).gameObject;
            if (child.name.StartsWith($"SphereTile_Face_{faceID}"))
                existingChildren.Add(child);
        }

        var requiredNames = new HashSet<string>();
        for (int y = 0; y < tilesPerSide; y++)
        {
            for (int x = 0; x < tilesPerSide; x++)
                requiredNames.Add($"SphereTile_Face_{faceID}_X{x}_Y{y}");
        }

        foreach (GameObject child in existingChildren)
        {
            if (!requiredNames.Contains(child.name))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(child);
                else
                    Object.Destroy(child);
#else
                Object.Destroy(child);
#endif
            }
        }

        for (int y = 0; y < tilesPerSide; y++)
        {
            for (int x = 0; x < tilesPerSide; x++)
            {
                int index = x + y * tilesPerSide;
                string tileName = $"SphereTile_Face_{faceID}_X{x}_Y{y}";

                GameObject tileGO = parentTransform.Find(tileName)?.gameObject;

                if (tileGO == null)
                {
                    tileGO = new GameObject(tileName);
                    tileGO.transform.SetParent(parentTransform);
                }

                MeshFilter meshFilter = tileGO.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = tileGO.AddComponent<MeshFilter>();

                MeshRenderer meshRenderer = tileGO.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = tileGO.AddComponent<MeshRenderer>();

                if (tileMaterial != null)
                    meshRenderer.sharedMaterial = tileMaterial;

                if (meshFilter.sharedMesh == null)
                    meshFilter.sharedMesh = new Mesh();

                TileDeformer_GPU tileDeformer = tileGO.GetComponent<TileDeformer_GPU>();
                if (tileDeformer == null)
                {
                    tileDeformer = tileGO.AddComponent<TileDeformer_GPU>();
                    if (sharedGpuDeformer != null)
                    {
                        tileDeformer.computeShader = sharedGpuDeformer.computeShader;
                    }
                    else
                    {
                        Debug.LogWarning($"TileDeformer_GPU on '{tileName}' could not be assigned a Compute Shader because the master deformer was null.");
                    }
                }

                tiles[index] = new SphereTile(
                    meshFilter.sharedMesh,
                    meshRenderer,
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
                    tileDeformer
                );
            }
        }
    }

    public void ConstructFaceMeshes(bool useGpuDeformation)
    {
        foreach (SphereTile tile in tiles)
        {
            tile?.ConstructTileMesh(useGpuDeformation);
        }
    }

    public void SetTileVisibility(bool visible)
    {
        foreach (SphereTile tile in tiles)
        {
            tile?.SetVisibility(visible);
        }
    }

    private string CleanVectorName(Vector3 v) => v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");
}