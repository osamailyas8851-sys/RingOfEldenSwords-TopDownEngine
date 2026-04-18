namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Static bridge that carries the player's chosen LevelData and current
    /// difficulty tier across scene loads.
    /// Set by LevelSelectButton before the scene transition, read by
    /// PerkManager / spawners / generators in the gameplay scene.
    /// </summary>
    public static class LevelSelectConfig
    {
        /// <summary>
        /// The LevelData the player selected. Null if no selection has been made
        /// (e.g. when launching the gameplay scene directly from the editor).
        /// </summary>
        public static LevelData SelectedLevel { get; set; }

        /// <summary>
        /// The difficulty tier the player is about to play (1–10).
        /// Derived from <c>LevelProgressEntry.Progression + 1</c>.
        /// Gameplay systems use this to scale enemies, spawns, etc.
        /// Defaults to 1 when no selection has been made.
        /// </summary>
        public static int CurrentDifficulty { get; set; } = 1;

        /// <summary>
        /// Convenience check — true when a level has been selected.
        /// </summary>
        public static bool HasSelection => SelectedLevel != null;
    }
}
