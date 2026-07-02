using System;
using SpookyGame.Interfaces;
using UnityEngine;

namespace SpookyGame.Core
{
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        [SerializeField] private InteractionPrompt interactionPrompt;
        [SerializeField] private string promptText = "Press [E] to Interact";
        public abstract bool CanInteract(GameObject interactor);
        public abstract void Interact(GameObject interactor);
        public Action OnDisplayPrompt;
        public Action OnHidePrompt;
        public void DisplayPrompt()
        {
            interactionPrompt.Show(promptText,  transform);
            OnDisplayPrompt?.Invoke();
        }

        public void HidePrompt()
        {
            interactionPrompt.Hide();
            OnHidePrompt?.Invoke();
        }
    }
}
