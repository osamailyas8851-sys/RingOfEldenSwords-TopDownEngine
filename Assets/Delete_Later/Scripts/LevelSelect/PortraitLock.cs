using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Locks the screen to portrait orientation on this scene only.
    /// Runs in Awake so it beats Start/OnEnable of everything else.
    /// Disables landscape autorotation explicitly so the OS can't flip back.
    /// Note: the Unity Editor's Device Simulator window caches its own
    /// rotation per-device and must be rotated once manually; runtime
    /// <see cref="Screen.orientation"/> calls don't affect it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("TopDown Engine/GUI/Portrait Lock")]
    public class PortraitLock : MonoBehaviour
    {
        [Tooltip("Allow upside-down portrait as well.")]
        public bool AllowPortraitUpsideDown = false;

        protected virtual void Awake()
        {
            Apply();
        }

        protected virtual void OnEnable()
        {
            // Re-apply when re-enabled (e.g. after returning from a landscape scene)
            Apply();
        }

        protected virtual void Apply()
        {
            Screen.autorotateToLandscapeLeft   = false;
            Screen.autorotateToLandscapeRight  = false;
            Screen.autorotateToPortrait        = true;
            Screen.autorotateToPortraitUpsideDown = AllowPortraitUpsideDown;

            Screen.orientation = AllowPortraitUpsideDown
                ? ScreenOrientation.AutoRotation
                : ScreenOrientation.Portrait;
        }
    }
}
