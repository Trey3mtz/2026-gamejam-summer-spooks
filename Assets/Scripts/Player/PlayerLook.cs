using UnityEngine;
using UnityEngine.InputSystem;

namespace SummerSpooks.Player
{
    /// <summary>
    /// First-person look. Yaw rotates the player body (this transform) so movement stays
    /// camera-relative; pitch rotates only the camera pivot. Driven by the orchestrating
    /// PlayerController via <see cref="Tick"/> rather than reading input itself.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Tooltip("Transform the camera is parented to (or the camera itself). Pitched on the local X axis.")]
        [SerializeField] private Transform _cameraPivot;

        [Header("Sensitivity")]
        [Tooltip("Degrees of rotation per unit of pointer delta.")]
        [SerializeField] private float _sensitivity = 0.08f;
        [SerializeField] private bool _invertY = false;

        [Header("Pitch Limits")]
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursorOnPlay = true;

        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _yaw = transform.eulerAngles.y;
            if (_cameraPivot != null)
                _pitch = NormalizePitch(_cameraPivot.localEulerAngles.x);
        }

        private void OnEnable()
        {
            if (_lockCursorOnPlay) LockCursor(true);
        }

        private void OnDisable()
        {
            LockCursor(false);
        }

        /// <summary>Apply a frame of pointer delta. Ignored while the cursor is unlocked.</summary>
        public void Tick(Vector2 lookDelta)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            _yaw += lookDelta.x * _sensitivity;
            _pitch += lookDelta.y * _sensitivity * (_invertY ? 1f : -1f);
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraPivot != null)
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Update()
        {
            // Lightweight cursor handling so the editor user is never trapped:
            // Esc frees the cursor, clicking the game view re-locks it.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                LockCursor(false);

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float NormalizePitch(float euler)
        {
            return euler > 180f ? euler - 360f : euler;
        }
    }
}
