using UnityEngine;

namespace SpookyGame.Core.Interactables
{
    public class InteractableTEST : Interactable
    {
        public override bool CanInteract(GameObject interactor)
        {
            return true;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public override void Interact(GameObject interactor)
        {
            Debug.Log("Interacting with " + gameObject.name);
        }
    }
}