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


        // ================================================================
        //  Inventory API
        // ================================================================
        
        // react to selection and use without polling.
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
        
        public int SelectedSlot => _selectedInventorySlot;
        public int PreviousSlot => FindPopulatedSlot(_selectedInventorySlot, -1);
        public int NextSlot     => FindPopulatedSlot(_selectedInventorySlot, +1);
        
        public void SelectNextItem()     => MoveSelection(+1);
        public void SelectPreviousItem() => MoveSelection(-1);

        /// <summary>Advances to the next non-empty slot in the given direction, wrapping.</summary>
        private void MoveSelection(int direction)
        {
            int idx = FindPopulatedSlot(_selectedInventorySlot, direction);
            if (idx >= 0) SetSelected(idx);
        }

        /// <summary>Next non-empty slot from `fromSlot` in `direction`, wrapping. -1 if none.</summary>
        private int FindPopulatedSlot(int fromSlot, int direction)
        {
            var entries = _inventory.GetInventory();
            int count = entries.Count;
            if (count == 0) return -1;
        
            for (int step = 1; step <= count; step++)
            {
                int idx = (((fromSlot + direction * step) % count) + count) % count;
                if (entries[idx].item != null)
                    return idx;
            }
            return -1;
        }

        public ItemDefinition ItemInSlot(int slot)
        {
            var entries = _inventory.GetInventory();
            if (slot < 0 || slot >= entries.Count) return null;
            return entries[slot].item;
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
        
        public void TryUseItem()
        {
            var entry = SelectedEntry;
            if (entry?.item == null) return false;
            ItemDefinition item = entry.item;
            // targetPosition is provided for effects that need a world point (spawns, throws).
            Vector3 target = transform.position;
            
            item.Execute(gameObject, target);
            
            if (item.Category == ItemType.Consumable)
                _inventory.ConsumeItem(_selectedInventorySlot);
        }

        public void TryAddItem(ItemDefinition itemDef, int amount)
        {
            bool wasSuccessful = _inventory.AddItem(itemDef, amount);
        }        

        
        // ================================================================
        //  Saving & Loading
        // ================================================================
        
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
