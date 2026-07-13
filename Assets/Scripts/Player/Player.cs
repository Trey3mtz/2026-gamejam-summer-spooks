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
        //  Item States
        // ================================================================

        [SerializeField] private BatteryLight _flashlight = new BatteryLight();
        [SerializeField] private BatteryLight _blacklight = new BatteryLight();


        // ================================================================
        //  Item API & helper methods
        // ================================================================

        public BatteryLight Flashlight => _flashlight;
        public BatteryLight Blacklight => _blacklight;
        
        public bool ToggleFlashlight() => _flashlight.Toggle();
        public bool ToggleBlacklight() => _blacklight.Toggle();

        private void EvaluateHeldLight()
        {
            var selected = Inventory.SelectedItem;
        
            if (_flashlight.IsOn && !IsSameItem(selected, _flashlightItem))
                _flashlight.TurnOff();
        
            if (_blacklight.IsOn && !IsSameItem(selected, _blacklightItem))
                _blacklight.TurnOff();
        }
   
        private static bool IsSameItem(ItemDefinition a, ItemDefinition b)
            => a != null && b != null && a.ItemID == b.ItemID;

        // ================================================================
        //  State Updates
        // ================================================================

        private void Update()
        {
            EvaluateHeldLight();
            _flashlight.Tick(Time.deltaTime);
            _blacklight.Tick(Time.deltaTime);
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

            data.FlashlightBatteryLife = _flashlightBatteryLife;
            data.BlacklightBatteryLife = _blacklightBatteryLife;
        }

        public void Load(PlayerSaveData data)
        {
            Inventory.Load(data.Inventory);

            _lastRespawnPosition = data.Position;
            _health.InitHealthBar(data.MaxHealth);
            _health.SetCurrentHealth(data.CurrentHealth);

            _flashlightBatteryLife = data.FlashlightBatteryLife;
            _blacklightBatteryLife = data.BlacklightBatteryLife;
        }
    }
}
