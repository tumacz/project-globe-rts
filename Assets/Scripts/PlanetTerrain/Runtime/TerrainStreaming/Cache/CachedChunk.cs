using System.Collections.Generic;
using UnityEngine;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;

namespace GlobeRTS.PlanetTerrain.Runtime.TerrainStreaming.Cache
{
    /// <summary>Description of a single cached tile.</summary>
    public sealed class CachedChunk
    {
        public TileKey key;

        public GameObject go;
        public Mesh mesh;
        public TileDeformer_GPU deformer;

        public long sizeBytes;
        public float lastAccessTime;
        public bool isActive;

        public int lastVisibleStamp;

        // LRU
        public LinkedListNode<TileKey> lruNode;
    }
}
