#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SphereShaperEditorHelper
{
    static SphereShaperEditorHelper()
    {
        EditorApplication.delayCall += ProcessPendingDestruction;
    }

    static void ProcessPendingDestruction()
    {
        var shapers = Object.FindObjectsByType<SphereShaper>(FindObjectsSortMode.None);
        foreach (var shaper in shapers)
        {
            if (shaper != null)
                shaper.SafeEditorCleanup();
        }
    }
}
#endif