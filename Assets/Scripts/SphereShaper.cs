using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SphereShaper : MonoBehaviour
{
    [Header("Global Sphere Settings")]
    [Range(1, 32)] public int tilesPerSide = 2;
    [Range(2, 256)] public int tileMeshResolution = 8;
    [SerializeField] public Material sphereMaterial;
    [SerializeField, Min(0.1f)] public float SphereRadius = 1f;

    private SphereFace[] editorSphereFaces;
    private MeshFilter[] editorMeshFilters;

#if UNITY_EDITOR
    private void OnValidate()
    {
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

        for (int i = 0; i < 6; i++)
        {
            if (editorMeshFilters[i] == null || editorMeshFilters[i].gameObject == null)
            {
                GameObject faceGO = new GameObject($"SphereFaceMesh_{CleanVectorName(directions[i])}");
                faceGO.transform.SetParent(transform);
                editorMeshFilters[i] = faceGO.AddComponent<MeshFilter>();
                faceGO.AddComponent<MeshRenderer>();
            }
            editorMeshFilters[i].GetComponent<MeshRenderer>().sharedMaterial = sphereMaterial;
            if (editorMeshFilters[i].sharedMesh == null)
                editorMeshFilters[i].sharedMesh = new Mesh();

            editorSphereFaces[i] = new SphereFace(tilesPerSide, tileMeshResolution, directions[i]);
            editorSphereFaces[i].InitializeTiles(editorMeshFilters[i].transform, sphereMaterial);
        }
        SetEditorMeshVisibility(true);
    }

    private void GenerateEditorMesh()
    {
        if (editorSphereFaces == null) return;
        foreach (SphereFace face in editorSphereFaces)
        {
            face?.ConstructFaceMeshes();
        }
    }

    public void SafeEditorCleanup()
    {
        var childrenToDestroy = new List<GameObject>();
        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            GameObject child = transform.GetChild(j).gameObject;
            if (child.name.Contains("SphereFaceMesh_") || child.name.Contains("Chunk_Face_"))
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
    }

    private void SetEditorMeshVisibility(bool visible)
    {
        if (editorMeshFilters == null) return;
        foreach (var mf in editorMeshFilters)
        {
            if (mf != null && mf.GetComponent<MeshRenderer>() != null)
            {
                mf.GetComponent<MeshRenderer>().enabled = visible;
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
                if (child.name.Contains("SphereFaceMesh_"))
                {
                    Object.Destroy(child);
                }
            }
#endif
            this.enabled = false;
        }
    }

    private string CleanVectorName(Vector3 v) => v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");
}