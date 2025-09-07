using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HeightmapMeshDeformer : MonoBehaviour
{
    [Range(2, 255)]
    public int subdivisions = 20;

    [Range(-1, 1)]
    public float heightScale = 1.0f;

    public Texture2D heightMap;

    [HideInInspector]
    public Rect uvRect = new Rect(0, 0, 1, 1);

    [Header("Sphere Deformation (Global Control)")]
    public bool useSphericalDeformation = false;

    [HideInInspector]
    public float globalSphereDistanceFromGrid = 100.0f;

    [HideInInspector]
    public Vector3 globalSphereCenter;

    private MeshFilter _meshFilter;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter.sharedMesh == null)
        {
            _meshFilter.sharedMesh = new Mesh();
            _meshFilter.sharedMesh.name = "GeneratedDeformedMesh";
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && gameObject.activeInHierarchy)
        {
            Regenerate();
        }
    }

    public void Regenerate()
    {
        if (_meshFilter == null)
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                Debug.LogError("HeightmapMeshDeformer requires a MeshFilter component!", this);
                return;
            }
        }

        if (heightMap == null)
        {
            if (_meshFilter.sharedMesh != null)
            {
                _meshFilter.sharedMesh.Clear();
            }
            return;
        }

        GenerateDeformedMesh();
    }

    private void GenerateDeformedMesh()
    {
        int width = subdivisions + 1;
        int height = subdivisions + 1;

        Vector3[] vertices = new Vector3[width * height];
        Vector2[] uvs = new Vector2[width * height];
        int[] triangles = new int[subdivisions * subdivisions * 6];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                float uLocal = (float)x / (width - 1);
                float vLocal = (float)y / (height - 1);

                float uGlobal = uvRect.x + uLocal * uvRect.width;
                float vGlobal = uvRect.y + vLocal * uvRect.height;

                Color pixel = heightMap.GetPixelBilinear(uGlobal, vGlobal);
                float currentHeight = (pixel.r - 0.5f) * heightScale;

                Vector3 basePosition;

                if (useSphericalDeformation)
                {
                    Vector3 localFlatVertexOffset = new Vector3(uLocal - 0.5f, vLocal - 0.5f, 0);
                    Vector3 globalFlatVertexPosition = transform.TransformPoint(localFlatVertexOffset);
                    Vector3 directionFromSphereCenter = (globalFlatVertexPosition - globalSphereCenter).normalized;
                    basePosition = globalSphereCenter + directionFromSphereCenter * (globalSphereDistanceFromGrid + currentHeight);
                    basePosition = transform.InverseTransformPoint(basePosition);
                }
                else
                {
                    float posX = uLocal - 0.5f;
                    float posY = vLocal - 0.5f;
                    basePosition = new Vector3(posX, posY, currentHeight);
                }

                vertices[i] = basePosition;
                uvs[i] = new Vector2(uGlobal, vGlobal);
            }
        }

        int t = 0;
        for (int y = 0; y < subdivisions; y++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                int i = x + y * width;

                triangles[t++] = i;
                triangles[t++] = i + width;
                triangles[t++] = i + 1;

                triangles[t++] = i + 1;
                triangles[t++] = i + width;
                triangles[t++] = i + width + 1;
            }
        }

        Mesh mesh = _meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            _meshFilter.sharedMesh = mesh;
            mesh.name = "GeneratedDeformedMesh";
        }
        else
        {
            mesh.Clear();
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
    }
}
