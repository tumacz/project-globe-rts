//using UnityEngine;
//using System.Collections.Generic;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//[ExecuteAlways]
//public class TileGridSpawner : MonoBehaviour
//{
//    public GameObject tilePrefab;
//    [Range(1, 100)] public int gridSizeX = 10;
//    [Range(1, 100)] public int gridSizeY = 10;
//    public float tileSpacing = 1.0f;

//    [Header("Global Deformation Settings")]
//    public bool globalUseSphericalDeformation = true;
//    public Vector3 globalSphereCenter = Vector3.zero;
//    [Range(1.0f, 20.0f)] public float globalSphereDistanceFromGrid = 20.0f;
//    [Range(0.1f, 1.0f)] public float tileHeightScale = 1.0f;

//    [Header("Tile Mesh Settings")]
//    [Range(2, 255)]
//    public int tileSubdivisions = 20;

//    [Header("Heightmap Settings")]
//    public Texture2D globalHeightMap;

//#if UNITY_EDITOR
//    public static HashSet<GameObject> _pendingDestroyEditor = new HashSet<GameObject>();
//#endif

//    private void Awake()
//    {
//        if (Application.isPlaying)
//        {
//            SpawnTiles();
//        }
//    }

//#if UNITY_EDITOR
//    private void OnValidate()
//    {
//        if (!Application.isPlaying && gameObject.activeInHierarchy)
//        {
//            EditorApplication.delayCall += () =>
//            {
//                if (this == null) return;

//                SafeEditorCleanup();
//                SpawnTiles();
//                UpdateDeformationParameters();
//            };
//        }
//    }

//    public void SafeEditorCleanup()
//    {
//        List<GameObject> childrenToDestroy = new List<GameObject>();
//        for (int i = transform.childCount - 1; i >= 0; i--)
//        {
//            GameObject child = transform.GetChild(i).gameObject;
//            if (child.name.StartsWith("Tile_"))
//            {
//                childrenToDestroy.Add(child);
//            }
//        }

//        foreach (GameObject child in childrenToDestroy)
//        {
//            if (child != null)
//            {
//                _pendingDestroyEditor.Add(child);
//            }
//        }
//    }
//#endif

//    public void SpawnTiles()
//    {
//        List<GameObject> childrenToDestroy = new List<GameObject>();
//        foreach (Transform child in transform)
//        {
//            childrenToDestroy.Add(child.gameObject);
//        }
//        foreach (GameObject child in childrenToDestroy)
//        {
//#if UNITY_EDITOR
//            if (Application.isPlaying)
//                Destroy(child);
//            else
//                _pendingDestroyEditor.Add(child);
//#else
//            Destroy(child);
//#endif
//        }

//        Vector3 gridCenterGlobal = transform.position;
//        globalSphereCenter = gridCenterGlobal + new Vector3(0, 0, globalSphereDistanceFromGrid);

//        float centerX = (gridSizeX - 1) / 2f;
//        float centerY = (gridSizeY - 1) / 2f;

//        for (int y = 0; y < gridSizeY; y++)
//        {
//            for (int x = 0; x < gridSizeX; x++)
//            {
//                Vector3 spawnPos = new Vector3(
//                    (x - centerX) * tileSpacing,
//                    (y - centerY) * tileSpacing,
//                    0
//                );

//                GameObject tile = Instantiate(tilePrefab, spawnPos, Quaternion.identity, transform);
//                tile.name = $"Tile_{x}_{y}";

//                Rect tileUvRect = new Rect(
//                    (float)x / gridSizeX,
//                    (float)y / gridSizeY,
//                    1f / gridSizeX,
//                    1f / gridSizeY
//                );

//                var deformer = tile.GetComponent<HeightmapMeshDeformer>();
//                if (deformer != null)
//                {
//                    deformer.subdivisions = tileSubdivisions;
//                    deformer.heightScale = tileHeightScale;
//                    deformer.globalSphereCenter = globalSphereCenter;
//                    deformer.globalSphereDistanceFromGrid = globalSphereDistanceFromGrid;
//                    deformer.useSphericalDeformation = globalUseSphericalDeformation;
//                }

//                var tileDeformerConfigurator = tile.GetComponent<TileDeformerConfigurator>();
//                if (tileDeformerConfigurator != null)
//                {
//                    tileDeformerConfigurator.Setup(this.globalHeightMap, tileUvRect);
//                }
//            }
//        }
//    }

//    public void UpdateDeformationParameters()
//    {
//        Vector3 gridCenterGlobal = transform.position;
//        globalSphereCenter = gridCenterGlobal + new Vector3(0, 0, globalSphereDistanceFromGrid);

//        foreach (Transform child in transform)
//        {
//            if (child == null) continue;

//            var deformer = child.GetComponent<HeightmapMeshDeformer>();
//            if (deformer != null)
//            {
//                deformer.heightScale = tileHeightScale;
//                deformer.globalSphereCenter = globalSphereCenter;
//                deformer.globalSphereDistanceFromGrid = globalSphereDistanceFromGrid;
//                deformer.useSphericalDeformation = globalUseSphericalDeformation;
//                deformer.Regenerate();
//            }
//        }
//    }
//}
// TileGridSpawner.cs
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
    [Range(1.0f, 200.0f)] public float globalSphereDistanceFromGrid = 20.0f;
    [Range(0.1f, 10.0f)] public float tileHeightScale = 1.0f;

    [Header("Tile Mesh Settings")]
    [Range(2, 255)]
    public int tileSubdivisions = 20;

    [Header("Heightmap Settings")]
    public Texture2D globalHeightMap;

