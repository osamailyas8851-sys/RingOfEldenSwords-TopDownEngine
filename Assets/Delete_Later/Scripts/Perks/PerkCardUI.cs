using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Individual perk card in the selection overlay.
    /// Displays a PerkDefinition's icon and name, and notifies
    /// PerkManager when the player clicks this card.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Perk Card UI")]
    public class PerkCardUI : TopDownMonoBehaviour
    {
        [SerializeField] protected Image _iconImage;
        [SerializeField] protected TMP_Text _nameText;
        [SerializeField] protected TMP_Text _descriptionText;

        protected PerkDefinition _perk;
        protected PerkManager _manager;
        protected Button _button;

        protected virtual void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(OnCardClicked);
        }

        protected virtual void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnCardClicked);
        }

        public virtual void Setup(PerkDefinition perk, PerkManager manager)
        {
            _perk    = perk;
            _manager = manager;

            if (_iconImage != null && perk.Icon != null)
                _iconImage.sprite = perk.Icon;
            if (_nameText != null)
                _nameText.text = perk.PerkName;
            if (_descriptionText != null)
                _descriptionText.text = perk.Description;
        }

        public virtual void OnCardClicked()
        {
            if (_manager != null && _perk != null)
                _manager.SelectPerk(_perk);
        }
    }
}
