using UnityEngine;
using TMPro;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Displays coin and diamond counts on the UI.
    /// Listens to <see cref="GameProgressEvent"/> to stay in sync with
    /// <see cref="GameProgressManager"/> without polling.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Currency Display")]
    public class CurrencyDisplay : TopDownMonoBehaviour, MMEventListener<GameProgressEvent>
    {
        [Header("UI References")]
        [SerializeField] protected TMP_Text _coinsText;
        [SerializeField] protected TMP_Text _diamondsText;
        [SerializeField] protected TMP_Text _playerLevelText;

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<GameProgressEvent>();
            RefreshFromManager();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<GameProgressEvent>();
        }

        public virtual void OnMMEvent(GameProgressEvent progressEvent)
        {
            RefreshFromManager();
        }

        protected virtual void RefreshFromManager()
        {
            if (GameProgressManager.Instance == null) return;
            var p = GameProgressManager.Instance.Progress;
            if (p == null) return;

            if (_coinsText != null)        _coinsText.text       = FormatCount(p.Coins);
            if (_diamondsText != null)     _diamondsText.text    = FormatCount(p.Diamonds);
            if (_playerLevelText != null)  _playerLevelText.text = p.PlayerLevel.ToString();
        }

        protected virtual string FormatCount(int value)
        {
            if (value >= 1000000) return $"{value / 1000000f:0.#}M";
            if (value >= 1000)    return $"{value / 1000f:0.#}K";
            return value.ToString();
        }
    }
}
