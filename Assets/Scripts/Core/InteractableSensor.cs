using UnityEngine;

namespace SpookyGame.Core
{
    public class InteractableSensor : MonoBehaviour
    {       
        [SerializeField] private Transform cameraTransform;
        
        [Header("Sensor Settings")]
        [SerializeField] private float interactDistance = 1.5f;
        [SerializeField] private float castRadius = 0.3f; // Controls the "thickness" of the ray
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
            
            Interactable hitInteractable = null;
            
            // Perform the spatial data transform (SphereCast)
            // Parameters: Origin, Radius, Direction, out HitInfo, Distance, LayerMask
            if (Physics.SphereCast(originTransform.position, castRadius, originTransform.forward, out RaycastHit hit, interactDistance, interactableLayer))
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
                _currentInteractable.Interact(gameObject.transform.root.gameObject);
                
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


        private void OnDrawGizmosSelected()
        {
            // Draws the spherecast area in the Unity Editor so you can visualize the radius and distance
            Transform originTransform = cameraTransform ? cameraTransform : transform;
            
            Gizmos.color = Color.yellow;
            
            // Draw starting sphere
            Gizmos.DrawWireSphere(originTransform.position, castRadius);
            // Draw ending sphere at max distance
            Vector3 endPosition = originTransform.position + (originTransform.forward * interactDistance);
            Gizmos.DrawWireSphere(endPosition, castRadius);
            
            // Draw connecting lines to form the "cylinder" 
            Gizmos.DrawLine(originTransform.position + originTransform.up * castRadius, endPosition + originTransform.up * castRadius);
            Gizmos.DrawLine(originTransform.position - originTransform.up * castRadius, endPosition - originTransform.up * castRadius);
            Gizmos.DrawLine(originTransform.position + originTransform.right * castRadius, endPosition + originTransform.right * castRadius);
            Gizmos.DrawLine(originTransform.position - originTransform.right * castRadius, endPosition - originTransform.right * castRadius);
        }
    }
}
