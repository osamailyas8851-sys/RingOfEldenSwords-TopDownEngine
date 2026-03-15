using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using RingOfEldenSwords.Combat.Weapons;
using RingOfEldenSwords.Combat.Config;
using RingOfEldenSwords.Core;

namespace RingOfEldenSwords.Combat.Orbit
{
    public class OrbitSystem : MonoBehaviour
    {
        public enum OrbitState { Idle, Spawning, Sweeping, Orbiting }

        [Header("Configuration")]
        [SerializeField] private CombatConfig config;

        [Header("References")]
        [SerializeField] private GameObject weaponPrefab;

        [Header("Faction")]
        public Faction faction = Faction.Enemy;

        [Header("Weapon Count")]
        [SerializeField] private int weaponCount = 3;

        [Header("Rotation")]
        [SerializeField] private float orbitSpeed = 180f;

        [Header("Rotation Offset")]
        [SerializeField] private float weaponRotationOffset = 0f;

        private const int WeaponSortingOrder = 100;

        private struct WeaponEntry
        {
            public GameObject go;
            public WeaponBehaviour behaviour;
        }

        private List<WeaponEntry> activeWeapons = new List<WeaponEntry>();
        private List<float> targetAngles = new List<float>();
        private OrbitState currentState = OrbitState.Idle;
        private int weaponsArrived = 0;
        private bool isRotating = false;

        private float cachedOrbitRadius = 2f;
        private float cachedOrbitSpeed = 180f;
        private float cachedSpawnAngleOffset = -45f;
        private float cachedArrivalDuration = 0.5f;
        private AnimationCurve cachedSweepCurve;

        private Queue<GameObject> weaponPool = new Queue<GameObject>();

        public event Action<GameObject> OnWeaponSpawned;
        public event Action OnSweepComplete;
        public event Action<OrbitState> OnStateChanged;
        public event Action<GameObject> OnWeaponDestroyed;

        public int WeaponCount => weaponCount;
        public int ActiveWeaponCount => activeWeapons.Count;
        public OrbitState State => currentState;
        public bool IsRotating => isRotating;

        public IReadOnlyList<GameObject> Weapons
        {
            get
            {
                var list = new List<GameObject>(activeWeapons.Count);
                foreach (var entry in activeWeapons) list.Add(entry.go);
                return list;
            }
        }

        void Start()
        {
            CacheConfig();
            UpdateWeapons(weaponCount);
        }

        void Update()
        {
            if (isRotating && activeWeapons.Count > 0)
                transform.Rotate(0, 0, cachedOrbitSpeed * Time.deltaTime, Space.Self);
        }

        private void OnDestroy()
        {
            while (weaponPool.Count > 0)
            {
                GameObject pooled = weaponPool.Dequeue();
                if (pooled != null) Destroy(pooled);
            }
        }

        private void CacheConfig()
        {
            if (config != null)
            {
                cachedOrbitRadius = config.orbitRadius;
                cachedOrbitSpeed = config.orbitSpeed;
                cachedSpawnAngleOffset = config.spawnAngleOffset;
                cachedArrivalDuration = config.arrivalDuration;
                cachedSweepCurve = config.sweepCurve;
                orbitSpeed = config.orbitSpeed;
            }
            else
            {
                cachedSweepCurve = AnimationCurve.Linear(0, 0, 1, 1);
            }
        }

        public void StartRotation() => isRotating = true;
        public void StopRotation() => isRotating = false;
        public void SetSpeed(float speed) { cachedOrbitSpeed = speed; orbitSpeed = speed; }

        public void UpdateWeapons(int newWeaponCount)
        {
            StopAllCoroutines();
            weaponCount = newWeaponCount;
            weaponsArrived = 0;
            ReturnAllWeaponsToPool();
            ChangeState(OrbitState.Spawning);
            SpawnAndAnimateWeapons();
        }

        public void AddWeapons(int count)
        {
            if (count <= 0) return;
            UpdateWeapons(activeWeapons.Count + count);
        }

        public void SetConfig(CombatConfig newConfig) { config = newConfig; CacheConfig(); }
        public CombatConfig GetConfig() => config;

        private void ReturnAllWeaponsToPool()
        {
            foreach (var entry in activeWeapons)
            {
                if (entry.go == null) continue;
                if (entry.behaviour != null)
                    entry.behaviour.OnDestroyed -= HandleWeaponDestroyed;
                entry.go.SetActive(false);
                weaponPool.Enqueue(entry.go);
            }
            activeWeapons.Clear();
            targetAngles.Clear();
        }

