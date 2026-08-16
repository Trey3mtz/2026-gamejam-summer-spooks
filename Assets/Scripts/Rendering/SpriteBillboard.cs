using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Rendering
{
    /// <summary>
    /// Keeps a 2D character readable inside the 3D first-person world.
    /// The optional NavMeshAgent reference also flips a walking sprite toward
    /// its current screen-space travel direction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteBillboard : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private NavMeshAgent _movementAgent;
        [SerializeField] private bool _flipWithMovement = true;

        private Camera _playerCamera;

        private void Awake()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (_movementAgent == null)
                _movementAgent = GetComponentInParent<NavMeshAgent>();
        }

        private void LateUpdate()
        {
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_playerCamera == null)
                return;

            Vector3 cameraForward = _playerCamera.transform.forward;
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(cameraForward.normalized, Vector3.up);

            if (!_flipWithMovement || _spriteRenderer == null || _movementAgent == null)
                return;

            float horizontalMotion = Vector3.Dot(_movementAgent.velocity, _playerCamera.transform.right);
            if (Mathf.Abs(horizontalMotion) > 0.05f)
                _spriteRenderer.flipX = horizontalMotion < 0f;
        }
    }
}
