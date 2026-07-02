using UnityEngine;

namespace SpookyGame.Core
{
    public class InteractableSensor : MonoBehaviour
    {       
        [SerializeField] private Transform cameraTransform;
        
        [Header("Raycast Settings")]
        [SerializeField] private float interactDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;

        // Tracks the current state of what the player is looking at
        private Interactable _currentInteractable;

        private void Update()
        {
            PerformSensorCheck();
        }

        private void PerformSensorCheck()
        {
            // Defensive check: if no camera is assigned, default to this transform
            Transform originTransform = cameraTransform ? cameraTransform : transform;
            
            Ray ray = new Ray(originTransform.position, originTransform.forward);
            Interactable hitInteractable = null;

            // Perform the spatial data transform (Raycast)
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
            {
                // Grab the interactable component. 
                if (hit.collider.TryGetComponent(out Interactable interactable))
                {
                    // Check our business rules: Is it actually valid to interact with right now?
                    if (interactable.CanInteract(gameObject))
                    {
                        hitInteractable = interactable;
                    }
                }
            }

            // Evaluate state changes based on the data gathered
            HandleInteractableStateTransition(hitInteractable);
        }

        private void HandleInteractableStateTransition(Interactable newInteractable)
        {
            // If we are looking at something different than last frame...
            if (_currentInteractable != newInteractable)
            {
                if (_currentInteractable)
                    _currentInteractable.HidePrompt();
                
                _currentInteractable = newInteractable;
                
                if (_currentInteractable)
                    _currentInteractable.DisplayPrompt();
            }
        }

        /// <summary>
        /// Call this method via your Input System actions (e.g., when the Interact/E key is pressed).
        /// </summary>
        public void TryInteract()
        {
            // If we currently have a validated target in our data state, execute it
            if (_currentInteractable && _currentInteractable.CanInteract(gameObject.transform.root.gameObject))
            {
                _currentInteractable.Interact(gameObject);
                
                // Re-evaluate immediately after interacting. 
                PerformSensorCheck();
            }
        }

        private void OnDisable()
        {
            // Clean up UI if the player component disables mid-frame
            if (_currentInteractable != null)
            {
                _currentInteractable.HidePrompt();
                _currentInteractable = null;
            }
        }
    }
}