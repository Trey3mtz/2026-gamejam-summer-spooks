using UnityEngine;
using SpookyGame.Player;
using SpookyGame.Core.ItemSystem;
using DG.Tweening;

namespace SpookyGame.UI
{
    public class PlayerUI : MonoBehaviour
    {
        [SerializeField] private Player _player;
        
        [Header("Selection Cycle (bottom-left)")]
        [SerializeField] private Image _previousIcon;
        [SerializeField] private Image _currentIcon;
        [SerializeField] private Image _nextIcon;

        [Header("Item Name (bottom-centre)")]
        [SerializeField] private TextMeshProUGUI _itemNameLabel;
        [SerializeField] private CanvasGroup _itemNameGroup;
        [Tooltip("Seconds the name stays fully visible before it begins to fade.")]
        [SerializeField] private float _nameHoldDelay = 1f;
        [Tooltip("Seconds the name takes to fade out.")]
        [SerializeField] private float _nameFadeDuration = 1f;
        
        private PlayerInventory _inventory;
        
        private void OnEnable()
        {
            if (_player == null) return;
            _inventory = _player.Inventory;               // created in Player.Awake
            if (_inventory == null) return;
        
            _inventory.OnSelectionChanged += HandleSelectionChanged;
            _inventory.InventoryChanged   += RefreshIcons;
        }
        
        private void OnDisable()
        {
            if (_inventory == null) return;
            _inventory.OnSelectionChanged -= HandleSelectionChanged;
            _inventory.InventoryChanged   -= RefreshIcons;
        }

        private void Start()
        {
            RefreshIcons();                               // Player.Awake has run by now
            if (_itemNameGroup != null) _itemNameGroup.alpha = 0f;
        }

        private void HandleSelectionChanged(int slot)
        {
            RefreshIcons();
            FlashItemName(_inventory.SelectedItem);
        }

        private void RefreshIcons()
        {
            if (_inventory == null) return;

            int current  = _inventory.SelectedSlot;
            int previous = _inventory.PreviousSlot;
            int next     = _inventory.NextSlot;

            SetIcon(_currentIcon, _inventory.SelectedItem);

            // Hide a side icon when it resolves back to the current slot (≤ 1 item).
            SetIcon(_previousIcon, previous >= 0 && previous != current ? _inventory.ItemInSlot(previous) : null);
            SetIcon(_nextIcon,     next     >= 0 && next     != current ? _inventory.ItemInSlot(next)     : null);
        }

        private static void SetIcon(Image target, ItemDefinition item)
        {
            if (target == null) return;

            bool hasIcon = item != null && item.Icon != null;
            target.sprite  = hasIcon ? item.Icon : null;
            target.enabled = hasIcon;                     // empty slot draws nothing
        }

        private void FlashItemName(ItemDefinition item)
        {
            if (_itemNameGroup == null || _itemNameLabel == null) return;

            //if (_nameRoutine != null) StopCoroutine(_nameRoutine);

            if (item == null)
            {
                _itemNameGroup.alpha = 0f;
                return;
            }

            _itemNameLabel.text = item.ItemName;
            //_nameRoutine = StartCoroutine(NameFlashRoutine());
        }
    }
}
