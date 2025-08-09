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

    private Texture2D heightMap;
    private float heightScale;
    private float sphereRadius;
    private float uniformGlobalScale;

    public SphereTile(Mesh mesh, MeshRenderer meshRenderer, Material sharedMaterial, int tileMeshResolution, Vector3 localup, Vector3 axisA, Vector3 axisB,
                      int tileGridX, int tileGridY, int tilesPerSide, Texture2D heightMap, float heightScale, float sphereRadius, float uniformGlobalScale)
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
        this.heightMap = heightMap;
        this.heightScale = heightScale;
        this.sphereRadius = sphereRadius;
        this.uniformGlobalScale = uniformGlobalScale;
    }

    public void ConstructTileMesh()
    {
        if (heightMap == null && sharedMaterial != null && sharedMaterial.HasProperty("_HeightMap"))
        {
            heightMap = (Texture2D)sharedMaterial.GetTexture("_HeightMap");
        }

        if (sharedMaterial != null)
        {
            if (sharedMaterial.HasProperty("_HeightScale")) heightScale = sharedMaterial.GetFloat("_HeightScale");
            if (sharedMaterial.HasProperty("_SphereRadius")) sphereRadius = sharedMaterial.GetFloat("_SphereRadius");
            if (sharedMaterial.HasProperty("_UniformGlobalScale")) uniformGlobalScale = sharedMaterial.GetFloat("_UniformGlobalScale");
        }


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

                Vector2 localPercent = new Vector2(x, y) / (tileMeshResolution - 1);

                float tileStartX = (float)tileGridX / tilesPerSide;
                float tileStartY = (float)tileGridY / tilesPerSide;

                float globalXPercent = tileStartX + (localPercent.x / tilesPerSide);
                float globalYPercent = tileStartY + (localPercent.y / tilesPerSide);

                Vector2 finalPercent = new Vector2(globalXPercent, globalYPercent);
                uvs[i] = finalPercent;

                Vector3 pointOnUnitCube = localup +
                    (axisA * (finalPercent.x - 0.5f) * 2f) +
                    (axisB * (finalPercent.y - 0.5f) * 2f);

                Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;

                float elevation = 0;
                if (heightMap != null)
                {
                    elevation = heightMap.GetPixelBilinear(finalPercent.x, finalPercent.y).r;
                }

                Vector3 deformedPoint = pointOnUnitSphere * (sphereRadius + elevation * heightScale) * uniformGlobalScale;

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

    public void SetVisibility(bool visible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
    }
}