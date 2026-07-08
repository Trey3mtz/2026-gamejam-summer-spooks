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
        
        public PlayerInventory Inventory { get; private set; }
        
        private Vector3 _lastRespawnPosition;
        
        private void Awake()
        {
            _health = new HealthBar();
            _health.InitHealthBar(10);
            
            Inventory = new PlayerInventory();
            _lastRespawnPosition = transform.position;
        }

        
        // ================================================================
        //  Saving & Loading
        // ================================================================
        
        public void Save(ref PlayerSaveData data)
        {                            
            data.Inventory = new List<InventorySaveData>();         // Wipe previous saved inventory, save current inventory
            Inventory.Save(ref data.Inventory);

            data.Position = _lastRespawnPosition;
            data.MaxHealth = _health.MaxHp;
            data.CurrentHealth = _health.CurrentHp;
        }

        public void Load(PlayerSaveData data)
        {
            Inventory.Load(data.Inventory);

            _lastRespawnPosition = data.Position;
            _health.InitHealthBar(data.MaxHealth);
            _health.SetCurrentHealth(data.CurrentHealth);
        }
    }
}
