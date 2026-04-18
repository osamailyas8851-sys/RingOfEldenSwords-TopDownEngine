using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    // ── Serializable data structs for LevelData ─────────────────────────

    /// <summary>
    /// A prefab + quantity pair for general spawning (items, decorations, etc.).
    /// Mirrors TilemapLevelGenerator.SpawnData but lives in a ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class LevelSpawnData
    {
        [Tooltip("The prefab to spawn.")]
        public GameObject Prefab;

        [Tooltip("How many of this prefab to spawn.")]
        [Min(0)]
        public int Quantity = 1;
    }

    /// <summary>
    /// Enemy spawn entry with randomised orbit-weapon loadout.
    /// Mirrors TilemapLevelGeneratorExtended.DynamicSpawnData but lives in a ScriptableObject.
    /// Orbit weapon definitions are auto-loaded from Resources at runtime.
    /// </summary>
    [System.Serializable]
    public class LevelEnemySpawnData
    {
        [Tooltip("The enemy prefab to spawn.")]
        public GameObject AIPrefab;

        [Tooltip("How many of this enemy type to spawn.")]
        [Min(0)]
        public int Quantity = 1;

        [Header("Loadout Randomization")]
        [Tooltip("Minimum number of orbit weapons on this enemy.")]
        [Min(1)]
        public int MinWeaponCount = 1;

        [Tooltip("Maximum number of orbit weapons on this enemy.")]
        [Min(1)]
        public int MaxWeaponCount = 5;
    }

    /// <summary>
    /// Mirrors MMTilemapGeneratorLayer.MMTilemapGeneratorLayerSafeSpot.
    /// A rectangular exclusion zone where tiles won't be painted.
    /// </summary>
    [System.Serializable]
    public class LevelSafeSpot
    {
        [Tooltip("Start coordinate of the safe zone (grid units).")]
        public Vector2Int Start;

        [Tooltip("End coordinate of the safe zone (grid units).")]
        public Vector2Int End;
    }

    /// <summary>
    /// Complete per-layer configuration that can be stored in a ScriptableObject.
    /// 1-to-1 mapping with every serialized field on MMTilemapGeneratorLayer.
    /// Scene-bound Tilemap references are resolved at runtime by matching
    /// <see cref="Name"/> to a Tilemap GameObject name in the scene.
    /// </summary>
    [System.Serializable]
    public class LevelTileLayer
    {
        [Tooltip("Must match a Tilemap GameObject name in the scene (e.g. 'Ground', 'Walls').")]
        public string Name = "Layer";

        [Tooltip("Whether this layer is active. Inactive layers are skipped during generation.")]
        public bool Active = true;

        [Tooltip("The tile to paint.")]
        public TileBase Tile;

        [Tooltip("Algorithm used to generate this layer's grid.")]
        public MMTilemapGenerator.GenerateMethods GenerateMethod = MMTilemapGenerator.GenerateMethods.Perlin;

        // ── Seed ────────────────────────────────────────────────────────
        [Header("Seed")]
        [Tooltip("If true, this layer ignores the global seed and uses its own.")]
        public bool DoNotUseGlobalSeed = false;

        [Tooltip("Randomize this layer's own seed each generation.")]
        public bool RandomizeSeed = true;

        [Tooltip("Per-layer seed (used when DoNotUseGlobalSeed is true and RandomizeSeed is false).")]
        public int Seed = 1;

        // ── Per-Layer Grid Override ─────────────────────────────────────
        [Header("Per-Layer Grid Override")]
        [Tooltip("If true, this layer uses its own grid dimensions instead of the global ones.")]
        public bool OverrideGridSize = false;

        public int LayerGridWidth = 50;
        public int LayerGridHeight = 50;

        // ── Post-Processing ─────────────────────────────────────────────
        [Header("Post-Processing")]
        [Tooltip("Smoothen the result (removes spikes/isolated cells).")]
        public bool Smooth = false;

        [Tooltip("Invert the grid after generation.")]
        public bool InvertGrid = false;

        [Tooltip("How this layer merges with layers above it.")]
        public MMTilemapGeneratorLayer.FusionModes FusionMode = MMTilemapGeneratorLayer.FusionModes.Normal;

        // ── Bounds ──────────────────────────────────────────────────────
        [Header("Bounds")]
        public bool BoundsTop = false;
        public bool BoundsBottom = false;
        public bool BoundsLeft = false;
        public bool BoundsRight = false;

        // ── Safe Spots ──────────────────────────────────────────────────
        [Header("Safe Spots")]
        [Tooltip("Rectangular exclusion zones where tiles won't be painted. " +
                 "Useful for reserving spawn/exit areas.")]
        public List<LevelSafeSpot> SafeSpots = new List<LevelSafeSpot>();

        // ── Random Fill (Random method) ─────────────────────────────────
        [Header("Random Fill (Random method only)")]
        [Range(0, 100)]
        public int RandomFillPercentage = 50;

        // ── Random Walk (RandomWalk method) ─────────────────────────────
        [Header("Random Walk (RandomWalk method only)")]
        [Range(0, 100)]
        public int RandomWalkPercent = 50;
        public Vector2Int RandomWalkStartingPoint = Vector2Int.zero;
        public int RandomWalkMaxIterations = 1500;

        // ── Random Walk Ground (RandomWalkGround method) ────────────────
        [Header("Random Walk Ground (RandomWalkGround method only)")]
        public int RandomWalkGroundMinHeightDifference = 1;
        public int RandomWalkGroundMaxHeightDifference = 3;
        public int RandomWalkGroundMinFlatDistance = 1;
        public int RandomWalkGroundMaxFlatDistance = 3;
        public int RandomWalkGroundMaxHeight = 3;

        // ── Random Walk Avoider (RandomWalkAvoider method) ──────────────
        [Header("Random Walk Avoider (RandomWalkAvoider method only)")]
        [Range(0, 100)]
        public int RandomWalkAvoiderPercent = 50;
        public Vector2Int RandomWalkAvoiderStartingPoint = Vector2Int.zero;
        public int RandomWalkAvoiderObstaclesDistance = 1;
        public int RandomWalkAvoiderMaxIterations = 100;

        [Tooltip("Name of a Tilemap GameObject in the scene to avoid. " +
                 "Resolved at runtime just like the layer's own TargetTilemap.")]
        public string RandomWalkAvoiderObstaclesTilemapName;

        // ── Path (Path method) ──────────────────────────────────────────
        [Header("Path (Path method only)")]
        public Vector2Int PathStartPosition = Vector2Int.zero;
        public MMGridGeneratorPath.Directions PathDirection = MMGridGeneratorPath.Directions.BottomToTop;
        public int PathMinWidth = 2;
        public int PathMaxWidth = 4;
        public int PathDirectionChangeDistance = 2;
        [Range(0, 100)]
        public int PathWidthChangePercentage = 50;
        [Range(0, 100)]
        public int PathDirectionChangePercentage = 50;

        // ── Full (Full method) ──────────────────────────────────────────
        [Header("Full (Full method only)")]
        public bool FullGenerationFilled = true;

        // ── Copy (Copy method) ──────────────────────────────────────────
        [Header("Copy (Copy method only)")]
        [Tooltip("Name of a Tilemap GameObject in the scene to copy from. " +
                 "Resolved at runtime by name lookup.")]
        public string CopyTilemapName;
    }

    // ── LevelData ScriptableObject ──────────────────────────────────────

    /// <summary>
    /// Complete level configuration stored as a ScriptableObject.
    /// Drives grid generation, tile painting, prefab spawning, enemy loadouts,
    /// and difficulty — all from a single asset.
    /// Every serialized field across the MMTilemapGenerator → TilemapLevelGenerator
    /// → TilemapLevelGeneratorExtended chain is represented here.
    /// Scene-bound references (Grid, Tilemap, Transform, LevelManager) stay in the
    /// scene and are resolved at runtime by name matching.
    /// Create via: Create > TopDown Engine > Levels > Level Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "TopDown Engine/Levels/Level Data")]
    public class LevelData : ScriptableObject
    {
        // ── Display ─────────────────────────────────────────────────────
        [Header("Display")]
        [Tooltip("Name shown on the level select card.")]
        public string LevelName = "New Level";

        [TextArea]
        [Tooltip("Short description shown under the level name.")]
        public string Description;

        [Tooltip("Icon displayed on the level select card.")]
        public Sprite Icon;

        [Tooltip("Accent colour for the card border strip.")]
        public Color AccentColor = Color.white;

        // ── Scene ───────────────────────────────────────────────────────
        [Header("Scene")]
        [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
        public string SceneName = "ProceduralLevel";

        // ── Grid ────────────────────────────────────────────────────────
        [Header("Grid")]
        [Tooltip("Width range (min, max) of the generated grid in cells.")]
        public Vector2Int GridWidth = new Vector2Int(50, 50);

        [Tooltip("Height range (min, max) of the generated grid in cells.")]
        public Vector2Int GridHeight = new Vector2Int(50, 50);

        [Tooltip("Seed used by all layers. Set to 0 for random.")]
        public int GlobalSeed = 0;

        [Tooltip("Randomize the seed each time the level is generated.")]
        public bool RandomizeGlobalSeed = true;

        // ── Slow Render ─────────────────────────────────────────────────
        [Header("Slow Render")]
        [Tooltip("If true, the map draws progressively at runtime (eye-candy).")]
        public bool SlowRender = false;

        [Tooltip("Duration of the slow render animation in seconds.")]
        [Min(0.1f)]
        public float SlowRenderDuration = 1f;

        [Tooltip("Tween curve used for the slow render animation.")]
        public MMTweenType SlowRenderTweenType = new MMTweenType(MMTween.MMTweenCurve.EaseInOutCubic);

        // ── Tile Layers ─────────────────────────────────────────────────
        [Header("Tile Layers")]
        [Tooltip("Ordered list of tile layers. First layer paints first; " +
                 "later layers merge via their FusionMode. " +
                 "Tilemap targets are matched by layer Name → scene GameObject name.")]
        public List<LevelTileLayer> TileLayers = new List<LevelTileLayer>();

        // ── Prefab Spawning ─────────────────────────────────────────────
        [Header("Prefab Spawning")]
        [Tooltip("General prefabs to scatter (items, decorations, pickups).")]
        public List<LevelSpawnData> PrefabsToSpawn = new List<LevelSpawnData>();

        [Tooltip("Minimum distance from already-spawned objects when placing prefabs.")]
        [Min(0.5f)]
        public float PrefabsSpawnMinDistance = 2f;

        [Tooltip("Minimum distance between the player spawn and the exit.")]
        [Min(1f)]
        public float MinDistanceFromSpawnToExit = 2f;

        // ── Enemy Spawning ──────────────────────────────────────────────
        [Header("Enemy Spawning")]
        [Tooltip("Enemy types to spawn with randomised orbit-weapon loadouts. " +
                 "Weapons are auto-loaded from Resources and capped by MaxSwordTier.")]
        public List<LevelEnemySpawnData> EnemySpawns = new List<LevelEnemySpawnData>();

        // ── Difficulty ──────────────────────────────────────────────────
        [Header("Difficulty")]
        [Tooltip("Base difficulty tier for this level (1 = easiest, 10 = hardest). " +
                 "The actual difficulty the player faces is driven by their progression " +
                 "(Progression + 1), but this sets the floor. A level with BaseDifficulty 3 " +
                 "starts the player at difficulty 3 even on their first attempt.")]
        [Range(1, 10)]
        public int BaseDifficulty = 1;

        [Tooltip("Number of enemy waves in this level at difficulty 1. " +
                 "Scales up with the current difficulty tier.")]
        [Min(1)]
        public int WaveCount = 5;

        // ── Progression Cap ─────────────────────────────────────────────
        [Header("Progression Cap")]
        [Tooltip("Highest sword tier the player can reach (1-based). " +
                 "Set to the total number of sword assets for no cap.")]
        [Min(1)]
        public int MaxSwordTier = 48;

        [Tooltip("Resources sub-folder for auto-loading sword definitions.")]
        public string SwordsResourcePath = "Weapons/Swords";

        // ── Computed Properties ─────────────────────────────────────────

        /// <summary>
        /// Returns a human-readable difficulty label based on <see cref="BaseDifficulty"/>.
        /// </summary>
        public string DifficultyLabel
        {
            get
            {
                if (BaseDifficulty <= 3)  return "Easy";
                if (BaseDifficulty <= 6)  return "Normal";
                if (BaseDifficulty <= 8)  return "Hard";
                return "Nightmare";
            }
        }
    }
}
