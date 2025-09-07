//using UnityEngine;

//namespace GlobeRTS.PlanetTerrain.Runtime.Geometry
//{
//    /// <summary>
//    /// Helper for computing the local axis frame for each face of a cube-sphere.
//    /// Provides the face normal (localUp) and two orthogonal axes (A/B) on that face.
//    /// </summary>
//    public static class FaceAxes
//    {
//        // Face index order: +Y, -Y, -X, +X, +Z, -Z
//        private static readonly Vector3[] s_FaceUp =
//        {
//            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
//        };

//        /// <summary>
//        /// Returns localUp and two orthogonal axes (axisA, axisB) for a given face (0..5).
//        /// </summary>
//        /// <param name="faceIndex">Face index in range 0..5.</param>
//        /// <param name="localUp">Out: face normal in local space.</param>
//        /// <param name="axisA">Out: first in-plane axis (orthogonal to localUp).</param>
//        /// <param name="axisB">Out: second in-plane axis (orthogonal to both localUp and axisA).</param>
//        public static void GetAxes(byte faceIndex, out Vector3 localUp, out Vector3 axisA, out Vector3 axisB)
//        {
//            // Defensive clamp (normally faceIndex should already be 0..5).
//            int i = Mathf.Clamp(faceIndex, 0, 5);
//            localUp = s_FaceUp[i];
//            axisA = new Vector3(localUp.y, localUp.z, localUp.x);
//            axisB = Vector3.Cross(localUp, axisA);
//        }
//    }
//}

using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.Geometry
{
    /// <summary>
    /// Helper for computing the local axis frame for each face of a cube-sphere.
    /// Provides the face normal (localUp) and two orthogonal axes (A/B) on that face.
    /// </summary>
    public static class FaceAxes
    {
        /// <summary>
        /// Canonical face normals order: +Y, -Y, -X, +X, +Z, -Z.
        /// Use this instead of duplicating arrays across classes.
        /// </summary>
        public static readonly Vector3[] FaceUp =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };

        /// <summary>
        /// Returns localUp and two orthogonal axes (axisA, axisB) for a given face (0..5).
        /// </summary>
        public static void GetAxes(byte faceIndex, out Vector3 localUp, out Vector3 axisA, out Vector3 axisB)
        {
            int i = Mathf.Clamp(faceIndex, 0, 5);
            localUp = FaceUp[i];
            axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            axisB = Vector3.Cross(localUp, axisA);
        }
    }
}