using System.Collections.Generic;
using UnityEngine;

namespace SpookyGame 
{
    [CreateAssetMenu(fileName = "New Item", menuName = "SpookyGame/Item Definition")]
    public class ItemDefinition : ScriptableObject 
    {
        [Header("Identity")]
        public string ItemName;
        [field: SerializeField, ReadOnly] public uint ItemID { get; private set; } 

        [Header("Visuals")]
        public Sprite Icon;
        public GameObject WorldPrefab;
        public AudioClip UseSound;

        [Header("Data")]
        public bool IsStackable = true;
        public int MaxStackSize = 99;
        public ItemType Category;

        // Unity 6 robustly supports SerializeReference. 
        // This allows us to embed lightweight C# classes directly into the inspector.
        [SerializeReference] 
        public List<IItemEffect> Effects = new List<IItemEffect>();

        // Generate the hash automatically so you never have to type it.
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ItemName)) return;
            // Generate a deterministic integer from the name
            ItemID = (uint)ItemName.GetHashCode(); 
        }
    }

    public enum ItemType { Consumable, Weapon, Material, Key }
}
