using SpookyGame.Input;
using SpookyGame.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpookyGame.Player
{
    /// <summary>
    /// First-person look. Yaw rotates the player body (this transform) so movement stays
    /// camera-relative; pitch rotates only the camera pivot. Driven by the orchestrating
    /// PlayerController via <see cref="Tick"/> rather than reading input itself.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Tooltip("Transform the camera is parented to. Pitched on the local X axis.")]
        [SerializeField] private Transform _cameraPivot;
        [Tooltip("Child of the player body at eye height. Holds the raw look target; parent the flashlight here.")]
        [SerializeField] private Transform _aimTarget;
 
        [Header("Smoothing")]
        [Tooltip("How quickly the head catches up to the look target (16 == ~1/seconds). Higher = snappier. 30+ is effectively instant.")]
        [SerializeField] private float _lookSharpness = 12f;
        [SerializeField] private bool _invertY = false;
 
        [Header("Sensitivity (fallbacks until GameSettings is wired)")]
        [Tooltip("Degrees per pointer count (mouse). Applied per delta, no dt.")]
        [SerializeField] private float _mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full stick deflection (gamepad). Applied as a rate, scaled by dt.")]
        [SerializeField] private float _gamepadSensitivity = 180f;
 
        [Header("Pitch Limits")]
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursorOnPlay = true;
        
        private float _targetYaw, _targetPitch;   // where input says to look (drives AimTarget)
        private float _yaw, _pitch;               // smoothed values applied to body/pivot
 
        public Transform AimTarget => _aimTarget;
 
        /// <summary>Remaining yaw/pitch to traverse (degrees). Useful for procedural lean/lag effects later.</summary>
        public Vector2 LookLag => new Vector2(_targetYaw - _yaw, _targetPitch - _pitch);
 
        private void Awake()
        {
            _yaw = _targetYaw = transform.eulerAngles.y;
            if (_cameraPivot != null)
                _pitch = _targetPitch = NormalizePitch(_cameraPivot.localEulerAngles.x);
        }
 
        private void OnEnable()
        {
            if (_lockCursorOnPlay) LockCursor(true);
            RefreshSettings();
            // TODO(settings): GameSettings.ControlsChanged += RefreshSettings;
        }
 
        private void OnDisable()
        {
            LockCursor(false);
            // TODO(settings): GameSettings.ControlsChanged -= RefreshSettings;
        }
 
        /// <summary>Re-reads user settings. Wire to the GameSettings change event.</summary>
        public void RefreshSettings()
        {
            // TODO(settings):
            // _mouseSensitivity   = GameSettings.MouseSensitivity;
            // _gamepadSensitivity = GameSettings.GamepadSensitivity;
            _invertY            = GameSettings.InvertY;   // if/when exposed
        }
 
        /// <summary>
        /// Apply a frame of look input. Call once per rendered frame (Update).
        /// Ignored while the cursor is unlocked.
        /// </summary>
        public void Tick(Vector2 lookDelta, ControlDeviceType deviceType, float dt)
        {
             if (Cursor.lockState != CursorLockMode.Locked)
                 return;
            
             // Mouse deltas are per-frame displacements; gamepad sticks are a rate
             // and must be scaled by dt to stay framerate-independent.
             float yawDelta, pitchDelta;
             if (deviceType == ControlDeviceType.Gamepad)
             {
                 yawDelta   = lookDelta.x * _gamepadSensitivity * dt;
                 pitchDelta = lookDelta.y * _gamepadSensitivity * dt;
             }
             else
             {
                 yawDelta   = lookDelta.x * _mouseSensitivity;
                 pitchDelta = lookDelta.y * _mouseSensitivity;
             }
 
             _targetYaw   += yawDelta;
             _targetPitch += pitchDelta * (_invertY ? 1f : -1f);
             _targetPitch  = Mathf.Clamp(_targetPitch, _minPitch, _maxPitch);
 
             // Both angles accumulate continuously, so plain Lerp is correct here:
             // there is never a +/-180 wrap discontinuity between current and target.
             float k = 1f - Mathf.Exp(-_lookSharpness * dt);
             _yaw   = Mathf.Lerp(_yaw, _targetYaw, k);
             _pitch = Mathf.Lerp(_pitch, _targetPitch, k);
 
             // Periodically rebase yaw to protect float precision during long sessions.
             // Both values shift together, so nothing observable changes.
             if (Mathf.Abs(_targetYaw) > 720f)
             {
                 float rebase = Mathf.Round(_targetYaw / 360f) * 360f;
                 _targetYaw -= rebase;
                 _yaw       -= rebase;
             }
 
             transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
             if (_cameraPivot != null)
                 _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
 
             // AimTarget is a child of the body (already rotated by _yaw), so its
             // local yaw is the remaining lag; pitch is applied directly.
             if (_aimTarget != null)
                 _aimTarget.localRotation = Quaternion.Euler(_targetPitch, _targetYaw - _yaw, 0f);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
 
        private static float NormalizePitch(float euler) => euler > 180f ? euler - 360f : euler;
    }
}
