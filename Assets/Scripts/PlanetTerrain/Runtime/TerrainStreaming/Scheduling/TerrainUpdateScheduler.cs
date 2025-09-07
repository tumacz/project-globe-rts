using System;
using System.Collections.Generic;
using GlobeRTS.PlanetTerrain.Runtime.Geometry;

namespace GlobeRTS.PlanetTerrain.Runtime.Update
{
    /// <summary>
    /// Simple FIFO queue with a set to avoid enqueuing duplicate keys.
    /// </summary>
    public sealed class TerrainUpdateScheduler
    {
        private readonly Queue<TileKey> _queue = new(256);
        private readonly HashSet<TileKey> _enqueued = new();

        public int Count => _queue.Count;

        public bool EnqueueIfAbsent(in TileKey key)
        {
            if (_enqueued.Contains(key)) return false;
            _queue.Enqueue(key);
            _enqueued.Add(key);
            return true;
        }

        /// <summary>Dequeues up to 'budget' items and calls 'create' for each.</summary>
        public void Drain(int budget, Func<TileKey, bool> create)
        {
            int done = 0;
            while (done < budget && _queue.Count > 0)
            {
                var key = _queue.Dequeue();
                _enqueued.Remove(key);
                if (create(key)) done++;
            }
        }

        public void Clear()
        {
            _queue.Clear();
            _enqueued.Clear();
        }
    }
}