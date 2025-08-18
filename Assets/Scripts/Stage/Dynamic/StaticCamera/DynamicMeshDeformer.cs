using UnityEngine;

public class DynamicMeshDeformer : MonoBehaviour
{
    [Header("General Settings")]
    [SerializeField] private Material _deformingMaterial;
    [SerializeField, Range(1, 32)] public int _tilesPerSide = 2;
    [SerializeField, Range(2, 256)] private int _tileMeshResolution = 256;
    [SerializeField, Min(0.1f)] private float _sphereRadius = 1f;
    [SerializeField] private float _heightScale = 10f;
    [SerializeField] private float _uniformGlobalScale = 1f;

    [Header("Data Textures")]
    [SerializeField] private Texture2D _heightMap;
    [SerializeField] private Texture2D _provinceMap;

    [Header("Deformation Mode")]
    [Tooltip("If true, deformation is handled by GPU via shader. If false, by CPU in C#.")]
    [SerializeField] private bool _useGpuDeformation = true;

    [Header("GPU Deformation Settings")]
    [Tooltip("Assign the Compute Shader used for mesh deformation when GPU mode is active.")]
    [SerializeField] private ComputeShader _deformationComputeShader;

    private SphereFace[] _sphereFaces;
    private MeshFilter[] _faceMeshFilters;

    private readonly Vector3[] _faceDirections = {
        Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
    };

    private Vector3 _currentGlobalMapPosition = Vector3.zero;

    private void OnEnable()
    {
        InitializeDeformer();
    }

    private void OnDisable()
    {
        CleanupDeformer();
    }

    private void Update()
    {
        if (_useGpuDeformation)
        {
            UpdateGpuDeformation(_currentGlobalMapPosition);
        }
        else
        {
            UpdateCpuDeformation(_currentGlobalMapPosition);
        }
    }

    private void InitializeDeformer()
    {
        CleanupDeformer();

        _sphereFaces = new SphereFace[_faceDirections.Length];
        _faceMeshFilters = new MeshFilter[_faceDirections.Length];

        if (_deformingMaterial != null)
        {
            _deformingMaterial.SetTexture("_HeightMap", _heightMap);
            _deformingMaterial.SetTexture("_ProvinceMap", _provinceMap);
            _deformingMaterial.SetFloat("_SphereRadius", _sphereRadius);
            _deformingMaterial.SetFloat("_HeightScale", _heightScale);
            _deformingMaterial.SetFloat("_UniformGlobalScale", _uniformGlobalScale);
        }
        else
        {
            Debug.LogError("Deforming Material is not assigned to DynamicMeshDeformer!");
            return;
        }

        TileDeformer_GPU masterGpuDeformerInstance = null;
        if (_useGpuDeformation)
        {
            if (_deformationComputeShader == null)
            {
                Debug.LogError("GPU Deformation is enabled but Compute Shader is not assigned to DynamicMeshDeformer!");
                return;
            }
            masterGpuDeformerInstance = GetComponent<TileDeformer_GPU>();
            if (masterGpuDeformerInstance == null)
            {
                masterGpuDeformerInstance = gameObject.AddComponent<TileDeformer_GPU>();
            }
            masterGpuDeformerInstance.computeShader = _deformationComputeShader;
        }

        for (int i = 0; i < _faceDirections.Length; i++)
        {
            GameObject faceGO = new GameObject($"Face_{_faceDirections[i].ToString().Replace(" ", "")}");
            faceGO.transform.SetParent(transform);

            MeshFilter mf = faceGO.AddComponent<MeshFilter>();
            MeshRenderer mr = faceGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _deformingMaterial;
            mf.sharedMesh = new Mesh();
            _faceMeshFilters[i] = mf;

            _sphereFaces[i] = new SphereFace(_tilesPerSide, _tileMeshResolution, _faceDirections[i]);

            if (_useGpuDeformation)
            {
                _sphereFaces[i].InitializeTiles(
                    faceGO.transform,
                    _deformingMaterial,
                    _heightMap,
                    _heightScale,
                    _sphereRadius,
                    _uniformGlobalScale,
                    masterGpuDeformerInstance
                );
                _sphereFaces[i].ConstructFaceMeshes(_useGpuDeformation);
            }
            else 
            {
                _sphereFaces[i].InitializeTiles(
                    faceGO.transform,
                    _deformingMaterial,
                    _heightMap,
                    _heightScale,
                    _sphereRadius,
                    _uniformGlobalScale
                );

                _sphereFaces[i].ConstructFaceMeshes(_useGpuDeformation);
                mf.sharedMesh = null;
                mr.enabled = false;
            }
        }
    }

    private void CleanupDeformer()
    {
        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            GameObject child = transform.GetChild(j).gameObject;
            if (!Application.isPlaying) GameObject.DestroyImmediate(child);
            else GameObject.Destroy(child);
        }
        TileDeformer_GPU existingGpuDeformer = GetComponent<TileDeformer_GPU>();
        if (existingGpuDeformer != null)
        {
            if (!Application.isPlaying) DestroyImmediate(existingGpuDeformer);
            else Destroy(existingGpuDeformer);
        }
    }

    //private void GenerateFlatBaseMeshForFace(Mesh mesh, int resolution, Vector3 localUp)
    //{
    //    mesh.Clear();
    //    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

    //    Vector3[] vertices = new Vector3[resolution * resolution];
    //    Vector2[] uvs = new Vector2[vertices.Length];
    //    int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
    //    int triIndex = 0;

    //    Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
    //    Vector3 axisB = Vector3.Cross(localUp, axisA);

    //    for (int y = 0; y < resolution; y++)
    //    {
    //        for (int x = 0; x < resolution; x++)
    //        {
    //            int i = x + y * resolution;
    //            Vector2 percent = new Vector2(x, y) / (resolution - 1);

    //            vertices[i] = (percent.x - 0.5f) * 2f * axisA + (percent.y - 0.5f) * 2f * axisB;
    //            uvs[i] = percent;
    //        }
    //    }

    //    for (int y = 0; y < resolution - 1; y++)
    //    {
    //        for (int x = 0; x < resolution - 1; x++)
    //        {
    //            int i = x + y * resolution;
    //            triangles[triIndex++] = i;
    //            triangles[triIndex++] = i + resolution + 1;
    //            triangles[triIndex++] = i + resolution;

    //            triangles[triIndex++] = i;
    //            triangles[triIndex++] = i + 1;
    //            triangles[triIndex++] = i + resolution + 1;
    //        }
    //    }

    //    mesh.vertices = vertices;
    //    mesh.triangles = triangles;
    //    mesh.uv = uvs;
    //    mesh.RecalculateNormals();
    //}


    private void UpdateGpuDeformation(Vector3 currentGlobalMapPosition)
    {
        _deformingMaterial.SetVector("_GlobalMapPosition", currentGlobalMapPosition);
        _deformingMaterial.SetVector("_PlanetCenterOffset", transform.position);

        foreach (MeshFilter mf in _faceMeshFilters)
        {
            if (mf != null)
            {
                mf.GetComponent<MeshRenderer>().enabled = false;
            }
        }

        foreach (SphereFace face in _sphereFaces)
        {
            face?.SetTileVisibility(true);
        }
    }

    private void UpdateCpuDeformation(Vector3 currentGlobalMapPosition)
    {
        foreach (MeshFilter mf in _faceMeshFilters)
        {
            if (mf != null)
            {
                mf.GetComponent<MeshRenderer>().enabled = false;
            }
        }
        foreach (SphereFace face in _sphereFaces)
        {
            face?.SetTileVisibility(true);
        }
    }
}