        private void SpawnAndAnimateWeapons()
        {
            float angleStep = 360f / weaponCount;
            for (int i = 0; i < weaponCount; i++)
            {
                float targetAngle = i * angleStep;
                GameObject weapon = GetOrCreateWeapon(i, cachedSpawnAngleOffset, targetAngle);
                if (weapon == null) continue;
                float angularDistance = targetAngle - cachedSpawnAngleOffset;
                if (angularDistance < 0) angularDistance += 360f;
                StartCoroutine(SweepWeaponToPosition(weapon, cachedSpawnAngleOffset, targetAngle, angularDistance, i));
            }
            ChangeState(OrbitState.Sweeping);
        }

        private GameObject GetOrCreateWeapon(int index, float spawnAngle, float targetAngle)
        {
            if (weaponPrefab == null) { Debug.LogError("[OrbitSystem] Weapon prefab not assigned!"); return null; }

            GameObject weapon = null;
            while (weaponPool.Count > 0 && weapon == null)
                weapon = weaponPool.Dequeue();
            if (weapon == null)
                weapon = Instantiate(weaponPrefab);

            weapon.transform.SetParent(transform);

            WeaponBehaviour wb = weapon.GetComponent<WeaponBehaviour>();
            if (wb != null)
            {
                wb.ownerFaction = faction;
                wb.ResetHealth();
                wb.OnDestroyed += HandleWeaponDestroyed;
            }

            targetAngles.Add(targetAngle);
            weapon.transform.localPosition = CalculatePosition(spawnAngle);
            weapon.transform.localRotation = Quaternion.Euler(0, 0, spawnAngle + weaponRotationOffset);

            SpriteRenderer sr = weapon.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = WeaponSortingOrder;

            weapon.SetActive(true);
            activeWeapons.Add(new WeaponEntry { go = weapon, behaviour = wb });
            OnWeaponSpawned?.Invoke(weapon);
            return weapon;
        }

        private Vector3 CalculatePosition(float angle)
        {
            return new Vector3(
                cachedOrbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad),
                cachedOrbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad),
                0
            );
        }

        private void HandleWeaponDestroyed(GameObject weapon)
        {
            if (weapon == null) return;
            for (int i = 0; i < activeWeapons.Count; i++)
            {
                if (activeWeapons[i].go != weapon) continue;
                if (activeWeapons[i].behaviour != null)
                    activeWeapons[i].behaviour.OnDestroyed -= HandleWeaponDestroyed;
                activeWeapons.RemoveAt(i);
                break;
            }
            OnWeaponDestroyed?.Invoke(weapon);
        }

        private IEnumerator SweepWeaponToPosition(GameObject weapon, float startAngle, float targetAngle, float angularDistance, int index)
        {
            if (weapon == null) { OnWeaponArrived(); yield break; }

            if (angularDistance < 0.5f)
            {
                weapon.transform.localPosition = CalculatePosition(targetAngle);
                weapon.transform.localRotation = Quaternion.Euler(0, 0, targetAngle + weaponRotationOffset);
                OnWeaponArrived();
                yield break;
            }

            float elapsed = 0f;
            float duration = cachedArrivalDuration;
            AnimationCurve curve = cachedSweepCurve;

            while (elapsed < duration)
            {
                if (weapon == null) { OnWeaponArrived(); yield break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float currentAngle = startAngle + (angularDistance * curve.Evaluate(t));
                weapon.transform.localPosition = CalculatePosition(currentAngle);
                weapon.transform.localRotation = Quaternion.Euler(0, 0, currentAngle + weaponRotationOffset);
                yield return null;
            }

            if (weapon != null)
            {
                weapon.transform.localPosition = CalculatePosition(targetAngle);
                weapon.transform.localRotation = Quaternion.Euler(0, 0, targetAngle + weaponRotationOffset);
            }
            OnWeaponArrived();
        }

        private void OnWeaponArrived()
        {
            weaponsArrived++;
            if (weaponsArrived >= weaponCount)
            {
                OnSweepComplete?.Invoke();
                ChangeState(OrbitState.Orbiting);
                StartRotation();
            }
        }

        private void ChangeState(OrbitState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            OnStateChanged?.Invoke(newState);
            if (newState == OrbitState.Idle || newState == OrbitState.Spawning)
                StopRotation();
        }
    }
}
