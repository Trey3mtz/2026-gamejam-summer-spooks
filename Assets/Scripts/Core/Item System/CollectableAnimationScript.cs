using UnityEngine;
using DG.Tweening; // Required for DOTween
namespace SpookyGame.Core.Item_System
{
    public class CollectableAnimationScript : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("The speed and axis of the rotation. Default rotates on the Y axis.")]
        [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);
    
        [Header("Hover Settings (DOTween)")]
        [Tooltip("The time in seconds it takes to move up or down once.")]
        [SerializeField] private float hoverDuration = 1f;
        [Tooltip("The distance it moves up and down from its starting position.")]
        [SerializeField] private float hoverHeight = 0.25f;
    
        private void Start()
        {
            // Use DOTween to animate the Y position.
            // Move to current Y + hoverHeight over hoverDuration.
            // SetLoops(-1, LoopType.Yoyo) tells it to loop infinitely back and forth.
            // SetEase(Ease.InOutSine) provides the smooth deceleration at the top and bottom of the hover.
            transform.DOMoveY(transform.position.y + hoverHeight, hoverDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    
        private void Update()
        {
            // Keep the rotation in Update as requested, ensuring it remains frame-rate independent
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    
        private void OnDestroy()
        {
            // Always kill tweens on this object when it is destroyed to prevent DOTween from 
            // trying to move an object that no longer exists (e.g., when the player collects it).
            transform.DOKill();
        }
    }
}
