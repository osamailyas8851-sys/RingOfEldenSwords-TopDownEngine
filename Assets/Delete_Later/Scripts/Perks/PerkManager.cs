using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;

namespace MoreMountains.TopDownEngine
{
    [AddComponentMenu("TopDown Engine/Character/Perks/Perk Manager")]
    public class PerkManager : TopDownMonoBehaviour, MMEventListener<XPChangeEvent>
    {
        [Header("Perk Pool")]
        [Tooltip("All perks available in the random selection pool.")]
        [SerializeField] protected PerkDefinition[] AvailablePerks;

        [Header("Sword Tiers (ordered Level 1 -> 5)")]
        [Tooltip("Drag Sword_Level_1 through Sword_Level_5 in order.")]
        [SerializeField] protected OrbitWeaponDefinition[] SwordTiers;

        [Header("UI")]
        [SerializeField] protected PerkSelectionUI _perkUI;

        [Header("State (read-only)")]
        [MMReadOnly] public List<PerkDefinition> AcquiredPerks = new List<PerkDefinition>();

        protected CharacterWeaponsOrbit _orbit;
        protected Health _health;

        // Reusable pool list to avoid GC allocation every level-up
        protected readonly List<PerkDefinition> _reusablePool = new List<PerkDefinition>();

        protected virtual void Start()
        {
            _orbit  = GetComponent<CharacterWeaponsOrbit>();
            _health = GetComponent<Health>();

            // Find PerkSelectionUI in scene if not assigned
            if (_perkUI == null)
            {
                PerkSelectionUI found = FindAnyObjectByType<PerkSelectionUI>(FindObjectsInactive.Include);
                if (found != null) _perkUI = found;
            }

            // Make sure UI starts hidden
            if (_perkUI != null)
                _perkUI.Hide();
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<XPChangeEvent>();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<XPChangeEvent>();

            // Safety: if we're disabled while perk UI is open, resume game
            ResumeGame();
        }

        public virtual void OnMMEvent(XPChangeEvent xpEvent)
        {
            if (xpEvent.JustLeveledUp)
                ShowPerkSelection();
        }

        protected virtual void ShowPerkSelection()
        {
            // Guard: no UI available
            if (_perkUI == null)
            {
                Debug.LogWarning("[PerkManager] PerkSelectionUI not found. Skipping perk selection.");
                return;
            }

            PerkDefinition[] choices = PickRandomPerks(3);

            // Guard: no valid perks to offer
            if (choices.Length == 0)
            {
                Debug.Log("[PerkManager] No valid perks available this level-up.");
                return;
            }

            PauseGame();
            _perkUI.Show(choices, this);
        }

        public virtual void SelectPerk(PerkDefinition perk)
        {
            ApplyPerk(perk);
            AcquiredPerks.Add(perk);

            _perkUI.Hide();
            ResumeGame();
        }

        // ── Pause / Resume (follows TDE GameManager pattern) ─────────────

        protected virtual void PauseGame()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.Pause(PauseMethods.NoPauseMenu, false);
            }
            else
            {
                // Fallback if GameManager is missing
                Time.timeScale = 0f;
            }
        }

        protected virtual void ResumeGame()
        {
            if (GameManager.HasInstance && GameManager.Instance.Paused)
            {
                GameManager.Instance.UnPause(PauseMethods.NoPauseMenu);
            }
            else if (Time.timeScale == 0f)
            {
                // Fallback safety net
                Time.timeScale = 1f;
            }
        }

        // ── Perk Application ─────────────────────────────────────────────

        protected virtual void ApplyPerk(PerkDefinition perk)
        {
            switch (perk.PerkType)
            {
                case PerkType.ExtraSword:
                    if (_orbit != null)
                        _orbit.AddWeapons((int)perk.Value);
                    break;

                case PerkType.SwordLevelUp:
                    if (_orbit != null)
                        UpgradeSwordTier();
                    break;

                case PerkType.HealthUp:
                    if (_health != null)
                    {
                        _health.MaximumHealth += perk.Value;
                        _health.SetHealth(_health.MaximumHealth);
                    }
                    break;
            }
        }

        protected virtual void UpgradeSwordTier()
        {
            if (SwordTiers == null || SwordTiers.Length == 0) return;

            int currentIndex = GetCurrentSwordTierIndex();
            int nextIndex    = Mathf.Min(currentIndex + 1, SwordTiers.Length - 1);

            if (nextIndex == currentIndex) return; // already max

            _orbit.WeaponDefinition = SwordTiers[nextIndex];
            _orbit.UpdateWeapons(_orbit.ActiveWeaponCount);
        }

        protected virtual int GetCurrentSwordTierIndex()
        {
            for (int i = 0; i < SwordTiers.Length; i++)
            {
                if (SwordTiers[i] == _orbit.WeaponDefinition)
                    return i;
            }
            return 0;
        }

        protected virtual bool IsSwordMaxLevel()
        {
            if (SwordTiers == null || SwordTiers.Length == 0) return true;
            return GetCurrentSwordTierIndex() >= SwordTiers.Length - 1;
        }

        // ── Random Selection (zero-alloc reusable list) ──────────────────

        protected virtual PerkDefinition[] PickRandomPerks(int count)
        {
            _reusablePool.Clear();

            foreach (PerkDefinition perk in AvailablePerks)
            {
                if (perk == null) continue;

                // Filter out SwordLevelUp if already at max tier
                if (perk.PerkType == PerkType.SwordLevelUp && IsSwordMaxLevel())
                    continue;

                _reusablePool.Add(perk);
            }

            // Fisher-Yates partial shuffle and pick
            int pickCount = Mathf.Min(count, _reusablePool.Count);
            PerkDefinition[] result = new PerkDefinition[pickCount];

            for (int i = 0; i < pickCount; i++)
            {
                int randomIndex = Random.Range(i, _reusablePool.Count);
                // Swap
                PerkDefinition temp    = _reusablePool[i];
                _reusablePool[i]           = _reusablePool[randomIndex];
                _reusablePool[randomIndex] = temp;

                result[i] = _reusablePool[i];
            }

            return result;
        }
    }
}
