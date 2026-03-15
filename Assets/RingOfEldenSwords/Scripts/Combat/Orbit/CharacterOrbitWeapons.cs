using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Combat.Orbit
{
    public class CharacterOrbitWeapons : CharacterAbility
    {
        public enum OrbitState { Idle, Spawning, Sweeping, Orbiting }

        [Header("Orbit Configuration")]
        [SerializeField] private GameObject weaponPrefab;
        [SerializeField] private int weaponCount = 3;
        [SerializeField] private float orbitRadius = 2f;
        [SerializeField] private float orbitSpeed = 180f;

        [Header("Sweep Animation")]
        [SerializeField] private float arrivalDuration = 0.5f;
        [SerializeField] private float spawnAngleOffset = -45f;
        [SerializeField] private AnimationCurve sweepCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Rotation")]
        [SerializeField] private float weaponRotationOffset = 0f;

        private const int WeaponSortingOrder = 100;
        private const bool DebugLogs = false;

        private struct WeaponEntry
        {
            public GameObject go;
            public WeaponBehaviour behaviour;
        }

        private List<WeaponEntry> activeWeapons = new List<WeaponEntry>();
        private OrbitState currentState = OrbitState.Idle;
        private int weaponsArrived = 0;
        private bool isRotating = false;
        private Transform orbitPivot;
        private Queue<GameObject> weaponPool = new Queue<GameObject>();

        public event Action<GameObject> OnWeaponDestroyed;
        public event Action OnSweepComplete;

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

        protected override void Initialization()
        {
            base.Initialization();
            Transform existing = transform.Find("OrbitPivot");
            orbitPivot = existing != null ? existing : CreatePivot();
            UpdateWeapons(weaponCount);
        }

        private Transform CreatePivot()
        {
            GameObject pivotGO = new GameObject("OrbitPivot");
            pivotGO.transform.SetParent(transform);
            pivotGO.transform.localPosition = Vector3.zero;
            pivotGO.transform.localRotation = Quaternion.identity;
            return pivotGO.transform;
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();
            if (isRotating && orbitPivot != null && activeWeapons.Count > 0)
                orbitPivot.Rotate(0, 0, orbitSpeed * Time.deltaTime, Space.Self);
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            isRotating = false;
        }

        protected override void OnRespawn()
        {
            base.OnRespawn();
            UpdateWeapons(weaponCount);
        }

        private void OnDestroy()
        {
            while (weaponPool.Count > 0)
            {
                GameObject pooled = weaponPool.Dequeue();
                if (pooled != null) Destroy(pooled);
            }
        }

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

        public void StartRotation() => isRotating = true;
        public void StopRotation() => isRotating = false;

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
        }

        private void SpawnAndAnimateWeapons()
        {
            if (weaponCount <= 0 || orbitPivot == null) return;
            float angleStep = 360f / weaponCount;
            for (int i = 0; i < weaponCount; i++)
            {
                float targetAngle = i * angleStep;
                GameObject weapon = GetOrCreateWeapon(spawnAngleOffset, targetAngle);
                if (weapon == null) continue;
                float angularDistance = targetAngle - spawnAngleOffset;
                if (angularDistance < 0) angularDistance += 360f;
                StartCoroutine(SweepWeaponToPosition(weapon, spawnAngleOffset, targetAngle, angularDistance));
            }
            ChangeState(OrbitState.Sweeping);
        }

        private GameObject GetOrCreateWeapon(float spawnAngle, float targetAngle)
        {
            if (weaponPrefab == null)
            {
                Debug.LogError("[CharacterOrbitWeapons] Weapon prefab not assigned!");
                return null;
            }

            GameObject weapon = null;
            while (weaponPool.Count > 0 && weapon == null)
                weapon = weaponPool.Dequeue();
            if (weapon == null)
                weapon = Instantiate(weaponPrefab);

            weapon.transform.SetParent(orbitPivot);
            weapon.transform.localPosition = CalculatePosition(spawnAngle);
            weapon.transform.localRotation = Quaternion.Euler(0, 0, spawnAngle + weaponRotationOffset);

            WeaponBehaviour wb = weapon.GetComponent<WeaponBehaviour>();
            if (wb != null)
            {
                wb.ResetHealth();
                wb.OnDestroyed += HandleWeaponDestroyed;
            }

            SpriteRenderer sr = weapon.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = WeaponSortingOrder;

            weapon.SetActive(true);
            activeWeapons.Add(new WeaponEntry { go = weapon, behaviour = wb });
            return weapon;
        }

        private Vector3 CalculatePosition(float angle)
        {
            return new Vector3(
                orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad),
                orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad),
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

        private void ChangeState(OrbitState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            if (newState == OrbitState.Idle || newState == OrbitState.Spawning)
                StopRotation();
        }

        private IEnumerator SweepWeaponToPosition(GameObject weapon, float startAngle, float targetAngle, float angularDistance)
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
            float duration = arrivalDuration;
            AnimationCurve curve = sweepCurve;

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
    }
}
