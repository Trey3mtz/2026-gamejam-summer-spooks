using UnityEngine;

namespace SpookyGame.Interfaces
{
    public interface IInteractable
    {
        public bool CanInteract(GameObject interactor);
        public void Interact(GameObject interactor);
        public void DisplayPrompt();
        public void HidePrompt();
    }
}
