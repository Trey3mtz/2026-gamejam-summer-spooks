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
        public List<InventoryEntry> Inventory => _inventory.GetInventory();
        
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

        private int _selectedInventorySlot = 0;
        private int _invItemCount => Inventory.Count;
        private int _invCapacity => Inventory.Capacity;

        public void SelectNextItem()
        {
            if(_invItemCount <= 0)
                return;

            int nextSlotIndex = _selectedInventorySlot + 1;
            if(nextSlotIndex > _invItemCount)
                nextSlotIndex = 0;

            _selectedInventorySlot = nextSlotIndex;
        }
 
        public void SelectPreviousItem()
        {
            if(_invItemCount <= 0)
                return;

            int prevSlotIndex = _selectedInventorySlot - 1;
            if(prevSlotIndex < 0)
                prevSlotIndex = _invItemCount;

            _selectedInventorySlot = prevSlotIndex;
        }

        public void SelectSpecificItem(int i)
        {
            if(_invItemCount <= 0)
                return;

            if(i < 0)
                i = 0;
            if(i > _invItemCount)
                i = _invItemCount;

            _selectedInventorySlot = i;
        }

        public bool TryUseItem()
        {
            
            
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
