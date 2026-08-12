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

        
        [Header("Data")]
        public bool IsStackable = true;
        public int MaxStackSize = 99;
        public ItemType Category;        

        
        [Header("Visuals")]
        public Sprite Icon;
        [Tooltip("The prefab that will be spawned in the world before the player picks up this item.")]
        public GameObject WorldPrefab; 
        [Tooltip("Visual-only prefab shown in the hand while selected. Leave null for items that cannot be held (Keys, Materials).")]
        public GameObject HeldPrefab;


        [Header("Item Use")]
        // This allows us to embed lightweight C# classes directly into the inspector.
        [Tooltip("Logic pieces that will execute when this item is used.")]
        [SerializeReference, SubclassPicker]
        public List<IItemEffect> Effects = new List<IItemEffect>();        
        public FMODUnity.EventReference UseSound;
        


        // This is the call to use the item
        public void Execute(GameObject user, Vector3 targetPosition)
        {
            foreach(var effect in Effects)
                effect.Execute(user, targetPosition);

            // We check if the event is null (unassigned) before trying to play it to prevent errors in the console.
            if (!UseSound.IsNull)
            {
                // PlayOneShot handles creating the instance, playing it, and releasing it automatically.
                // Passing in targetPosition ensures 3D sounds spatialize correctly in the world.
                FMODUnity.RuntimeManager.PlayOneShot(UseSound, targetPosition);
            }
        }
        

        // Generate the hash automatically so you never have to type it.
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ItemName)) { ID = 0; return; }
            ID = ComputeStableId(ItemName);
        }
        
        // Deterministic FNV-1a. Unlike string.GetHashCode, stable across platforms/sessions,
        // which the save system depends on.
        private static uint ComputeStableId(string s)
        {
            const uint offset = 2166136261u, prime = 16777619u;
            uint hash = offset;
            foreach (char c in s) { hash ^= c; hash *= prime; }
            return hash;
        }
    }

    public enum ItemType { Consumable, Tool, Weapon, Material, Key } 
}
