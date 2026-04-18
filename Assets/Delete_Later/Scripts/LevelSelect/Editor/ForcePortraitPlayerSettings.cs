using UnityEditor;
using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Editor tool to force the project's default screen orientation to Portrait.
    /// Unity reverts hand-edits to ProjectSettings.asset sometimes — using the
    /// official PlayerSettings API makes the change stick.
    /// Tools > Level Select > Force Portrait Orientation
    /// </summary>
    public static class ForcePortraitPlayerSettings
    {
        [MenuItem("Tools/Level Select/Force Portrait Orientation")]
        public static void ForcePortrait()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait           = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft      = false;
            PlayerSettings.allowedAutorotateToLandscapeRight     = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[ForcePortraitPlayerSettings] Player Settings now locked to Portrait. " +
                      "Close and reopen the Simulator window (or click its Rotate button) so it picks up the change.");
        }
    }
}
