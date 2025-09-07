using UnityEngine;

public class TileDeformer_GPU : MonoBehaviour
{
    public ComputeShader computeShader;
    public Texture2D heightMap;

    public float heightScale;
    public float sphereRadius;
    public float globalScale;

    private ComputeBuffer originalVerticesBuffer;
    private ComputeBuffer deformedVerticesBuffer;
    private ComputeBuffer uvBuffer;

    private readonly MeshFilter meshFilter;
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector2[] meshUVs;
    private int kernelID;

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    public void DeformMesh(Mesh meshToDeform)
    {
        if (!ValidateSetup(meshToDeform))
            return;

        SetupBuffers();

        if (originalVerticesBuffer == null || deformedVerticesBuffer == null || uvBuffer == null)
            return;

        int threadGroupsX = Mathf.CeilToInt(originalVertices.Length / 64.0f);

        computeShader.Dispatch(kernelID, threadGroupsX, 1, 1);

        Vector3[] newVertices = new Vector3[originalVertices.Length];
        deformedVerticesBuffer.GetData(newVertices);

        mesh.vertices = newVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    private bool ValidateSetup(Mesh meshToDeform)
    {
        this.mesh = meshToDeform;
        if (mesh == null)
        {
            Debug.LogError("Mesh is not assigned.");
            return false;
        }

        if (computeShader == null || heightMap == null)
        {
            Debug.LogError("Compute Shader or Height Map is not assigned.");
            return false;
        }

        originalVertices = mesh.vertices;
        meshUVs = mesh.uv;

        if (meshUVs == null || meshUVs.Length != originalVertices.Length)
        {
            Debug.LogError($"UV buffer missing or size mismatch (uvs:{(meshUVs == null ? 0 : meshUVs.Length)} vs verts:{originalVertices.Length}). Ensure SphereTile assigns mesh.uv.");
            return false;
        }

        kernelID = computeShader.FindKernel("CSMain");

        return true;
    }

    private void SetupBuffers()
    {
        ReleaseBuffers();

        heightMap.wrapMode = TextureWrapMode.Clamp;
        heightMap.filterMode = FilterMode.Bilinear;
        heightMap.anisoLevel = 0;

        int vertexCount = originalVertices.Length;

        originalVerticesBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        deformedVerticesBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        uvBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);

        originalVerticesBuffer.SetData(originalVertices);
        uvBuffer.SetData(meshUVs);

        computeShader.SetBuffer(kernelID, "originalVertices", originalVerticesBuffer);
        computeShader.SetBuffer(kernelID, "deformedVertices", deformedVerticesBuffer);
        computeShader.SetBuffer(kernelID, "uvs", uvBuffer);

        computeShader.SetTexture(kernelID, "heightMap", heightMap);

        computeShader.SetFloat("heightScale", heightScale);
        computeShader.SetFloat("sphereRadius", sphereRadius);
        computeShader.SetFloat("globalScale", globalScale);
        computeShader.SetInt("vertexCount", vertexCount);
    }

    private void ReleaseBuffers()
    {
        if (originalVerticesBuffer != null)
        {
            originalVerticesBuffer.Release();
            originalVerticesBuffer = null;
        }
        if (deformedVerticesBuffer != null)
        {
            deformedVerticesBuffer.Release();
            deformedVerticesBuffer = null;
        }
        if (uvBuffer != null)
        {
            uvBuffer.Release();
            uvBuffer = null;
        }
    }
}
