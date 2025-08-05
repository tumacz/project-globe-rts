using UnityEngine;
using System.Collections.Generic;

public class SphereFace
{
    private Transform faceParentTransform;
    SphereTile[] tiles;
    int tilesPerSide;
    int tileMeshResolution;
    Vector3 localup;
    Vector3 axisA;
    Vector3 axisB;

    public SphereFace(int tilesPerSide, int tileMeshResolution, Vector3 localup)
    {
        this.tilesPerSide = tilesPerSide;
        this.tileMeshResolution = tileMeshResolution;
        this.localup = localup;

        axisA = new Vector3(localup.y, localup.z, localup.x);
        axisB = Vector3.Cross(localup, axisA);

        tiles = new SphereTile[tilesPerSide * tilesPerSide];
    }

    public void InitializeTiles(Transform parentTransform, Material tileMaterial)
    {
        this.faceParentTransform = parentTransform;

        string faceID = CleanVectorName(localup);

        var existingChildren = new List<GameObject>();
        for (int j = 0; j < faceParentTransform.childCount; j++)
        {
            GameObject child = faceParentTransform.GetChild(j).gameObject;
            if (child.name.StartsWith($"SphereTile_Face_{faceID}"))
                existingChildren.Add(child);
        }

        var requiredNames = new HashSet<string>();
        for (int y = 0; y < tilesPerSide; y++)
        {
            for (int x = 0; x < tilesPerSide; x++)
                requiredNames.Add($"SphereTile_Face_{faceID}_X{x}_Y{y}");
        }

        foreach (GameObject child in existingChildren)
        {
            if (!requiredNames.Contains(child.name))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(child);
                else
                    Object.Destroy(child);
#else
                Object.Destroy(child);
#endif
            }
        }

        for (int y = 0; y < tilesPerSide; y++)
        {
            for (int x = 0; x < tilesPerSide; x++)
            {
                int index = x + y * tilesPerSide;
                string tileName = $"SphereTile_Face_{faceID}_X{x}_Y{y}";

                GameObject tileGO = parentTransform.Find(tileName)?.gameObject;

                if (tileGO == null)
                {
                    tileGO = new GameObject(tileName);
                    tileGO.transform.SetParent(parentTransform);
                }

                MeshFilter meshFilter = tileGO.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = tileGO.AddComponent<MeshFilter>();

                MeshRenderer meshRenderer = tileGO.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = tileGO.AddComponent<MeshRenderer>();

                if (tileMaterial != null)
                    meshRenderer.sharedMaterial = tileMaterial;

                if (meshFilter.sharedMesh == null)
                    meshFilter.sharedMesh = new Mesh();

                tiles[index] = new SphereTile(meshFilter.sharedMesh, tileMeshResolution, localup, axisA, axisB, x, y, tilesPerSide);
            }
        }
    }

    public void ConstructFaceMeshes()
    {
        foreach (SphereTile tile in tiles)
        {
            tile?.ConstructTileMesh();
        }
    }

    private string CleanVectorName(Vector3 v) => v.ToString().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");
}