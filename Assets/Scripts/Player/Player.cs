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

        // Out of 100. 0 means no battery.
        private int _flashlightBatteryLife = 100;
        private int _blacklightBatteryLife = 100;

        // How long its been on.
        private float _flashlightOnDuration = 0f;
        private float _blacklightOnDuration = 0f;

        // The toggle state
        private bool _flashlightOn = false;
        private bool _blacklightOn = false;


        // ================================================================
        //  Item API & helper methods
        // ================================================================

        public bool ToogleFlashlight()
        {
            if(_flashlightOn)
                _flashlightOn = false;
            else if(_flashlightBatteryLife > 0)
                _flashlightOn = true;

            return _flashlightOn;
        }

        public bool ToogleBlacklight()
        {
            if(_blacklightOn)
                _blacklightOn = false;
            else if(_blacklightBatteryLife > 0)
                _blacklightOn = true;

            return _blacklightOn;
        }

        

        private void HandleFlashlightUpdate()
        {
            // If its off, return
            if(!_flashlightOn)
                return;

            // If its on, but out of battery, force off
            if(_flashlightBatteryLife <= 0)
            {
                ToogleFlashlight();
                _flashlightBatteryLife = 0;
                _flashlightOnDuration = 0f;
                return;
            }
                
            // If its on and has battery life
            _flashlightOnDuration += Time.deltaTime;
            if(_flashlightOnDuration >= 2)
            {
                _flashlightBatteryLife -= 1;
                _flashlightOnDuration = 0f;
            }
        }

        private void HandleBlacklightUpdate()
        {
            // If its off, return
            if(!_blacklightOn)
                return;

            // If its on, but out of battery, force off
            if(_blacklightBatteryLife <= 0)
            {
                ToogleFlashlight();
                _blacklightBatteryLife = 0;
                _blacklightOnDuration = 0f;
                return;
            }
                
            // If its on and has battery life
            _blacklightOnDuration += Time.deltaTime;
            if(_blacklightOnDuration >= 2)
            {
                _blacklightBatteryLife -= 1;
                _blacklightOnDuration = 0f;
            }
        }


        // ================================================================
        //  State Updates
        // ================================================================

        private void Update()
        {
            HandleFlashlightUpdate();
            HandleBlacklightUpdate();
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
