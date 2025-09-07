using UnityEngine;

[ExecuteAlways]
public class TileDeformerConfigurator : MonoBehaviour
{
    public Texture2D heightMap;
    public Rect uvRect;

    private HeightmapMeshDeformer _deformer;

    private void Awake()
    {
        _deformer = GetComponent<HeightmapMeshDeformer>();
    }

    private void Start()
    {
        UpdateMeshDeformer();
    }

    public void Setup(Texture2D newHeightMap, Rect newUvRect)
    {
        this.heightMap = newHeightMap;
        this.uvRect = newUvRect;
        UpdateMeshDeformer();
    }

    private void UpdateMeshDeformer()
    {
        if (_deformer == null)
        {
            _deformer = GetComponent<HeightmapMeshDeformer>();
        }

        if (_deformer != null)
        {
            _deformer.heightMap = heightMap;
            _deformer.uvRect = uvRect;
            _deformer.Regenerate();
        }
        else
        {
            Debug.LogWarning($"TileDeformerConfigurator on {gameObject.name} requires a HeightmapMeshDeformer component!", this);
        }
    }
}