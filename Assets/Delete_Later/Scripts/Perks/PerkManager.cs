using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MoreMountains.Tools;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;

namespace MoreMountains.TopDownEngine
{
    [AddComponentMenu("TopDown Engine/Character/Perks/Perk Manager")]
    public class PerkManager : TopDownMonoBehaviour, MMEventListener<XPChangeEvent>
    {
        [Header("Auto-Load Paths (Resources/)")]
        [Tooltip("Resources sub-folder to auto-load PerkDefinition assets from. Leave empty to use the manual array below.")]
        [SerializeField] protected string PerksResourcePath = "Perks";

        [Tooltip("Resources sub-folder to auto-load OrbitWeaponDefinition assets from (sorted by name). Leave empty to use the manual array below.")]
        [SerializeField] protected string SwordsResourcePath = "Weapons/Swords";

        [Header("Manual Overrides (used only if Resource paths are empty)")]
        [Tooltip("Manually assigned perks. Ignored when PerksResourcePath is set.")]
        [SerializeField] protected PerkDefinition[] AvailablePerks;

        // Always auto-loaded from Resources — not exposed in Inspector
        protected OrbitWeaponDefinition[] SwordTiers;

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

            // Auto-load perks from Resources if path is set
            LoadPerksFromResources();

            // Auto-load sword tiers from Resources if path is set
            LoadSwordTiersFromResources();

            // Cap sword tiers based on the selected level's MaxSwordTier
            ApplyLevelSwordCap();

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

        // ── Auto-Load from Resources ─────────────────────────────────────

        /// <summary>
        /// Loads all PerkDefinition assets from Resources/[PerksResourcePath].
        /// Any new .asset dropped into that folder is picked up automatically.
        /// </summary>
        protected virtual void LoadPerksFromResources()
        {
            if (string.IsNullOrEmpty(PerksResourcePath)) return;

            PerkDefinition[] loaded = Resources.LoadAll<PerkDefinition>(PerksResourcePath);
            if (loaded.Length > 0)
            {
                AvailablePerks = loaded;
                Debug.Log($"[PerkManager] Auto-loaded {loaded.Length} perks from Resources/{PerksResourcePath}");
            }
            else
            {
                Debug.LogWarning($"[PerkManager] No PerkDefinition assets found in Resources/{PerksResourcePath}");
            }
        }

        /// <summary>
        /// Loads all OrbitWeaponDefinition assets from Resources/[SwordsResourcePath],
        /// sorted by name so Sword_Level_1 → Sword_Level_48 are in correct tier order.
        /// </summary>
        protected virtual void LoadSwordTiersFromResources()
        {
            if (string.IsNullOrEmpty(SwordsResourcePath)) return;

            OrbitWeaponDefinition[] loaded = Resources.LoadAll<OrbitWeaponDefinition>(SwordsResourcePath);
            if (loaded.Length > 0)
            {
                // Sort by name using natural numeric ordering (Level_1, Level_2, ..., Level_10)
                SwordTiers = loaded.OrderBy(s => ExtractSwordLevel(s.name)).ToArray();
                Debug.Log($"[PerkManager] Auto-loaded {SwordTiers.Length} sword tiers from Resources/{SwordsResourcePath}");
            }
            else
            {
                Debug.LogWarning($"[PerkManager] No OrbitWeaponDefinition assets found in Resources/{SwordsResourcePath}");
            }
        }

        /// <summary>
        /// Caps the SwordTiers array based on the selected level's MaxSwordTier.
        /// If no level is selected (e.g., launching scene directly from editor), all tiers remain available.
        /// </summary>
        protected virtual void ApplyLevelSwordCap()
        {
            if (SwordTiers == null || SwordTiers.Length == 0) return;
            if (!LevelSelectConfig.HasSelection) return;

            int maxTier = LevelSelectConfig.SelectedLevel.MaxSwordTier;
            if (maxTier <= 0) return;

            if (maxTier < SwordTiers.Length)
            {
                OrbitWeaponDefinition[] capped = new OrbitWeaponDefinition[maxTier];
                System.Array.Copy(SwordTiers, capped, maxTier);
                SwordTiers = capped;
                Debug.Log($"[PerkManager] Sword tiers capped to {maxTier} for level '{LevelSelectConfig.SelectedLevel.LevelName}'");
            }
        }

        /// <summary>
        /// Extracts the numeric level from a name like "Sword_Level_12".
        /// Returns the number for sorting, or int.MaxValue if not found.
        /// </summary>
        protected virtual int ExtractSwordLevel(string assetName)
        {
            // Find the last underscore and parse the number after it
            int lastUnderscore = assetName.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < assetName.Length - 1)
            {
                string numberPart = assetName.Substring(lastUnderscore + 1);
                if (int.TryParse(numberPart, out int level))
                    return level;
            }
            return int.MaxValue;
        }

        // ── Event Listeners ──────────────────────────────────────────────

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

        // ── Perk Selection Flow ──────────────────────────────────────────

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

            if (nextIndex == currentIndex) return;

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

                if (perk.PerkType == PerkType.SwordLevelUp && IsSwordMaxLevel())
                    continue;

                _reusablePool.Add(perk);
            }

            int pickCount = Mathf.Min(count, _reusablePool.Count);
            PerkDefinition[] result = new PerkDefinition[pickCount];

            for (int i = 0; i < pickCount; i++)
            {
                int randomIndex = Random.Range(i, _reusablePool.Count);
                PerkDefinition temp        = _reusablePool[i];
                _reusablePool[i]           = _reusablePool[randomIndex];
                _reusablePool[randomIndex] = temp;

                result[i] = _reusablePool[i];
            }

            return result;
        }
    }
}
