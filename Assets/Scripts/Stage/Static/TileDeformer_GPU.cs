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

    private MeshFilter meshFilter;
    private Mesh mesh;
    private Vector3[] originalVertices;
    private int kernelID;

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    public void DeformMesh(Mesh meshToDeform)
    {
        if (!ValidateSetup(meshToDeform))
        {
            return;
        }

        SetupBuffers();

        if (originalVerticesBuffer == null || deformedVerticesBuffer == null)
        {
            return;
        }

        int threadGroupsX = Mathf.CeilToInt(originalVertices.Length / 64.0f);

        computeShader.Dispatch(kernelID, threadGroupsX, 1, 1);

        Vector3[] newVertices = new Vector3[originalVertices.Length];
        deformedVerticesBuffer.GetData(newVertices);

        mesh.vertices = newVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
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
        kernelID = computeShader.FindKernel("CSMain");

        return true;
    }
    private void SetupBuffers()
    {
        ReleaseBuffers();

        int vertexCount = originalVertices.Length;
        originalVerticesBuffer = new ComputeBuffer(vertexCount, 12);
        deformedVerticesBuffer = new ComputeBuffer(vertexCount, 12);

        originalVerticesBuffer.SetData(originalVertices);

        computeShader.SetBuffer(kernelID, "originalVertices", originalVerticesBuffer);
        computeShader.SetBuffer(kernelID, "deformedVertices", deformedVerticesBuffer);

        computeShader.SetTexture(kernelID, "heightMap", heightMap);

        computeShader.SetFloat("heightScale", heightScale);
        computeShader.SetFloat("sphereRadius", sphereRadius);
        computeShader.SetFloat("globalScale", globalScale);
        computeShader.SetInt("vertexCount", vertexCount);
        computeShader.SetInt("heightMapWidth", heightMap.width);
        computeShader.SetInt("heightMapHeight", heightMap.height);
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
    }
}