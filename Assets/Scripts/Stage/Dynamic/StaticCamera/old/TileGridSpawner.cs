using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TileGridSpawner : MonoBehaviour
{
    public GameObject tilePrefab;
    [Range(1, 100)] public int gridSizeX = 10;
    [Range(1, 100)] public int gridSizeY = 10;
    public float tileSpacing = 1.0f;

    [Header("Global Deformation Settings")]
    public bool globalUseSphericalDeformation = true;
    public Vector3 globalSphereCenter = Vector3.zero;
    [Range(1.0f, 20.0f)] public float globalSphereDistanceFromGrid = 20.0f;
    [Range(0.1f, 1.0f)] public float tileHeightScale = 1.0f;

    [Header("Tile Mesh Settings")]
    [Range(2, 255)]
    public int tileSubdivisions = 20;

    [Header("Heightmap Settings")]
    public Texture2D globalHeightMap;

#if UNITY_EDITOR
    public static HashSet<GameObject> _pendingDestroyEditor = new HashSet<GameObject>();
#endif

    private void Awake()
    {
        if (Application.isPlaying)
        {
            SpawnTiles();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && gameObject.activeInHierarchy)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;

                SafeEditorCleanup();
                SpawnTiles();
                UpdateDeformationParameters();
            };
        }
    }

    public void SafeEditorCleanup()
    {
        List<GameObject> childrenToDestroy = new List<GameObject>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name.StartsWith("Tile_"))
            {
                childrenToDestroy.Add(child);
            }
        }

        foreach (GameObject child in childrenToDestroy)
        {
            if (child != null)
            {
                _pendingDestroyEditor.Add(child);
            }
        }
    }
#endif

    public void SpawnTiles()
    {
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            childrenToDestroy.Add(child.gameObject);
        }
        foreach (GameObject child in childrenToDestroy)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(child);
            else
                _pendingDestroyEditor.Add(child);
#else
            Destroy(child);
#endif
        }

        Vector3 gridCenterGlobal = transform.position;
        globalSphereCenter = gridCenterGlobal + new Vector3(0, 0, globalSphereDistanceFromGrid);

        float centerX = (gridSizeX - 1) / 2f;
        float centerY = (gridSizeY - 1) / 2f;

        for (int y = 0; y < gridSizeY; y++)
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                Vector3 spawnPos = new Vector3(
                    (x - centerX) * tileSpacing,
                    (y - centerY) * tileSpacing,
                    0
                );

                GameObject tile = Instantiate(tilePrefab, spawnPos, Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";

                Rect tileUvRect = new Rect(
                    (float)x / gridSizeX,
                    (float)y / gridSizeY,
                    1f / gridSizeX,
                    1f / gridSizeY
                );

                var deformer = tile.GetComponent<HeightmapMeshDeformer>();
                if (deformer != null)
                {
                    deformer.subdivisions = tileSubdivisions;
                    deformer.heightScale = tileHeightScale;
                    deformer.globalSphereCenter = globalSphereCenter;
                    deformer.globalSphereDistanceFromGrid = globalSphereDistanceFromGrid;
                    deformer.useSphericalDeformation = globalUseSphericalDeformation;
                }

                var tileDeformerConfigurator = tile.GetComponent<TileDeformerConfigurator>();
                if (tileDeformerConfigurator != null)
                {
                    tileDeformerConfigurator.Setup(this.globalHeightMap, tileUvRect);
                }
            }
        }
    }

    public void UpdateDeformationParameters()
    {
        Vector3 gridCenterGlobal = transform.position;
        globalSphereCenter = gridCenterGlobal + new Vector3(0, 0, globalSphereDistanceFromGrid);

        foreach (Transform child in transform)
        {
            if (child == null) continue;

            var deformer = child.GetComponent<HeightmapMeshDeformer>();
            if (deformer != null)
            {
                deformer.heightScale = tileHeightScale;
                deformer.globalSphereCenter = globalSphereCenter;
                deformer.globalSphereDistanceFromGrid = globalSphereDistanceFromGrid;
                deformer.useSphericalDeformation = globalUseSphericalDeformation;
                deformer.Regenerate();
            }
        }
    }
}