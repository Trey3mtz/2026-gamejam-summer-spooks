using System;
using System.Collections.Generic;
using SpookyGame.Core;
using SpookyGame.Core.Item_Effects;
using SpookyGame.Core.Item_System;
using SpookyGame.Player.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SpookyGame.Player
{
    [RequireComponent(typeof(ActorHealth))]
    public class Player : MonoBehaviour
    {
        [SerializeField] private AudioClip _toggleFlashlightSfx;
        [SerializeField] private float lightSfxVolume = 1;
        [SerializeField] private Vector2 _lightSfxPitchRange = new Vector2(1f, 1.02f);
        private ActorHealth _health;
        public ActorHealth Health => _health;
        
        private PlayerInventory _inventory = new PlayerInventory();
        
        public PlayerInventory Inventory => _inventory;
        
        private Vector3 _lastRespawnPosition;
        
        private void Awake()
        {
            _health = GetComponent<ActorHealth>();
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
        [SerializeField] private FlashlightWeaponState _flashlightWeapon = new FlashlightWeaponState();

        public BatteryLight Flashlight => _flashlight;
        public BatteryLight Blacklight => _blacklight;
        public FlashlightWeaponState FlashlightWeapon => _flashlightWeapon;
        
        public bool ToggleLight(LightKind kind)
        {
            if (GetLight(kind).Toggle())
                return true;
            
            return false;
        }

        public bool IsSelectedLight(LightKind kind)
            => GetLightKind(Inventory.SelectedItem) == kind;

        public bool ToggleSelectedLight(LightKind kind)
        {
            if (!IsSelectedLight(kind)) return false;
            SetHolstered(false);
            bool result = ToggleLight(kind);
            if (result)
                Audio.AudioManager.Instance.PlaySoundFX(_toggleFlashlightSfx, transform, lightSfxVolume, Random.Range(_lightSfxPitchRange.x, _lightSfxPitchRange.y));
            else
            {
                Audio.AudioManager.Instance.PlaySoundFX(_toggleFlashlightSfx, transform, lightSfxVolume, Random.Range(_lightSfxPitchRange.x*.9f, _lightSfxPitchRange.y*.9f));
            }
            return result;
        }

        public bool TryFireFlashlight()
        {
            if (IsHolstered || !IsSelectedLight(LightKind.Flashlight)) return false;

            FlashlightShooter shooter = GetComponentInChildren<FlashlightShooter>(true);
            return shooter != null && shooter.TryFire(_flashlight, _flashlightWeapon);
        }

        public bool TryReloadFlashlight()
        {
            if (IsHolstered || !IsSelectedLight(LightKind.Flashlight))
                return false;

            return _flashlightWeapon.TryBeginReload();
        }

        public int AddFlashlightAmmo(int amount, int batteryRecharge = 0)
        {
            int added = _flashlightWeapon.AddReserveAmmo(amount);
            if (batteryRecharge > 0)
                _flashlight.Recharge(batteryRecharge);
            return added;
        }

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
            _flashlightWeapon.Tick();
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
            data.FlashlightMagazineAmmo = _flashlightWeapon.RoundsInMagazine;
            data.FlashlightReserveAmmo = _flashlightWeapon.ReserveAmmo;
        }

        public void Load(PlayerSaveData data)
        {
            Inventory.Load(data.Inventory);

            _lastRespawnPosition = data.Position;
            _health.SetMaximumHealth(data.MaxHealth);
            _health.SetCurrentHealth(data.CurrentHealth);

            _flashlight.SetBatteryLife(data.FlashlightBatteryLife);
            _blacklight.SetBatteryLife(data.BlacklightBatteryLife);
            _flashlightWeapon.SetAmmo(data.FlashlightMagazineAmmo, data.FlashlightReserveAmmo);
        }
    }
}
