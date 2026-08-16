using System;
using System.Collections.Generic;
using SpookyGame.Core;
using SpookyGame.Core.Item_Effects;
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
        
        private PlayerInventory _inventory = new PlayerInventory();
        
        public PlayerInventory Inventory => _inventory;
        
        private Vector3 _lastRespawnPosition;
        
        private void Awake()
        {
            _health = new HealthBar();
            _health.InitHealthBar(10);
            
            _lastRespawnPosition = transform.position;
        }

        // ================================================================
        //  Item API & helper methods
        // ================================================================
        
        public bool IsHolstered { get; private set; }
        public event Action<bool> HolsterChanged = delegate { };
        
        public void SetHolstered(bool holstered)
        {
            if (IsHolstered == holstered) return;
            IsHolstered = holstered;
            HolsterChanged(holstered);
        }
        
        public void ToggleHolster() => SetHolstered(!IsHolstered);

        
        [SerializeField] private BatteryLight _flashlight = new BatteryLight();
        [SerializeField] private BatteryLight _blacklight = new BatteryLight();

        public BatteryLight Flashlight => _flashlight;
        public BatteryLight Blacklight => _blacklight;
        
        public bool ToggleLight(LightKind kind) => GetLight(kind).Toggle();

        private void EvaluateHeldLight()
        {
            LightKind? held = IsHolstered ? null : GetLightKind(Inventory.SelectedItem);

            if (_flashlight.IsOn && held != LightKind.Flashlight) _flashlight.TurnOff();
            if (_blacklight.IsOn && held != LightKind.Blacklight) _blacklight.TurnOff();
        }
   
        private static LightKind? GetLightKind(ItemDefinition item)
        {
            if (!item) return null;
            foreach (var effect in item.Effects)
                if (effect is ToggleLightEffect toggle)
                    return toggle.Kind;
            return null;
        }
        
        private BatteryLight GetLight(LightKind kind)
            => kind == LightKind.Flashlight ? _flashlight : _blacklight;
            
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

            data.FlashlightBatteryLife = _flashlight.BatteryLife;
            data.BlacklightBatteryLife = _blacklight.BatteryLife;
        }

        public void Load(PlayerSaveData data)
        {
            Inventory.Load(data.Inventory);

            _lastRespawnPosition = data.Position;
            _health.InitHealthBar(data.MaxHealth);
            _health.SetCurrentHealth(data.CurrentHealth);

            _flashlight.BatteryLife = data.FlashlightBatteryLife;
            _blacklight.BatteryLife = data.BlacklightBatteryLife;
        }
    }
}