#if UNITY_EDITOR
    public static HashSet<GameObject> _pendingDestroyEditor = new HashSet<GameObject>();
#endif

    // --- View window in UV space (centered)
    [HideInInspector] public float viewCenterU = 0.5f;
    [HideInInspector] public float viewCenterV = 0.5f;
    [HideInInspector] public float viewUvWidth = 0.1f;
    [HideInInspector] public float viewUvHeight = 0.05f;

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
        // remove existing tiles
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

        // compute center in world-space used for sphere center
        Vector3 gridCenterGlobal = transform.position;
        globalSphereCenter = gridCenterGlobal + new Vector3(0, 0, globalSphereDistanceFromGrid);

        float centerX = (gridSizeX - 1) / 2f;
        float centerY = (gridSizeY - 1) / 2f;

        // per-tile uv size based on current view window
        float tileUvW = viewUvWidth / gridSizeX;
        float tileUvH = viewUvHeight / gridSizeY;

        // bottom-left corner of view window in UV space
        float viewOriginU = viewCenterU - viewUvWidth * 0.5f;
        float viewOriginV = viewCenterV - viewUvHeight * 0.5f;

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

                // compute per-tile UV rect inside the view window
                float uvX = viewOriginU + x * tileUvW;
                float uvY = viewOriginV + y * tileUvH;

                Rect tileUvRect = new Rect(uvX, uvY, tileUvW, tileUvH);

                var deformer = tile.GetComponent<HeightmapMeshDeformer>();
                if (deformer != null)
                {
                    deformer.subdivisions = tileSubdivisions;
                    deformer.heightScale = 0.3f;//                    deformer.heightScale = tileHeightScale;
                    deformer.globalSphereCenter = globalSphereCenter;
                    deformer.globalSphereDistanceFromGrid = globalSphereDistanceFromGrid;
                    deformer.useSphericalDeformation = globalUseSphericalDeformation;
                }

                var tileDeformerConfigurator = tile.GetComponent<TileDeformerConfigurator>();
                if (tileDeformerConfigurator != null)
                {
                    // pass the UV rect wrapped/clamped for heightmap sampling
                    tileDeformerConfigurator.Setup(this.globalHeightMap, WrapRectUV(tileUvRect));
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
                deformer.heightScale = 0.3f;//deformer.heightScale = tileHeightScale;
                deformer.globalSphereCenter = globalSphereCenter;
                deformer.globalSphereDistanceFromGrid = globalSphereDistanceFromGrid;
                deformer.useSphericalDeformation = globalUseSphericalDeformation;
                deformer.Regenerate();
            }

            var config = child.GetComponent<TileDeformerConfigurator>();
            if (config != null)
            {
                // try to compute tile's uv rect from its name (Tile_x_y) so we can update without respawning
                var parts = child.name.Split('_');
                if (parts.Length == 3)
                {
                    if (int.TryParse(parts[1], out int tx) && int.TryParse(parts[2], out int ty))
                    {
                        float tileUvW = viewUvWidth / gridSizeX;
                        float tileUvH = viewUvHeight / gridSizeY;
                        float viewOriginU = viewCenterU - viewUvWidth * 0.5f;
                        float viewOriginV = viewCenterV - viewUvHeight * 0.5f;
                        Rect tileUvRect = new Rect(viewOriginU + tx * tileUvW, viewOriginV + ty * tileUvH, tileUvW, tileUvH);
                        config.Setup(this.globalHeightMap, WrapRectUV(tileUvRect));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Public API: set the center and size of the view window in UV space (0..1)
    /// and optionally respawn tiles to reflect new window size.
    /// </summary>
    public void SetHeightmapView(float centerU, float centerV, float uvWidth, float uvHeight)
    {
        viewCenterU = centerU;
        viewCenterV = centerV;
        viewUvWidth = Mathf.Clamp01(uvWidth);
        viewUvHeight = Mathf.Clamp01(uvHeight);

        // If the view window is larger than 1.0 clamp it but keep center
        viewUvWidth = Mathf.Min(viewUvWidth, 1f);
        viewUvHeight = Mathf.Min(viewUvHeight, 1f);

        // Recreate tiles if the per-tile UV size changed dramatically (simple heuristic) or always update UVs
        // For now we will update existing tiles' UVs if present, otherwise spawn.
        if (transform.childCount == 0)
        {
            SpawnTiles();
        }
        else
        {
            UpdateDeformationParameters();
        }
    }

    /// <summary>
    /// Wrap or clamp UV rect so each value is in 0..1. For U (longitude) we wrap, for V (latitude) we clamp.
    /// Note: Rect may span outside 0..1 in U; we keep it as-is because HeightmapMeshDeformer uses GetPixelBilinear
    /// which accepts coordinates outside 0..1 depending on texture wrap mode. However, to be robust we normalize U.
    /// </summary>
    private Rect WrapRectUV(Rect r)
    {
        float u = r.x;
        float v = Mathf.Clamp01(r.y);

        // wrap u into [0,1)
        u = u - Mathf.Floor(u);

        return new Rect(u, v, r.width, r.height);
    }
}