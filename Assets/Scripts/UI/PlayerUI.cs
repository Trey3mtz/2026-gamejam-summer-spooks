using UnityEngine;
using SpookyGame.Player;
using SpookyGame.Core.ItemSystem;

namespace SpookyGame.UI
{
    public class PlayerUI : MonoBehaviour
    {
        [SerializeField] private Player _player;
        
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

        private void HandleSelectionChanged()
        {
            
        }

        private void RefreshIcons()
        {
        
        }
    }
}
