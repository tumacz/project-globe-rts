#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public static class TileGridSpawnerEditorHelper
{
    static TileGridSpawnerEditorHelper()
    {
        EditorApplication.delayCall += ProcessPendingDestruction;
    }

    static void ProcessPendingDestruction()
    {
        if (TileGridSpawner._pendingDestroyEditor.Count > 0)
        {
            List<GameObject> toDestroyNow = new List<GameObject>(TileGridSpawner._pendingDestroyEditor);
            TileGridSpawner._pendingDestroyEditor.Clear();

            foreach (var obj in toDestroyNow)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }

        EditorApplication.delayCall += ProcessPendingDestruction;
    }
}
#endif