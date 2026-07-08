using UnityEngine;
using SpookyGame.Core.Item_System;
using SpookyGame.Player;
using TMPro;

namespace SpookyGame.Core.Interactables
{
    public class WorldItem : Interactable
    {
        [SerializeField] private ItemDefinition _itemDef;
        // NOTE: Look at Interactable.cs for the rest of the members

        private void Awake()
        {
            Validate();
        }

        private void Validate()
        {
            // 1. Search the hierarchy
            // Passing 'true' tells Unity to include inactive GameObjects in the search.
            TextMeshPro childTextComponent = TryGetComponentInChildren<TextMeshPro>(true);
            // 2. Validate the data
            if (childTextComponent == null)
            {
                // 3. Fail loudly
                Debug.LogError($"[Structural Error] {gameObject.name} requires a TextMeshPro component in its children, but none was found. Please update the prefab.", gameObject);
                
                // 4. Prevent further errors
                enabled = false; 
                return;
            }
        }
        
        
        public override bool CanInteract(GameObject interactor)
        {
            return true; // TryAddItem() already knows if something can't be added
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public override void Interact(GameObject interactor)
        {
            var player = interactor.TryGetComponent<Player.Player>();
            if(!player) return;
            
            Debug.Log("Interacting with " + gameObject.name);
            var wasSuccessful = player.Inventory.TryAddItem(_itemDef);
            
            if(wasSuccessful)
            {
                HidePrompt();
            }
        }
    }
}
