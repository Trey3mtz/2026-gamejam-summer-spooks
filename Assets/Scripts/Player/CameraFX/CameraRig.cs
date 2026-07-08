using UnityEngine;
using SpookyGame.Player.Data;

namespace SpookyGame.Player.CameraFX
{
    /// <summary>
    /// Owns the procedural camera-motion subsystems (<see cref="HeadBob"/>,
    /// <see cref="CameraSpring"/>) and applies their combined offset to this
    /// transform in LateUpdate. Lives on a dedicated "CameraBobRoot" GameObject
    /// between the pitch pivot and the camera:
    ///
    ///   Player (yaw) -> CameraPivot (pitch) -> CameraBobRoot (this) -> Camera
    ///
    /// This node is the only transform this script writes, and nothing else
    /// writes it, so it never fights PlayerLook's rotation. PlayerController
    /// feeds it motion data each simulation step; ground transitions (jump,
    /// land) are detected here so the controller stays a pure orchestrator.
    ///
    /// Pause safety: Time.timeScale = 0 zeroes Time.deltaTime, which freezes
    /// the springs and bob phase automatically.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private HeadBob _headBob = new HeadBob();
        [SerializeField] private LookLean _lean = new LookLean();
        
        [Header("Impacts")]
        [SerializeField] private CameraSpring _verticalSpring = new CameraSpring();
        [Tooltip("Camera velocity (m/s) imparted per m/s of landing fall speed.")]
        [SerializeField] private float _landImpulseScale = 0.15f;
        [Tooltip("Cap on the landing impulse velocity (m/s).")]
        [SerializeField] private float _maxLandImpulse = 2f;
        [Tooltip("Upward kick (m/s) applied when leaving the ground with upward velocity.")]
        [SerializeField] private float _jumpImpulse = 0.4f;
        [Tooltip("Degrees of pitch dip per meter of spring compression, coupling impacts to a nod.")]
        [SerializeField] private float _impactPitchPerMeter = 40f;

        private CameraMotionData _motion;
        private bool _hasMotion;
        private bool _wasGrounded = true;
        private float _lastVerticalVelocity;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _bobEnabled = true;

        private float _lookLagYaw;
        private DynamicCameraMode _mode = DynamicCameraMode.On;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            RefreshSettings();
            GameSettings.VideoChanged += RefreshSettings;
        }

        private void OnDisable()
        {
            GameSettings.VideoChanged -= RefreshSettings;
            _headBob.ResetState();
            _verticalSpring.ResetState();
            _lean.ResetState();
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
        }

        /// <summary>Called by PlayerController after each simulation step.</summary>
        public void SetMotionData(in CameraMotionData data)
        {
            DetectGroundTransitions(in data);
            _motion = data;
            _hasMotion = true;
        }

        /// <summary>
        /// Manual hook for scripted events (scares, explosions, heavy doors).
        /// Negative values kick the camera downward.
        /// </summary>
        public void AddVerticalImpulse(float velocity) => _verticalSpring.AddImpulse(velocity);

        /// <summary>Called by PlayerController each rendered frame after PlayerLook ticks.</summary>
        public void SetLookLag(float yawLagDegrees) => _lookLagYaw = yawLagDegrees;
        
        public void RefreshSettings() => _mode = GameSettings.DynamicCamera;

        private void DetectGroundTransitions(in CameraMotionData data)
        {
            if (!_wasGrounded && data.Grounded)
            {
                // Landed: kick downward proportional to how fast we were falling.
                float fallSpeed = Mathf.Max(0f, -_lastVerticalVelocity);
                float impulse = Mathf.Min(fallSpeed * _landImpulseScale, _maxLandImpulse);
                if (impulse > 0.01f)
                    _verticalSpring.AddImpulse(-impulse);
            }
            else if (_wasGrounded && !data.Grounded && data.VerticalVelocity > 0.1f)
            {
                // Left the ground moving upward: subtle lift on jump.
                _verticalSpring.AddImpulse(_jumpImpulse);
            }

            _wasGrounded = data.Grounded;
            _lastVerticalVelocity = data.VerticalVelocity;
        }

        private void LateUpdate()
        {
            if (!_hasMotion)
                return;
        
            if (_mode == DynamicCameraMode.Off)
            {
                _headBob.ResetState();
                _verticalSpring.ResetState();
                _lean.ResetState();
                transform.localPosition = _basePosition;
                transform.localRotation = _baseRotation;
                return;
            }
        
            bool full = _mode == DynamicCameraMode.On;
            float intensity = full ? 1f : 0.5f;   // Lite damps everything that remains by half
        
            float dt = Time.deltaTime;
            _verticalSpring.Tick(dt);
        
            float springValue = _verticalSpring.Value * intensity;
            Vector3 positionOffset = Vector3.up * springValue;
            Quaternion rotationOffset = Quaternion.Euler(-springValue * _impactPitchPerMeter, 0f, 0f);
        
            if (full)
            {
                if (_bobEnabled)
                {
                    _headBob.Tick(in _motion, dt);
                    positionOffset += _headBob.PositionOffset;
                    rotationOffset = _headBob.RotationOffset * rotationOffset;
                }
                else
                {
                    _headBob.ResetState();
                }
        
                _lean.Tick(_lookLagYaw, in _motion, dt);
                rotationOffset = _lean.RotationOffset * rotationOffset;
                positionOffset += _lean.PositionOffset;
            }
            else
            {
                _headBob.ResetState();
                _lean.ResetState();
            }
        
            transform.localPosition = _basePosition + positionOffset;
            transform.localRotation = _baseRotation * rotationOffset;
        }
    }
}
