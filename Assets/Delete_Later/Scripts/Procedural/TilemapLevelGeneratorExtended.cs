using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.LevelGeneration
{
    [AddComponentMenu("TopDown Engine/Level Generation/Tilemap Level Generator Extended")]
    public class TilemapLevelGeneratorExtended : TilemapLevelGenerator
    {
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
            public List<OrbitWeaponDefinition> PossibleOrbitWeaponDefinitions;
        }

        [Header("Dynamic Spawning Settings")]
        [Tooltip("Custom advanced AI spawning. This runs AFTER the default TDE PrefabsToSpawn.")]
        public List<DynamicSpawnData> AIPrefabsToSpawn;

        public override void Generate()
        {
            // 1. Let TDE do all its normal map generation
            base.Generate();

            if (Application.isPlaying)
            {
                // 2. Spawn AI next physics frame
                StartCoroutine(SpawnAINextFrame());
            }
        }


        private IEnumerator SpawnAINextFrame()
        {
            // Wait one physics frame so the composite collider is fully updated
            yield return new WaitForFixedUpdate();
            SpawnAI();
        }

        /// <summary>
        /// Handles grid positioning and safe-space checking for our custom AI list.
        /// </summary>
        protected virtual void SpawnAI()
        {
            if (!Application.isPlaying) return;

            UnityEngine.Random.InitState(GlobalSeed);
            int width  = UnityEngine.Random.Range(GridWidth.x,  GridWidth.y);
            int height = UnityEngine.Random.Range(GridHeight.x, GridHeight.y);

            foreach (DynamicSpawnData data in AIPrefabsToSpawn)
            {
                for (int i = 0; i < data.Quantity; i++)
                {
                    Vector3 spawnPosition = Vector3.zero;
                    bool tooClose = true;
                    int iterationsCount = 0;

                    while (tooClose && (iterationsCount < 100))
                    {
                        spawnPosition = MMTilemap.GetRandomPosition(ObstaclesTilemap, TargetGrid, width, height, false, width * height * 2);

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
        /// FACTORY: Instantiates the prefab and injects the randomized loadout.
        /// </summary>
        protected virtual void InstantiateAndConfigureAI(DynamicSpawnData data, Vector3 spawnPosition)
        {
            GameObject spawnedEnemy = Instantiate(data.AIPrefab, spawnPosition, Quaternion.identity);
            _filledPositions.Add(spawnPosition);

            CharacterWeaponsOrbit orbitAbility = spawnedEnemy.GetComponent<CharacterWeaponsOrbit>();

            if (orbitAbility != null)
            {
                int              randomCount      = GetRandomWeaponCount(data.MinWeaponCount, data.MaxWeaponCount);
                OrbitWeaponDefinition randomDefinition = GetRandomOrbitWeaponDefinition(data.PossibleOrbitWeaponDefinitions, orbitAbility.WeaponDefinition);

                orbitAbility.WeaponDefinition = randomDefinition;
                orbitAbility.UpdateWeapons(randomCount);
            }
        }

        // ── Randomization helpers ────────────────────────────────────────────

        protected virtual int GetRandomWeaponCount(int min, int max)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        protected virtual OrbitWeaponDefinition GetRandomOrbitWeaponDefinition(List<OrbitWeaponDefinition> definitions, OrbitWeaponDefinition fallback)
        {
            if (definitions == null || definitions.Count == 0) return fallback;
            return definitions[UnityEngine.Random.Range(0, definitions.Count)];
        }
    }
}
