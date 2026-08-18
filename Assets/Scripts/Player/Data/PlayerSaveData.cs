using UnityEngine;
using System.Collections.Generic;
using SpookyGame.Core.Item_System;


namespace SpookyGame.Player.Data
{
    [System.Serializable]
    public struct PlayerSaveData
    {
        public Vector3 Position;   // This will be their respawn position
        public List<InventorySaveData> Inventory;
        public int MaxHealth;
        public int CurrentHealth;

        public int FlashlightBatteryLife;
        public int BlacklightBatteryLife;
        public int FlashlightMagazineAmmo;
        public int FlashlightReserveAmmo;
    }  
}
