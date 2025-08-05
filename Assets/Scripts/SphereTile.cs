using UnityEngine;

public class SphereTile
{
    Mesh mesh;
    int tileMeshResolution;
    Vector3 localup;
    Vector3 axisA;
    Vector3 axisB;

    int tileGridX;
    int tileGridY;
    int tilesPerSide;

    public SphereTile(Mesh mesh, int tileMeshResolution, Vector3 localup, Vector3 axisA, Vector3 axisB, int tileGridX, int tileGridY, int tilesPerSide)
    {
        this.mesh = mesh;
        this.tileMeshResolution = tileMeshResolution;
        this.localup = localup;
        this.axisA = axisA;
        this.axisB = axisB;
        this.tileGridX = tileGridX;
        this.tileGridY = tileGridY;
        this.tilesPerSide = tilesPerSide;
    }

    public void ConstructTileMesh()
    {
        Vector3[] vertices = new Vector3[tileMeshResolution * tileMeshResolution];
        Vector3[] normals = new Vector3[tileMeshResolution * tileMeshResolution];
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

                Vector3 pointOnUnitCube = localup +
                    (axisA * (finalPercent.x - 0.5f) * 2f) +
                    (axisB * (finalPercent.y - 0.5f) * 2f);

                Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;

                vertices[i] = pointOnUnitSphere;
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
    }
}