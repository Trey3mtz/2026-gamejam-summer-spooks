using UnityEngine;
using SpookyGame.Core.Item_System;

namespace SpookyGame.Player
{
    /// <summary>
    /// Spawns the HeldPrefab of the currently selected inventory item under this
    /// transform (the hand anchor). Purely visual; item logic stays in ItemDefinition
    /// effects. Listens to selection AND inventory changes so a consumed stack
    /// clears the hand even when the selected slot index doesn't move.
    /// </summary>
    public class HeldItemRig : MonoBehaviour
    {
        [Tooltip("Owning Player. Found in parents if left empty.")]
        [SerializeField] private Player _player;

        private PlayerInventory _inventory;
        private GameObject _currentInstance;
        private ItemDefinition _currentItem;

        private Tween _holsterTween;
        [SerializeField] private float _holsterDip = 0.35f;      // meters downward
        [SerializeField] private float _holsterDuration = 0.2f;

        // Start, not OnEnable: Player.Awake builds the inventory, and Awake/OnEnable
        // ordering across separate objects isn't guaranteed. Same reasoning as
        // PlayerUI deferring RefreshIcons to Start.
        private void Start()
        {
            if (_player == null)
                _player = GetComponentInParent<Player>();

            _inventory = _player.Inventory;
            Refresh();
        }

        private void OnEnable()
        {
            _inventory.OnSelectionChanged += HandleSelectionChanged;
            _inventory.InventoryChanged   += Refresh;
            _player.HolsterChanged        += HandleHolsterChanged;
        }

        private void OnDisable()
        {
            _inventory.OnSelectionChanged -= HandleSelectionChanged;
            _inventory.InventoryChanged   -= Refresh;
            _player.HolsterChanged        -= HandleHolsterChanged;
        }

        private void OnDestroy()
        {
            if (_inventory == null) return;
            _inventory.OnSelectionChanged -= HandleSelectionChanged;
            _inventory.InventoryChanged   -= Refresh;
        }

        private void HandleSelectionChanged(int _) => Refresh();

        private void Refresh()
        {
            ItemDefinition item = _inventory.SelectedItem;
            if (item == _currentItem) return;   // InventoryChanged fires often; skip no-ops
            _currentItem = item;

            if (_currentInstance != null)
            {
                Destroy(_currentInstance);
                _currentInstance = null;
            }

            if (item == null || item.HeldPrefab == null) return;

            // Identity local transform: the anchor defines the hand pose,
            // the prefab defines its own grip offset internally (see below).
            _currentInstance = Instantiate(item.HeldPrefab, transform, false);
        }

        private void HandleHolsterChanged(bool _) => ApplyVisibility();

        private void ApplyVisibility()
        {
            if (_currentInstance == null) return;
            _holsterTween?.Kill();
        
            if (_player.IsHolstered)
            {
                _holsterTween = _currentInstance.transform
                    .DOLocalMoveY(-_holsterDip, _holsterDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => _currentInstance.SetActive(false))
                    .SetLink(_currentInstance);
            }
            else
            {
                _currentInstance.SetActive(true);
                _currentInstance.transform.localPosition = new Vector3(0f, -_holsterDip, 0f);
                _holsterTween = _currentInstance.transform
                    .DOLocalMoveY(0f, _holsterDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(_currentInstance);
            }
        }
    }
}
