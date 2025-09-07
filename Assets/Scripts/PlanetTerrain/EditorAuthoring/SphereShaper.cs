using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SphereShaper : MonoBehaviour
{
    [Header("Global Sphere Settings")]
    [Range(1, 16)] public int tilesPerSide = 2;
    [Range(2, 256)] public int tileMeshResolution = 8;
    public Material sphereMaterial;
    [Min(0.1f)] public float SphereRadius = 1f;

    [Header("Editor Deformation Settings (Optional)")]
    [SerializeField] private Texture2D editorHeightMap;
    [SerializeField] private float editorHeightScale = 0f;
    [SerializeField] private float editorUniformGlobalScale = 1f;

    [Header("Deformation Method (Editor)")]
    [Tooltip("If true, GPU Compute Shader will be used for mesh deformation in Editor.")]
    public bool useGpuDeformationInEditor = false;
    [SerializeField] private ComputeShader deformationComputeShader;

    private SphereFace[] editorSphereFaces;
    private MeshFilter[] editorMeshFilters;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;

            try
            {
                SafeEditorCleanup();
                InitializeEditorSphere();
                GenerateEditorMesh();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        };
    }

    private void InitializeEditorSphere()
    {
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        if (editorMeshFilters == null || editorMeshFilters.Length != 6)
            editorMeshFilters = new MeshFilter[6];

        editorSphereFaces = new SphereFace[6];

        bool canUseEditorGpu = useGpuDeformationInEditor && deformationComputeShader != null;

        TileDeformer_GPU masterEditorDeformer = null;
        if (canUseEditorGpu)
        {
            // Avoid GetComponent allocation
            if (!TryGetComponent(out masterEditorDeformer) || masterEditorDeformer == null)
                masterEditorDeformer = gameObject.AddComponent<TileDeformer_GPU>();

            masterEditorDeformer.computeShader = deformationComputeShader;
            if (masterEditorDeformer.computeShader == null)
            {
                canUseEditorGpu = false;
            }
        }

        if (!canUseEditorGpu)
        {
            // Remove if present without using GetComponent (avoid alloc)
            if (TryGetComponent(out TileDeformer_GPU existing) && existing != null)
            {
                DestroyImmediate(existing);
            }

            if (useGpuDeformationInEditor && deformationComputeShader == null)
                Debug.LogWarning("[SphereShaper] useGpuDeformationInEditor = ON, but ComputeShader is not assigned. Editor switches to CPU.");

            useGpuDeformationInEditor = false;
            masterEditorDeformer = null;
        }

        for (int i = 0; i < 6; i++)
        {
            string faceName = $"SphereFaceMesh_{CleanVectorName(directions[i])}";

            Transform existingFaceTransform = transform.Find(faceName);
            GameObject faceGO;
            MeshFilter mf;
            MeshRenderer mr;

            if (existingFaceTransform != null)
            {
                faceGO = existingFaceTransform.gameObject;

                // MeshFilter
                if (!faceGO.TryGetComponent(out mf) || mf == null)
                    mf = faceGO.AddComponent<MeshFilter>();

                // MeshRenderer
                if (!faceGO.TryGetComponent(out mr) || mr == null)
                    mr = faceGO.AddComponent<MeshRenderer>();
            }
            else
            {
                faceGO = new GameObject(faceName);
                faceGO.transform.SetParent(transform, false);
                mf = faceGO.AddComponent<MeshFilter>();
                mr = faceGO.AddComponent<MeshRenderer>();
            }

            // Assign material if provided
            mr.sharedMaterial = sphereMaterial;

            if (mf.sharedMesh == null)
                mf.sharedMesh = new Mesh();

            editorMeshFilters[i] = mf;

            var face = new SphereFace(tilesPerSide, tileMeshResolution, directions[i]);
            editorSphereFaces[i] = face;

            Transform parentForTiles = (editorMeshFilters[i] != null) ? editorMeshFilters[i].transform : faceGO.transform;

            face.InitializeTiles(
                parentForTiles,
                sphereMaterial,
                editorHeightMap,
                editorHeightScale,
                SphereRadius,
                editorUniformGlobalScale,
                masterEditorDeformer
            );
        }

        SetEditorMeshVisibility(true);
    }

    private void GenerateEditorMesh()
    {
        if (editorSphereFaces == null) return;
        foreach (SphereFace face in editorSphereFaces)
            face?.ConstructFaceMeshes(useGpuDeformationInEditor);
    }

    public void SafeEditorCleanup()
    {
        var toDestroy = new List<GameObject>();
        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            GameObject child = transform.GetChild(j).gameObject;
            if (child.name.Contains("SphereFaceMesh_") || child.name.Contains("SphereTile_Face_"))
                toDestroy.Add(child);
        }

        foreach (var go in toDestroy)
        {
            if (go == null) continue;
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
        }

        editorMeshFilters = null;
        editorSphereFaces = null;

        // Avoid GetComponent allocation
        if (TryGetComponent(out TileDeformer_GPU existing) && existing != null)
        {
            DestroyImmediate(existing);
        }
    }

    private void SetEditorMeshVisibility(bool visible)
    {
        if (editorMeshFilters == null) return;
        foreach (var mf in editorMeshFilters)
        {
            if (mf == null) continue;

            // Avoid GetComponent allocation
            if (mf.TryGetComponent(out MeshRenderer rend) && rend != null)
                rend.enabled = visible;
        }

        if (editorSphereFaces != null)
        {
            foreach (var face in editorSphereFaces)
                face?.SetTileVisibility(visible);
        }
    }
#endif

    private void Awake()
    {
        if (Application.isPlaying)
        {
#if UNITY_EDITOR
            SetEditorMeshVisibility(false);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.name.Contains("SphereFaceMesh_") || child.name.Contains("SphereTile_Face_"))
                    Object.Destroy(child);
            }
            // Avoid GetComponent allocation in play mode cleanup
            if (TryGetComponent(out TileDeformer_GPU existing) && existing != null)
            {
                Object.Destroy(existing);
            }
#endif
            this.enabled = false;
        }
    }

    private string CleanVectorName(Vector3 v) =>
        v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");
}