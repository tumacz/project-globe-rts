using UnityEngine;

public class SphereTile
{
    public Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material sharedMaterial;

    private int tileMeshResolution;
    private Vector3 localup;
    private Vector3 axisA;
    private Vector3 axisB;

    private int tileGridX;
    private int tileGridY;
    private int tilesPerSide;

    private Texture2D globalHeightMap;
    private float globalHeightScale;
    private float globalSphereRadius;
    private float globalUniformGlobalScale;

    private TileDeformer_GPU gpuDeformer;


    public SphereTile(Mesh mesh, MeshRenderer meshRenderer, Material sharedMaterial, int tileMeshResolution,
                        Vector3 localup, Vector3 axisA, Vector3 axisB,
                        int tileGridX, int tileGridY, int tilesPerSide,
                        Texture2D heightMap, float heightScale, float sphereRadius, float uniformGlobalScale,
                        TileDeformer_GPU gpuDeformerInstance = null)
    {
        this.mesh = mesh;
        this.meshRenderer = meshRenderer;
        this.sharedMaterial = sharedMaterial;
        this.tileMeshResolution = tileMeshResolution;
        this.localup = localup;
        this.axisA = axisA;
        this.axisB = axisB;
        this.tileGridX = tileGridX;
        this.tileGridY = tileGridY;
        this.tilesPerSide = tilesPerSide;

        this.globalHeightMap = heightMap;
        this.globalHeightScale = heightScale;
        this.globalSphereRadius = sphereRadius;
        this.globalUniformGlobalScale = uniformGlobalScale;

        this.gpuDeformer = gpuDeformerInstance;

        if (this.sharedMaterial != null)
        {
            // todo
        }
    }

    public void ConstructTileMesh(bool useGpuDeformation = false)
    {
        if (useGpuDeformation && gpuDeformer != null)
        {
            Vector3[] vertices = new Vector3[tileMeshResolution * tileMeshResolution];
            Vector3[] normals = new Vector3[tileMeshResolution * tileMeshResolution];
            Vector2[] uvs = new Vector2[tileMeshResolution * tileMeshResolution];
            int[] triangles = new int[(tileMeshResolution - 1) * (tileMeshResolution - 1) * 6];
            int triIndex = 0;

            for (int y = 0; y < tileMeshResolution; y++)
            {
                for (int x = 0; x < tileMeshResolution; x++)
                {
                    int i = x + y * tileMeshResolution;

                    Vector2 percent = new Vector2(x, y) / (tileMeshResolution - 1);

                    float pointX = (float)tileGridX / tilesPerSide + (percent.x / tilesPerSide);
                    float pointY = (float)tileGridY / tilesPerSide + (percent.y / tilesPerSide);

                    Vector3 pointOnUnitCube = localup +
                                              (axisA * (pointX - 0.5f) * 2f) +
                                              (axisB * (pointY - 0.5f) * 2f);

                    vertices[i] = pointOnUnitCube;

                    Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;
                    float u = 0.5f + Mathf.Atan2(pointOnUnitSphere.x, pointOnUnitSphere.z) / (2 * Mathf.PI);
                    float v = 0.5f - Mathf.Asin(pointOnUnitSphere.y) / Mathf.PI;
                    v = Mathf.Clamp01(v);
                    u = Mathf.Repeat(u, 1f);
                    uvs[i] = new Vector2(u, v);
                    normals[i] = pointOnUnitSphere;
                }
            }

            for (int y = 0; y < tileMeshResolution - 1; y++)
            {
                for (int x = 0; x < tileMeshResolution - 1; x++)
                {
                    int i = x + y * tileMeshResolution;

                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + tileMeshResolution + 1;
                    triangles[triIndex++] = i + tileMeshResolution;

                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + 1;
                    triangles[triIndex++] = i + tileMeshResolution + 1;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.RecalculateTangents();

            gpuDeformer.heightMap = globalHeightMap;
            gpuDeformer.heightScale = globalHeightScale;
            gpuDeformer.sphereRadius = globalSphereRadius;
            gpuDeformer.globalScale = globalUniformGlobalScale;
            gpuDeformer.DeformMesh(this.mesh);

        }
        else
        {
            Vector3[] vertices = new Vector3[tileMeshResolution * tileMeshResolution];
            Vector3[] normals = new Vector3[tileMeshResolution * tileMeshResolution];
            Vector2[] uvs = new Vector2[tileMeshResolution * tileMeshResolution];
            int[] triangles = new int[(tileMeshResolution - 1) * (tileMeshResolution - 1) * 6];
            int triIndex = 0;

            for (int y = 0; y < tileMeshResolution; y++)
            {
                for (int x = 0; x < tileMeshResolution; x++)
                {
                    int i = x + y * tileMeshResolution;

                    Vector2 percent = new Vector2(x, y) / (tileMeshResolution - 1);

                    float pointX = (float)tileGridX / tilesPerSide + (percent.x / tilesPerSide);
                    float pointY = (float)tileGridY / tilesPerSide + (percent.y / tilesPerSide);

                    Vector3 pointOnUnitCube = localup +
                                              (axisA * (pointX - 0.5f) * 2f) +
                                              (axisB * (pointY - 0.5f) * 2f);

                    Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;

                    float u = 0.5f + Mathf.Atan2(pointOnUnitSphere.x, pointOnUnitSphere.z) / (2 * Mathf.PI);
                    float v = 0.5f - Mathf.Asin(pointOnUnitSphere.y) / Mathf.PI;
                    v = Mathf.Clamp01(v);
                    u = Mathf.Repeat(u, 1f);

                    uvs[i] = new Vector2(u, v);

                    float elevation = 0;
                    if (globalHeightMap != null)
                    {
                        elevation = globalHeightMap.GetPixelBilinear(1.0f - u, 1.0f - v).r; // reversed U V
                    }

                    float actualElevation = (elevation - 0.5f) * globalHeightScale;

                    Vector3 deformedPoint = pointOnUnitSphere * (globalSphereRadius + actualElevation) * globalUniformGlobalScale;

                    vertices[i] = deformedPoint;
                    normals[i] = pointOnUnitSphere;
                }
            }

            for (int y = 0; y < tileMeshResolution - 1; y++)
            {
                for (int x = 0; x < tileMeshResolution - 1; x++)
                {
                    int i = x + y * tileMeshResolution;

                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + tileMeshResolution + 1;
                    triangles[triIndex++] = i + tileMeshResolution;

                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + 1;
                    triangles[triIndex++] = i + tileMeshResolution + 1;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.RecalculateTangents();
        }
    }

    public void SetVisibility(bool visible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
    }
}