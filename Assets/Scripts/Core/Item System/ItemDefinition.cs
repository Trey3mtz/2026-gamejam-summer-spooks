using System.Collections.Generic;
using UnityEngine;
using SpookyGame.Interfaces;

namespace SpookyGame.Core.Item_System
{
    [CreateAssetMenu(fileName = "New Item", menuName = "SpookyGame/Create Item Definition")]
    public class ItemDefinition : ScriptableObject 
    {
        [Header("Identity")]
        public string ItemName;
        private uint ID;
        public uint ItemID => ID;
        
        
        [Header("Visuals")]
        public Sprite Icon;
        [Tooltip("The prefab that will be spawned in the world before the player picks up this item.")]
        public GameObject WorldPrefab; 
        public AudioClip UseSound;

        [Header("Data")]
        public bool IsStackable = true;
        public int MaxStackSize = 99;
        public ItemType Category;
        
        // This allows us to embed lightweight C# classes directly into the inspector.
        [Tooltip("Logic pieces that will execute when this item is used.")]
        [SerializeReference, SubclassPicker]
        public List<IItemEffect> Effects = new List<IItemEffect>();

        // Generate the hash automatically so you never have to type it.
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ItemName)) return;
            // Generate a deterministic integer from the name
            ID = (uint)ItemName.GetHashCode(); 
        }
    }

    public enum ItemType { Consumable, Weapon, Material, Key }
}
