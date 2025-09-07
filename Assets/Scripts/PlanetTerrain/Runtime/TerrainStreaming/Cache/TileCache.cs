using System;
using System.Collections.Generic;
using UnityEngine;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;

namespace GlobeRTS.PlanetTerrain.Runtime.TerrainStreaming.Cache
{
    /// <summary>
    /// Tile cache: dictionary + LRU, central API for activation/deactivation,
    /// sweeping of non-visible tiles, and capacity enforcement.
    /// Additionally: simple GameObject pool for tiles (F3) + telemetry (F9).
    /// F12: centralized Activate/Deactivate + events.
    /// </summary>
    public sealed class TileCache
    {
        private readonly Dictionary<TileKey, CachedChunk> _map = new(512);
        private readonly LinkedList<TileKey> _lru = new();
        private readonly Stack<GameObject> _goPool = new(128);

        private readonly int _maxTiles;
        private readonly bool _keepAll;
        private readonly bool _debug;

        // Shared MPB used when returning renderers to a neutral state
        private static readonly MaterialPropertyBlock _tmpMpb = new();

        public TileCache(int maxTiles, bool keepAllGenerated, bool debugLogs = false)
        {
            _maxTiles = Mathf.Max(1, maxTiles);
            _keepAll = keepAllGenerated;
            _debug = debugLogs;
        }

        public int Count => _map.Count;
        public int PoolSize => _goPool.Count;

        public IEnumerable<KeyValuePair<TileKey, CachedChunk>> Pairs => _map;

        public bool Contains(in TileKey key) => _map.ContainsKey(key);
        public bool TryGet(in TileKey key, out CachedChunk chunk) => _map.TryGetValue(key, out chunk);

        public bool Put(CachedChunk chunk)
        {
            if (_map.ContainsKey(chunk.key))
                return false;

            chunk.lruNode = _lru.AddLast(chunk.key);
            _map[chunk.key] = chunk;

#if UNITY_EDITOR
            if (_debug) Debug.Log($"[TileCache] Put {chunk.key} (active={chunk.isActive}) count={_map.Count} pool={_goPool.Count}");
#endif
            return true;
        }

        /// <summary>Invoked when a chunk becomes active.</summary>
        public event Action<TileKey, CachedChunk> ChunkActivated;

        /// <summary>Invoked when a chunk is deactivated.</summary>
        public event Action<TileKey, CachedChunk> ChunkDeactivated;

        public bool Activate(in TileKey key, float timeNow, int currentVisStamp)
        {
            if (!_map.TryGetValue(key, out var cc) || cc.go == null) return false;

            if (!cc.go.activeSelf) cc.go.SetActive(true);

            cc.isActive = true;
            cc.lastAccessTime = timeNow;
            cc.lastVisibleStamp = currentVisStamp;
            TouchLru(cc);

            ChunkActivated?.Invoke(key, cc);
            return true;
        }

        public bool Deactivate(in TileKey key, float timeNow)
        {
            if (!_map.TryGetValue(key, out var cc) || cc.go == null) return false;

            if (cc.go.activeSelf) cc.go.SetActive(false);

            cc.isActive = false;
            cc.lastAccessTime = timeNow;
            TouchLru(cc);

            ChunkDeactivated?.Invoke(key, cc);
            return true;
        }

        [Obsolete("Use Activate(key, now, visStamp) or Deactivate(key, now) instead.")]
        public void SetActive(in TileKey key, bool active, float timeNow)
        {
            if (active) Activate(key, timeNow, currentVisStamp: 0);
            else Deactivate(key, timeNow);
        }

        public int StatsPooledThisFrame { get; private set; }
        public void ResetFrameStats() => StatsPooledThisFrame = 0;

        public void SweepDeactivateStale(int currentVisStamp, float timeNow)
        {
            _toTurnOff.Clear();

            foreach (var pair in _map)
            {
                var cc = pair.Value;
                if (cc.isActive && cc.lastVisibleStamp != currentVisStamp)
                    _toTurnOff.Add(pair.Key);
            }

            for (int i = 0; i < _toTurnOff.Count; i++)
            {
                var k = _toTurnOff[i];
                Deactivate(k, timeNow);
            }
        }

        public void EnforceCapacity()
        {
            if (_keepAll) return;
            if (_map.Count <= _maxTiles) return;

            var node = _lru.First;
            while (_map.Count > _maxTiles && node != null)
            {
                var next = node.Next;
                var key = node.Value;

                if (_map.TryGetValue(key, out var cc) && !cc.isActive)
                {
                    ReturnToPool_Internal(cc);
                    _map.Remove(key);
                    _lru.Remove(node);

#if UNITY_EDITOR
                    if (_debug) Debug.Log($"[TileCache] Pooled {key} (map={_map.Count}, pool={_goPool.Count})");
#endif
                }

                node = next;
            }
        }

        public GameObject RentFromPool()
        {
            if (_goPool.Count == 0) return null;
            var go = _goPool.Pop();
            if (go == null) return null;
            return go;
        }

        public void DestroyAll()
        {
            foreach (var pair in _map)
            {
                var cc = pair.Value;
                if (cc.go != null) UnityEngine.Object.Destroy(cc.go);
            }
            _map.Clear();
            _lru.Clear();

            while (_goPool.Count > 0)
            {
                var go = _goPool.Pop();
                if (go != null) UnityEngine.Object.Destroy(go);
            }

            StatsPooledThisFrame = 0;
        }

        private void ReturnToPool_Internal(CachedChunk cc)
        {
            if (cc == null || cc.go == null) return;

            cc.go.SetActive(false);

            if (cc.deformer != null) cc.deformer.enabled = false;

            // Avoid GetComponent allocation: use TryGetComponent
            if (cc.go.TryGetComponent<MeshRenderer>(out var mr) && mr != null)
            {
                _tmpMpb.Clear();
                mr.SetPropertyBlock(_tmpMpb);
            }

            _goPool.Push(cc.go);
            StatsPooledThisFrame++;

            cc.deformer = null;
            cc.mesh = null;
            cc.go = null;
            cc.lruNode = null;
        }

        private void TouchLru(CachedChunk cc)
        {
            if (cc.lruNode == null || cc.lruNode.List != _lru) return;
            if (cc.lruNode != _lru.Last)
            {
                _lru.Remove(cc.lruNode);
                cc.lruNode = _lru.AddLast(cc.lruNode.Value);
            }
        }

        private readonly List<TileKey> _toTurnOff = new(256);
    }
}