using SpookyGame.Audio;
using SpookyGame.Core.Item_System;
using System;
using UnityEngine;

namespace SpookyGame.Core.Interactables
{
    public class LockedDoor : Interactable
    {
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _rattleSound;
        [SerializeField] private ItemDefinition _requiredKey;
        [SerializeField] private bool _consumeKeyOnUse = false;
        private bool _unlocked;
    
        public override bool CanInteract(GameObject interactor) => true; // locked doors still respond ("It's locked.")
    
        // ReSharper disable Unity.PerformanceAnalysis
        public override void Interact(GameObject interactor)
        {
            var player = interactor.transform.root.GetComponent<Player.Player>();
            if (!player) return;
    
            if (_unlocked) { Open(); return; }
    
            if (player.Inventory.Contains(_requiredKey))
            {
                _unlocked = true;
                if (_consumeKeyOnUse) player.Inventory.RemoveOne(_requiredKey);
                Open();
            }
            else
            {
                // rattle sound / "It's locked" prompt — leaving CanInteract true enables this feedback
                if (AudioManager.Instance)
                    AudioManager.Instance.PlaySoundFX(_rattleSound, transform);
            }
        }

        private void Open()
        {
            if (AudioManager.Instance)
                AudioManager.Instance.PlaySoundFX(_openSound, transform);
        }
    }
}
