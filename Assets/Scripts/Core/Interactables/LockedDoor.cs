using SpookyGame.Core.Item_System;
using System;
using UnityEngine;

namespace SpookyGame.Core.Interactables
{
  public class LockedDoor : Interactable
  {
      [SerializeField] private FMODUnity.EventReference _openSound;
      [SerializeField] private FMODUnity.EventReference _rattleSound;
      [SerializeField] private ItemDefinition _requiredKey;
      [SerializeField] private bool _consumeKeyOnUse = false;
      private bool _unlocked;
  
      public override bool CanInteract(GameObject interactor) => true; // locked doors still respond ("It's locked.")
  
      public override void Interact(GameObject interactor)
      {
          var player = interactor.GetComponentInParent<Player>();
          if (player == null) return;
  
          if (_unlocked) { Open(); return; }
  
          if (player.Inventory.Contains(_requiredKey))
          {
              _unlocked = true;
              if (_consumeKeyOnUse) player.Inventory.RemoveOne(_requiredKey);
              Open(); // + FMOD unlock sting
          }
          else
          {
              // rattle sound / "It's locked" prompt — leaving CanInteract true enables this feedback
          }
      }

      private void Open()
      {
      
      }
  }
}
