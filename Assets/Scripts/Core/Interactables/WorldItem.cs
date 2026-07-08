using UnityEngine;
using SpookyGame.Core.Item_System;
using SpookyGame.Player;

namespace SpookyGame.Core.Interactables
{
    public class WorldItem : Interactable
    {
        [SerializeField] private ItemDefinition _itemDef;
        // NOTE: Look at Interactable.cs for the rest of the members
        
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
