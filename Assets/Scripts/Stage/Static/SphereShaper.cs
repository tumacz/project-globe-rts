using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SphereShaper : MonoBehaviour
{
    [Header("Global Sphere Settings")]
    [Range(1, 8)] public int tilesPerSide = 2;
    [Range(2, 256)] public int tileMeshResolution = 8;
    [SerializeField] public Material sphereMaterial;
    [SerializeField, Min(0.1f)] public float SphereRadius = 1f;

    [Header("Editor Deformation Settings (Optional)")]
    [SerializeField] private Texture2D editorHeightMap;
    [SerializeField] private float editorHeightScale = 0f;
    [SerializeField] private float editorUniformGlobalScale = 1f;

    [Header("Deformation Method")]
    [Tooltip("If true, GPU Compute Shader will be used for mesh deformation in Editor. Requires a Compute Shader assigned.")]
    public bool useGpuDeformationInEditor = false;
    [SerializeField] private ComputeShader deformationComputeShader;

    private SphereFace[] editorSphereFaces;
    private MeshFilter[] editorMeshFilters;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;

            SafeEditorCleanup();
            InitializeEditorSphere();
            GenerateEditorMesh();
        };
    }

    private void InitializeEditorSphere()
    {
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        if (editorMeshFilters == null || editorMeshFilters.Length != 6)
            editorMeshFilters = new MeshFilter[6];

        editorSphereFaces = new SphereFace[6];

        TileDeformer_GPU masterEditorDeformer = null;
        if (useGpuDeformationInEditor && deformationComputeShader != null)
        {
            masterEditorDeformer = GetComponent<TileDeformer_GPU>();
            if (masterEditorDeformer == null) masterEditorDeformer = gameObject.AddComponent<TileDeformer_GPU>();
            masterEditorDeformer.computeShader = deformationComputeShader;
        }
        else
        {
            TileDeformer_GPU existing = GetComponent<TileDeformer_GPU>();
            if (existing != null) DestroyImmediate(existing);
        }

        for (int i = 0; i < 6; i++)
        {
            Transform existingFaceTransform = transform.Find($"SphereFaceMesh_{CleanVectorName(directions[i])}");
            GameObject faceGO;
            MeshFilter mf;
            MeshRenderer mr;

            if (existingFaceTransform != null)
            {
                faceGO = existingFaceTransform.gameObject;
                mf = faceGO.GetComponent<MeshFilter>();
                mr = faceGO.GetComponent<MeshRenderer>();
                if (mf == null) mf = faceGO.AddComponent<MeshFilter>();
                if (mr == null) mr = faceGO.AddComponent<MeshRenderer>();
            }
            else
            {
                faceGO = new GameObject($"SphereFaceMesh_{CleanVectorName(directions[i])}");
                faceGO.transform.SetParent(transform);
                mf = faceGO.AddComponent<MeshFilter>();
                mr = faceGO.AddComponent<MeshRenderer>();
            }

            mr.sharedMaterial = sphereMaterial;
            if (mf.sharedMesh == null)
                mf.sharedMesh = new Mesh();

            editorMeshFilters[i] = mf;

            editorSphereFaces[i] = new SphereFace(tilesPerSide, tileMeshResolution, directions[i]);

            editorSphereFaces[i].InitializeTiles(
                editorMeshFilters[i].transform,
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
        {
            face?.ConstructFaceMeshes(useGpuDeformationInEditor);
        }
    }

    public void SafeEditorCleanup()
    {
        var childrenToDestroy = new List<GameObject>();
        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            GameObject child = transform.GetChild(j).gameObject;
            if (child.name.Contains("SphereFaceMesh_") || child.name.Contains("SphereTile_Face_"))
            {
                childrenToDestroy.Add(child);
            }
        }
        foreach (GameObject child in childrenToDestroy)
        {
            if (child != null)
            {
                if (!Application.isPlaying) GameObject.DestroyImmediate(child);
                else GameObject.Destroy(child);
            }
        }
        editorMeshFilters = null;
        editorSphereFaces = null;

        if (!Application.isPlaying)
        {
            TileDeformer_GPU existing = GetComponent<TileDeformer_GPU>();
            if (existing != null) DestroyImmediate(existing);
        }
    }

    private void SetEditorMeshVisibility(bool visible)
    {
        if (editorMeshFilters == null) return;
        foreach (var mf in editorMeshFilters)
        {
            if (mf != null && mf.GetComponent<MeshRenderer>() != null)
            {
                mf.GetComponent<MeshRenderer>().enabled = visible;
                if (editorSphereFaces != null && editorSphereFaces.Length > 0)
                {
                    foreach (var face in editorSphereFaces)
                    {
                        face?.SetTileVisibility(visible);
                    }
                }
            }
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
                {
                    Object.Destroy(child);
                }
            }
            TileDeformer_GPU existing = GetComponent<TileDeformer_GPU>();
            if (existing != null) Object.Destroy(existing);
#endif
            this.enabled = false;
        }
    }

    private string CleanVectorName(Vector3 v) => v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");
}