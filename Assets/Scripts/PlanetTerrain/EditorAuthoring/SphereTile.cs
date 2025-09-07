using UnityEngine;
using GlobeRTS.PlanetTerrain.Runtime.Meshing;
using GlobeRTS.PlanetTerrain.Runtime.HeightData;

public class SphereTile
{
    public Mesh mesh;
    private readonly MeshRenderer meshRenderer;
    private readonly Material sharedMaterial;

    private readonly int tileMeshResolution;
    private readonly Vector3 localup;
    private readonly Vector3 axisA;
    private readonly Vector3 axisB;

    private readonly int tileGridX;
    private readonly int tileGridY;
    private readonly int tilesPerSide;

    private readonly Texture2D globalHeightMap;
    private readonly float globalHeightScale;
    private readonly float globalSphereRadius;
    private readonly float globalUniformGlobalScale;

    private readonly TileDeformer_GPU gpuDeformer;

    // --- Reuse buffers to reduce GC ---
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector2[] _uvs;

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
    }

    private void EnsureBuffers(int res)
    {
        int vCount = res * res;
        if (_vertices == null || _vertices.Length != vCount)
        {
            _vertices = new Vector3[vCount];
            _normals = new Vector3[vCount];
            _uvs = new Vector2[vCount];
        }
    }

    public void ConstructTileMesh(bool useGpuDeformation = false)
    {
        var percents = MeshCache.GetPercents(tileMeshResolution);
        var triangles = MeshCache.GetTriangles(tileMeshResolution);

        EnsureBuffers(tileMeshResolution);

        // Precompute constants
        float invTiles = 1f / tilesPerSide;
        float baseX = tileGridX * invTiles;
        float baseY = tileGridY * invTiles;

        // Fast constants for angle-to-uv mapping
        const float kTwoPI = 6.28318530717958647692f; // 2 * PI
        const float kPI = 3.14159265358979323846f;
        float invTwoPi = 1f / kTwoPI;
        float invPi = 1f / kPI;

        // Helper: cube->sphere mapping (no normalize call in hot path)
        static Vector3 Spherify(Vector3 p)
        {
            float x = p.x, y = p.y, z = p.z;
            float x2 = x * x, y2 = y * y, z2 = z * z;
            float sx = x * Mathf.Sqrt(1f - (y2 + z2) * 0.5f + (y2 * z2) / 3f);
            float sy = y * Mathf.Sqrt(1f - (z2 + x2) * 0.5f + (z2 * x2) / 3f);
            float sz = z * Mathf.Sqrt(1f - (x2 + y2) * 0.5f + (x2 * y2) / 3f);
            return new Vector3(sx, sy, sz);
        }

        if (useGpuDeformation && gpuDeformer != null)
        {
            // GPU path: generate base geometry (on the cube), GPU computes heights
            for (int y = 0; y < tileMeshResolution; y++)
            {
                int row = y * tileMeshResolution;
                for (int x = 0; x < tileMeshResolution; x++)
                {
                    int i = row + x;

                    Vector2 p = percents[i];
                    // multiply before add (analyzer-friendly)
                    float pointX = baseX + invTiles * p.x;
                    float pointY = baseY + invTiles * p.y;

                    // Map [0,1] -> [-1,1]: sx = 2*t - 1
                    float sx = 2f * pointX - 1f;
                    float sy = 2f * pointY - 1f;

                    // Multiply terms first, constant last
                    Vector3 pointOnUnitCube = axisA * sx + axisB * sy + localup;
                    Vector3 pointOnUnitSphere = Spherify(pointOnUnitCube);

                    _vertices[i] = pointOnUnitCube;   // GPU deformer expects "base" position (cube)
                    _normals[i] = pointOnUnitSphere; // store sphere dir for UV + normal

                    // Spherical UV
                    float u = 0.5f + Mathf.Atan2(pointOnUnitSphere.x, pointOnUnitSphere.z) * invTwoPi;
                    float v = 0.5f - Mathf.Asin(pointOnUnitSphere.y) * invPi;
                    v = Mathf.Clamp01(v);
                    u = Mathf.Repeat(u, 1f);
                    _uvs[i] = new Vector2(u, v);
                }
            }

            mesh.Clear();
            mesh.MarkDynamic();
            mesh.vertices = _vertices;
            mesh.triangles = triangles;
            mesh.normals = _normals;
            mesh.uv = _uvs;
            mesh.RecalculateTangents();

            gpuDeformer.heightMap = globalHeightMap;
            gpuDeformer.heightScale = globalHeightScale;
            gpuDeformer.sphereRadius = globalSphereRadius;
            gpuDeformer.globalScale = globalUniformGlobalScale;
            gpuDeformer.DeformMesh(mesh);
        }
        else
        {
            // CPU: fast sampler for height
            HeightSampler sampler = HeightSamplerCache.Get(globalHeightMap);

            for (int y = 0; y < tileMeshResolution; y++)
            {
                int row = y * tileMeshResolution;
                for (int x = 0; x < tileMeshResolution; x++)
                {
                    int i = row + x;

                    Vector2 p = percents[i];
                    float pointX = baseX + invTiles * p.x;
                    float pointY = baseY + invTiles * p.y;

                    float sx = 2f * pointX - 1f;
                    float sy = 2f * pointY - 1f;

                    Vector3 pointOnUnitCube = axisA * sx + axisB * sy + localup;
                    Vector3 pointOnUnitSphere = Spherify(pointOnUnitCube);

                    float u = 0.5f + Mathf.Atan2(pointOnUnitSphere.x, pointOnUnitSphere.z) * invTwoPi;
                    float v = 0.5f - Mathf.Asin(pointOnUnitSphere.y) * invPi;
                    v = Mathf.Clamp01(v);
                    u = Mathf.Repeat(u, 1f);
                    _uvs[i] = new Vector2(u, v);

                    // Samplers expect UVs as provided; flip kept here by convention
                    float elevation01 = (sampler != null) ? sampler.Sample01(1f - u, 1f - v) : 0.5f;
                    float actualElevation = (elevation01 - 0.5f) * globalHeightScale;

                    _vertices[i] = pointOnUnitSphere * (globalSphereRadius + actualElevation) * globalUniformGlobalScale;
                    _normals[i] = pointOnUnitSphere;
                }
            }

            mesh.Clear();
            mesh.MarkDynamic();
            mesh.vertices = _vertices;
            mesh.triangles = triangles;
            mesh.normals = _normals;
            mesh.uv = _uvs;
            mesh.RecalculateTangents(); // if no normal map → you can disable
        }
    }

    public void SetVisibility(bool visible)
    {
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
    }
}
