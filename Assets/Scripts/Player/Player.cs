using System;
using System.Collections.Generic;
using SpookyGame.Core;
using SpookyGame.Core.Item_System;
using SpookyGame.Player.Data;
using UnityEngine;

namespace SpookyGame.Player
{
    public class Player : MonoBehaviour
    {
        // Reference to health, only used for saving/loading in this script here or for referencing.
        private HealthBar _health;
        public HealthBar Health => _health;
        
        private Inventory _inventory;
        public List<Inventory.InventoryEntry> Inventory => _inventory.GetInventory();
        
        private Vector3 _lastRespawnPosition;
        
        private void Awake()
        {
            _health = new HealthBar();
            _health.InitHealthBar(10);
            _inventory = new Inventory();
            _lastRespawnPosition = transform.position;
        }


        // ==========================================================
        //  Inventory API
        // ==========================================================
        // UI / audio can react to selection and use without polling.
        public event Action<int> OnSelectionChanged;
        public event Action<ItemDefinition> OnItemUsed;
        public Inventory.InventoryEntry SelectedEntry
        {
            get
            {
                var entries = _inventory.GetInventory();
                if (_selectedInventorySlot < 0 || _selectedInventorySlot >= entries.Count)
                    return null;
                return entries[_selectedInventorySlot];
            }
        }
        
        public ItemDefinition SelectedItem => SelectedEntry?.item;

        private int _selectedInventorySlot = 0;
        private int _invItemCount => Inventory.Count;
        private int _invCapacity => Inventory.Capacity;
        
        public void SelectNextItem()     => MoveSelection(+1);
        public void SelectPreviousItem() => MoveSelection(-1);

        /// <summary>Advances to the next non-empty slot in the given direction, wrapping.</summary>
        private void MoveSelection(int direction)
        {
            var entries = _inventory.GetInventory();
            int count = entries.Count;
            if (count == 0) return;

            for (int step = 1; step <= count; step++)
            {
                int idx = (((_selectedInventorySlot + direction * step) % count) + count) % count;
                if (entries[idx].item != null)
                {
                    SetSelected(idx);
                    return;
                }
            }
            // Nothing to select; leave selection unchanged.
        }

        /// <summary>Selects a raw slot index (number keys / UI clicks), empty or not.</summary>
        public void SelectSpecificItem(int index)
        {
            var entries = _inventory.GetInventory();
            if (index < 0 || index >= entries.Count) return;
            SetSelected(index);
        }
        private void SetSelected(int index)
        {
            if (index == _selectedInventorySlot) return;
            _selectedInventorySlot = index;
            OnSelectionChanged?.Invoke(_selectedInventorySlot);
        }
        
        public bool TryUseItem()
        {
            var entry = SelectedEntry;
            if (entry?.item == null) return false;
            ItemDefinition item = entry.item;
            // targetPosition is provided for effects that need a world point (spawns, throws).
            Vector3 target = transform.position;
            
            item.Execute(gameObject, target);
            
            if (item.Category == ItemType.Consumable)
                _inventory.ConsumeItem(_selectedInventorySlot);
            
            return true;
        }
        
        // ==========================================================
        //  Saving & Loading
        // ==========================================================
        
        public void Save(ref PlayerSaveData data)
        {                            
            data.Inventory = new List<InventorySaveData>();         // Wipe previous saved inventory, save current inventory
            _inventory.Save(ref data.Inventory);

            data.Position = _lastRespawnPosition;
            data.MaxHealth = _health.MaxHp;
            data.CurrentHealth = _health.CurrentHp;
        }

        public void Load(PlayerSaveData data)
        {
            _inventory.Load(data.Inventory);

            _lastRespawnPosition = data.Position;
            _health.InitHealthBar(data.MaxHealth);
            _health.SetCurrentHealth(data.CurrentHealth);
        }
    }
}
