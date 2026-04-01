
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;

public class ClearAllTilemaps : EditorWindow
{
    [MenuItem("Tools/Clear All Tilemaps")]
    public static void ClearTilemaps()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (Tilemap tilemap in tilemaps)
        {
            Undo.RecordObject(tilemap, "Clear Tilemap");
            tilemap.ClearAllTiles();
        }
        Debug.Log($"[ClearAllTilemaps] Cleared {tilemaps.Length} tilemaps.");
    }
}
#endif
