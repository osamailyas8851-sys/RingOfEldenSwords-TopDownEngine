using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.LevelGeneration
{
    /// <summary>
    /// Extended procedural level generator that can be driven entirely from a
    /// <see cref="LevelData"/> ScriptableObject selected via the level-select screen.
    ///
    /// Inheritance chain:
    ///   MMTilemapGenerator → TilemapLevelGenerator → TilemapLevelGeneratorExtended
    ///
    /// When <see cref="UseSelectedLevelData"/> is true and a level has been selected
    /// via <see cref="LevelSelectConfig"/>, every generation parameter (grid, layers,
    /// spawns, enemies, slow-render, etc.) is overwritten from the ScriptableObject
    /// BEFORE <c>base.Generate()</c> runs. Scene-bound references (Grid, Tilemaps,
    /// Transforms, LevelManager) stay in the scene Inspector and are never stored in
    /// the SO — tile layers are matched by name at runtime.
    ///
    /// Design follows TDE conventions:
    /// - public/serialized fields with [Header] + [Tooltip]
    /// - virtual methods for easy subclass overrides
    /// - coroutine-based deferred spawn (physics frame wait)
    /// </summary>
    [AddComponentMenu("TopDown Engine/Level Generation/Tilemap Level Generator Extended")]
    public class TilemapLevelGeneratorExtended : TilemapLevelGenerator
    {
        // ── Nested Types ────────────────────────────────────────────────────

        [System.Serializable]
        public class DynamicSpawnData
        {
            [Tooltip("The base enemy prefab to spawn")]
            public GameObject AIPrefab;

            [Tooltip("How many of this specific enemy to spawn")]
            public int Quantity = 1;

            [Header("Loadout Randomization")]
            public int MinWeaponCount = 1;
            public int MaxWeaponCount = 5;

            [Tooltip("Leave empty to auto-fill from the shared sword pool loaded from Resources.")]
            public List<OrbitWeaponDefinition> PossibleOrbitWeaponDefinitions;
        }

        // ── Inspector Fields ────────────────────────────────────────────────

        [Header("Level Data Override")]
        [Tooltip("If true, automatically applies LevelSelectConfig.SelectedLevel " +
                 "before generation. Disable to configure purely from the Inspector.")]
        [SerializeField] protected bool UseSelectedLevelData = true;

        [Header("Auto-Load Swords (Resources/)")]
        [Tooltip("Resources sub-folder to auto-load OrbitWeaponDefinition assets from. " +
                 "Leave empty to use the manual lists per spawn entry.")]
        [SerializeField] protected string SwordsResourcePath = "Weapons/Swords";

        [Header("Dynamic Spawning Settings")]
        [Tooltip("Custom advanced AI spawning. Runs AFTER the default TDE PrefabsToSpawn.")]
        public List<DynamicSpawnData> AIPrefabsToSpawn;

        // ── Runtime State ───────────────────────────────────────────────────

        /// <summary>
        /// Shared sword pool, auto-loaded and capped by MaxSwordTier.
        /// Any DynamicSpawnData with an empty PossibleOrbitWeaponDefinitions list uses this.
        /// </summary>
        protected List<OrbitWeaponDefinition> _sharedSwordPool = new List<OrbitWeaponDefinition>();

        /// <summary>
        /// Cache of scene Tilemaps keyed by their GameObject name.
        /// Built once per generate pass so layers can be matched by name from LevelData.
        /// </summary>
        protected Dictionary<string, Tilemap> _sceneTilemapsByName;

        // =====================================================================
        //  GENERATE
        // =====================================================================

        public override void Generate()
        {
            // 0. Apply LevelData from the level-select flow (before anything else)
            if (UseSelectedLevelData)
                ApplyLevelConfig();

            // 1. Auto-load shared sword pool (respects MaxSwordTier cap)
            LoadSharedSwordPool();

            // 2. Let TDE do all its normal map generation
            //    (MMTilemapGenerator.Generate → TilemapLevelGenerator.Generate)
            base.Generate();

            if (Application.isPlaying)
            {
                // 3. Spawn AI next physics frame so composite collider is updated
                StartCoroutine(SpawnAINextFrame());
            }
        }

        // =====================================================================
        //  APPLY LEVEL CONFIG FROM ScriptableObject
        // =====================================================================

        /// <summary>
        /// Reads the selected <see cref="LevelData"/> from <see cref="LevelSelectConfig"/>
        /// and overwrites every generation parameter on this component before
        /// <c>base.Generate()</c> runs.
        /// If no level is selected (e.g. launched from the editor) keeps Inspector values.
        /// </summary>
        protected virtual void ApplyLevelConfig()
        {
            if (!LevelSelectConfig.HasSelection) return;

            LevelData level = LevelSelectConfig.SelectedLevel;
            Debug.Log($"[TilemapLevelGeneratorExtended] Applying LevelData '{level.LevelName}'");

            // ── Grid (MMTilemapGenerator) ───────────────────────────────
            GridWidth           = level.GridWidth;
            GridHeight          = level.GridHeight;
            GlobalSeed          = level.GlobalSeed;
            RandomizeGlobalSeed = level.RandomizeGlobalSeed;

            // ── Slow Render (MMTilemapGenerator) ────────────────────────
            SlowRender          = level.SlowRender;
            SlowRenderDuration  = level.SlowRenderDuration;
            SlowRenderTweenType = level.SlowRenderTweenType;

            // ── Tile Layers (MMTilemapGenerator.Layers) ─────────────────
            ApplyTileLayers(level);

            // ── Prefab Spawning (TilemapLevelGenerator) ─────────────────
            ApplyPrefabSpawns(level);
            PrefabsSpawnMinDistance    = level.PrefabsSpawnMinDistance;
            MinDistanceFromSpawnToExit = level.MinDistanceFromSpawnToExit;

            // ── Enemy Spawning (TilemapLevelGeneratorExtended) ──────────
            ApplyEnemySpawns(level);

            // ── Swords Resource Path ────────────────────────────────────
            if (!string.IsNullOrEmpty(level.SwordsResourcePath))
                SwordsResourcePath = level.SwordsResourcePath;
        }

        // ── Prefab Spawns ───────────────────────────────────────────────

        /// <summary>
        /// Converts <see cref="LevelData.PrefabsToSpawn"/> →
        /// <see cref="TilemapLevelGenerator.SpawnData"/> list.
        /// </summary>
        protected virtual void ApplyPrefabSpawns(LevelData level)
        {
            // Empty SO list → keep whatever the Inspector already has (fallback)
            if (level.PrefabsToSpawn == null || level.PrefabsToSpawn.Count == 0) return;

            PrefabsToSpawn = new List<SpawnData>();
            foreach (LevelSpawnData lsd in level.PrefabsToSpawn)
            {
                if (lsd.Prefab == null) continue;
                PrefabsToSpawn.Add(new SpawnData
                {
                    Prefab   = lsd.Prefab,
                    Quantity = lsd.Quantity
                });
            }
        }

        // ── Enemy Spawns ────────────────────────────────────────────────

        /// <summary>
        /// Converts <see cref="LevelData.EnemySpawns"/> →
        /// <see cref="DynamicSpawnData"/> list.
        /// </summary>
        protected virtual void ApplyEnemySpawns(LevelData level)
        {
            // Empty SO list → keep whatever the Inspector already has (fallback)
            if (level.EnemySpawns == null || level.EnemySpawns.Count == 0) return;

            AIPrefabsToSpawn = new List<DynamicSpawnData>();
            foreach (LevelEnemySpawnData esd in level.EnemySpawns)
            {
                if (esd.AIPrefab == null) continue;
                AIPrefabsToSpawn.Add(new DynamicSpawnData
                {
                    AIPrefab       = esd.AIPrefab,
                    Quantity       = esd.Quantity,
                    MinWeaponCount = esd.MinWeaponCount,
                    MaxWeaponCount = esd.MaxWeaponCount,
                    // Empty list → falls back to _sharedSwordPool at spawn time
                    PossibleOrbitWeaponDefinitions = new List<OrbitWeaponDefinition>()
                });
            }
        }

        // ── Tile Layers ─────────────────────────────────────────────────

        /// <summary>
        /// Maps <see cref="LevelData.TileLayers"/> onto <see cref="MMTilemapGenerator.Layers"/>.
        /// Scene Tilemaps are matched by GameObject name → <see cref="LevelTileLayer.Name"/>.
        /// </summary>
        protected virtual void ApplyTileLayers(LevelData level)
        {
            if (level.TileLayers == null || level.TileLayers.Count == 0) return;

            // Build a name → Tilemap lookup from every Tilemap in the scene
            BuildSceneTilemapCache();

            // Ensure Layers container exists
            if (Layers == null)
                Layers = new MMTilemapGeneratorLayerList();

            Layers.Clear();

            foreach (LevelTileLayer ltl in level.TileLayers)
            {
                MMTilemapGeneratorLayer layer = new MMTilemapGeneratorLayer();

                // ── Identity ────────────────────────────────────────────
                layer.Name   = ltl.Name;
                layer.Active = ltl.Active;

                // ── Tilemap binding (scene lookup by name) ──────────────
                layer.TargetTilemap = ResolveTilemap(ltl.Name);

                // ── Tile ────────────────────────────────────────────────
                layer.Tile = ltl.Tile;

                // ── Generation Method ───────────────────────────────────
                layer.GenerateMethod = ltl.GenerateMethod;

                // ── Seed ────────────────────────────────────────────────
                layer.DoNotUseGlobalSeed = ltl.DoNotUseGlobalSeed;
                layer.RandomizeSeed      = ltl.RandomizeSeed;
                layer.Seed               = ltl.Seed;

                // ── Per-layer grid override ─────────────────────────────
                layer.OverrideGridSize = ltl.OverrideGridSize;
                layer.GridWidth        = ltl.LayerGridWidth;
                layer.GridHeight       = ltl.LayerGridHeight;

                // ── Post-processing ─────────────────────────────────────
                layer.Smooth     = ltl.Smooth;
                layer.InvertGrid = ltl.InvertGrid;
                layer.FusionMode = ltl.FusionMode;

                // ── Bounds ──────────────────────────────────────────────
                layer.BoundsTop    = ltl.BoundsTop;
                layer.BoundsBottom = ltl.BoundsBottom;
                layer.BoundsLeft   = ltl.BoundsLeft;
                layer.BoundsRight  = ltl.BoundsRight;

                // ── Safe Spots ──────────────────────────────────────────
                ApplyLayerSafeSpots(layer, ltl);

                // ── Random Fill ─────────────────────────────────────────
                layer.RandomFillPercentage = ltl.RandomFillPercentage;

                // ── Random Walk ─────────────────────────────────────────
                layer.RandomWalkPercent       = ltl.RandomWalkPercent;
                layer.RandomWalkStartingPoint = ltl.RandomWalkStartingPoint;
                layer.RandomWalkMaxIterations = ltl.RandomWalkMaxIterations;

                // ── Random Walk Ground ──────────────────────────────────
                layer.RandomWalkGroundMinHeightDifference = ltl.RandomWalkGroundMinHeightDifference;
                layer.RandomWalkGroundMaxHeightDifference = ltl.RandomWalkGroundMaxHeightDifference;
                layer.RandomWalkGroundMinFlatDistance     = ltl.RandomWalkGroundMinFlatDistance;
                layer.RandomWalkGroundMaxFlatDistance     = ltl.RandomWalkGroundMaxFlatDistance;
                layer.RandomWalkGroundMaxHeight           = ltl.RandomWalkGroundMaxHeight;

                // ── Random Walk Avoider ─────────────────────────────────
                layer.RandomWalkAvoiderPercent           = ltl.RandomWalkAvoiderPercent;
                layer.RandomWalkAvoiderStartingPoint     = ltl.RandomWalkAvoiderStartingPoint;
                layer.RandomWalkAvoiderObstaclesDistance  = ltl.RandomWalkAvoiderObstaclesDistance;
                layer.RandomWalkAvoiderMaxIterations      = ltl.RandomWalkAvoiderMaxIterations;
                layer.RandomWalkAvoiderObstaclesTilemap   = ResolveTilemap(ltl.RandomWalkAvoiderObstaclesTilemapName);

                // ── Path ────────────────────────────────────────────────
                layer.PathStartPosition            = ltl.PathStartPosition;
                layer.PathDirection                 = ltl.PathDirection;
                layer.PathMinWidth                  = ltl.PathMinWidth;
                layer.PathMaxWidth                  = ltl.PathMaxWidth;
                layer.PathDirectionChangeDistance   = ltl.PathDirectionChangeDistance;
                layer.PathWidthChangePercentage     = ltl.PathWidthChangePercentage;
                layer.PathDirectionChangePercentage = ltl.PathDirectionChangePercentage;

                // ── Full ────────────────────────────────────────────────
                layer.FullGenerationFilled = ltl.FullGenerationFilled;

                // ── Copy ────────────────────────────────────────────────
                layer.CopyTilemap = ResolveTilemap(ltl.CopyTilemapName);

                // Mark as initialized so TDE won't reset defaults
                layer.Initialized = true;

                Layers.Add(layer);
            }

            Debug.Log($"[TilemapLevelGeneratorExtended] Applied {Layers.Count} tile layers from LevelData");
        }

        /// <summary>
        /// Converts <see cref="LevelTileLayer.SafeSpots"/> →
        /// <see cref="MMTilemapGeneratorLayer.MMTilemapGeneratorLayerSafeSpot"/> list.
        /// </summary>
        protected virtual void ApplyLayerSafeSpots(MMTilemapGeneratorLayer layer, LevelTileLayer ltl)
        {
            if (ltl.SafeSpots == null || ltl.SafeSpots.Count == 0)
            {
                layer.SafeSpots = new List<MMTilemapGeneratorLayer.MMTilemapGeneratorLayerSafeSpot>();
                return;
            }

            layer.SafeSpots = new List<MMTilemapGeneratorLayer.MMTilemapGeneratorLayerSafeSpot>();
            foreach (LevelSafeSpot spot in ltl.SafeSpots)
            {
                layer.SafeSpots.Add(new MMTilemapGeneratorLayer.MMTilemapGeneratorLayerSafeSpot
                {
                    Start = spot.Start,
                    End   = spot.End
                });
            }
        }

        // ── Scene Tilemap Cache ─────────────────────────────────────────

        /// <summary>
        /// Scans the scene for all <see cref="Tilemap"/> components (including inactive)
        /// and caches them by <see cref="GameObject.name"/>.
        /// </summary>
        protected virtual void BuildSceneTilemapCache()
        {
            _sceneTilemapsByName = new Dictionary<string, Tilemap>();

            Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Tilemap tm in allTilemaps)
            {
                string goName = tm.gameObject.name;
                if (!_sceneTilemapsByName.ContainsKey(goName))
                {
                    _sceneTilemapsByName[goName] = tm;
                }
                else
                {
                    Debug.LogWarning($"[TilemapLevelGeneratorExtended] Duplicate Tilemap name '{goName}' — first one wins.");
                }
            }

            Debug.Log($"[TilemapLevelGeneratorExtended] Cached {_sceneTilemapsByName.Count} scene Tilemaps: " +
                      string.Join(", ", _sceneTilemapsByName.Keys));
        }

        /// <summary>
        /// Resolves a Tilemap by name from the scene cache. Returns null if name is null/empty or not found.
        /// </summary>
        protected virtual Tilemap ResolveTilemap(string tilemapName)
        {
            if (string.IsNullOrEmpty(tilemapName)) return null;
            if (_sceneTilemapsByName == null) return null;

            if (_sceneTilemapsByName.TryGetValue(tilemapName, out Tilemap tm))
                return tm;

            Debug.LogWarning($"[TilemapLevelGeneratorExtended] No scene Tilemap named '{tilemapName}' found.");
            return null;
        }

        // =====================================================================
        //  AUTO-LOAD & CAP SWORDS
        // =====================================================================

        /// <summary>
        /// Loads <see cref="OrbitWeaponDefinition"/> assets from Resources, sorts by tier,
        /// and caps based on the selected level's <see cref="LevelData.MaxSwordTier"/>.
        /// </summary>
        protected virtual void LoadSharedSwordPool()
        {
            _sharedSwordPool.Clear();

            if (string.IsNullOrEmpty(SwordsResourcePath)) return;

            OrbitWeaponDefinition[] loaded = Resources.LoadAll<OrbitWeaponDefinition>(SwordsResourcePath);
            if (loaded.Length == 0)
            {
                Debug.LogWarning($"[TilemapLevelGeneratorExtended] No swords found in Resources/{SwordsResourcePath}");
                return;
            }

            // Sort by natural numeric order (Sword_Level_1 → Sword_Level_48)
            OrbitWeaponDefinition[] sorted = loaded.OrderBy(s => ExtractSwordLevel(s.name)).ToArray();

            // Cap based on selected level's MaxSwordTier
            int maxTier = sorted.Length;
            if (LevelSelectConfig.HasSelection)
            {
                maxTier = Mathf.Clamp(LevelSelectConfig.SelectedLevel.MaxSwordTier, 1, sorted.Length);
            }

            for (int i = 0; i < maxTier; i++)
                _sharedSwordPool.Add(sorted[i]);

            Debug.Log($"[TilemapLevelGeneratorExtended] Loaded {_sharedSwordPool.Count}/{sorted.Length} sword tiers" +
                      (LevelSelectConfig.HasSelection ? $" (capped by '{LevelSelectConfig.SelectedLevel.LevelName}')" : ""));
        }

        /// <summary>
        /// Extracts the numeric level from asset names like "Sword_Level_12".
        /// </summary>
        protected virtual int ExtractSwordLevel(string assetName)
        {
            int lastUnderscore = assetName.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < assetName.Length - 1)
            {
                string numberPart = assetName.Substring(lastUnderscore + 1);
                if (int.TryParse(numberPart, out int level))
                    return level;
            }
            return int.MaxValue;
        }

        // =====================================================================
        //  AI SPAWNING
        // =====================================================================

        private IEnumerator SpawnAINextFrame()
        {
            // Wait one physics frame so the composite collider is fully updated
            yield return new WaitForFixedUpdate();
            SpawnAI();
        }

        /// <summary>
        /// Handles grid positioning and safe-space checking for the custom AI list.
        /// </summary>
        protected virtual void SpawnAI()
        {
            if (!Application.isPlaying) return;
            if (AIPrefabsToSpawn == null || AIPrefabsToSpawn.Count == 0) return;

            UnityEngine.Random.InitState(GlobalSeed);
            int width  = UnityEngine.Random.Range(GridWidth.x,  GridWidth.y);
            int height = UnityEngine.Random.Range(GridHeight.x, GridHeight.y);

            foreach (DynamicSpawnData data in AIPrefabsToSpawn)
            {
                if (data.AIPrefab == null) continue;

                for (int i = 0; i < data.Quantity; i++)
                {
                    Vector3 spawnPosition = Vector3.zero;
                    bool tooClose = true;
                    int iterationsCount = 0;

                    while (tooClose && (iterationsCount < _maxIterationsCount))
                    {
                        spawnPosition = MMTilemap.GetRandomPosition(
                            ObstaclesTilemap, TargetGrid, width, height, false, width * height * 2);

                        tooClose = false;
                        foreach (Vector3 filledPosition in _filledPositions)
                        {
                            if (Vector3.Distance(spawnPosition, filledPosition) < PrefabsSpawnMinDistance)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        iterationsCount++;
                    }

                    InstantiateAndConfigureAI(data, spawnPosition);
                }
            }
        }

        /// <summary>
        /// Instantiates the AI prefab and injects a randomised orbit-weapon loadout.
        /// Uses per-entry <see cref="DynamicSpawnData.PossibleOrbitWeaponDefinitions"/> if
        /// populated, otherwise falls back to <see cref="_sharedSwordPool"/>.
        /// </summary>
        protected virtual void InstantiateAndConfigureAI(DynamicSpawnData data, Vector3 spawnPosition)
        {
            GameObject spawnedEnemy = Instantiate(data.AIPrefab, spawnPosition, Quaternion.identity);
            _filledPositions.Add(spawnPosition);

            CharacterWeaponsOrbit orbitAbility = spawnedEnemy.GetComponent<CharacterWeaponsOrbit>();

            if (orbitAbility != null)
            {
                int randomCount = GetRandomWeaponCount(data.MinWeaponCount, data.MaxWeaponCount);

                List<OrbitWeaponDefinition> pool =
                    (data.PossibleOrbitWeaponDefinitions != null && data.PossibleOrbitWeaponDefinitions.Count > 0)
                        ? data.PossibleOrbitWeaponDefinitions
                        : _sharedSwordPool;

                OrbitWeaponDefinition randomDefinition =
                    GetRandomOrbitWeaponDefinition(pool, orbitAbility.WeaponDefinition);

                orbitAbility.WeaponDefinition = randomDefinition;
                orbitAbility.UpdateWeapons(randomCount);
            }
        }

        // ── Randomization Helpers ───────────────────────────────────────

        protected virtual int GetRandomWeaponCount(int min, int max)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        protected virtual OrbitWeaponDefinition GetRandomOrbitWeaponDefinition(
            List<OrbitWeaponDefinition> definitions, OrbitWeaponDefinition fallback)
        {
            if (definitions == null || definitions.Count == 0) return fallback;
            return definitions[UnityEngine.Random.Range(0, definitions.Count)];
        }
    }
}
