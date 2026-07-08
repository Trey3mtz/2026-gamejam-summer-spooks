using System;
using System.Collections.Generic;
using SpookyGame.Core.Item_System;
using UnityEngine;
using SpookyGame.Interfaces;
using SpookyGame.Player.Data;

namespace SpookyGame.Player
{
    public class PlayerInventory
    {
        private Inventory _inventory;
        public Inventory Data => _inventory;

        private int _selectedInventorySlot = 0;
        private int _invItemCount => Inventory.Count;
        private int _invCapacity => Inventory.Capacity;

        // Events you can react to
        public event Action<int> OnSelectionChanged;   // selected slot index
        public event Action InventoryChanged;  
        public event Action<ItemDefinition> OnItemUsed;
        
        public PlayerInventory()
        {
            _inventory = new Inventory();
            _inventory.InitializeInventory();
            _inventory.onInventoryChanged += (_, __) => InventoryChanged?.Invoke();
        }
        
        // ================================================================
        //  Inventory API
        // ================================================================

        public List<Inventory.InventoryEntry> Inventory => _inventory.GetInventory();               
        public ItemDefinition SelectedItem => SelectedEntry?.item;
        
        public int SelectedSlot => _selectedInventorySlot;
        public int PreviousSlot => FindPopulatedSlot(_selectedInventorySlot, -1);
        public int NextSlot     => FindPopulatedSlot(_selectedInventorySlot, +1);
        
        public void SelectNextItem()     => MoveSelection(+1);
        public void SelectPreviousItem() => MoveSelection(-1);

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

        public void TryUseItem(GameObject gameObject)
        {
            var entry = SelectedEntry;
            if (!entry?.item) return;
            ItemDefinition item = entry.item;
            // targetPosition is provided for effects that need a world point (spawns, throws).
            Vector3 target = gameObject.transform.position;
            
            item.Execute(gameObject, target);
            
            if (item.Category == ItemType.Consumable)
                _inventory.ConsumeItem(_selectedInventorySlot);
        }

        public void TryAddItem(ItemDefinition itemDef, int amount = 1)
        {
            bool wasSuccessful = _inventory.AddItem(itemDef, amount);
        }        


        // ================================================================
        //  Helper methods
        // ================================================================

        private void SetSelected(int index)
        {
            if (index == _selectedInventorySlot) return;
            _selectedInventorySlot = index;
            OnSelectionChanged?.Invoke(_selectedInventorySlot);
        }

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

        // --- Persistence ---
        public void Save(ref List<InventorySaveData> data) => _inventory.Save(ref data);
        public void Load(List<InventorySaveData> data)      => _inventory.Load(data);
    }
}
