using System;

namespace GlobeRTS.PlanetTerrain.Runtime.Geometry
{
    /// <summary>
    /// Immutable key that identifies a single tile on the cube-sphere.
    /// </summary>
    [Serializable]
    public readonly struct TileKey : IEquatable<TileKey>
    {
        public readonly byte face;   // 0..5
        public readonly ushort x;    // 0..virtualTilesPerSide-1
        public readonly ushort y;    // 0..virtualTilesPerSide-1

        public TileKey(int face, int x, int y)
        {
            this.face = (byte)face;
            this.x = (ushort)x;
            this.y = (ushort)y;
        }

        public bool Equals(TileKey other) => face == other.face && x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is TileKey other && Equals(other);

        public override int GetHashCode()
        {
            // Large coprime multipliers to reduce collisions when hashing 3D grid coordinates.
            unchecked { return face * 73856093 ^ x * 19349663 ^ y * 83492791; }
        }

        public override string ToString() => $"F{face}_X{x}_Y{y}";
    }
}