using System;
using UnityEngine;
using SpookyGame.Player.Data;

namespace SpookyGame.Player.CameraFX
{
    /// <summary>
    /// Rolls the camera into motion: leaning into a turn (driven by
    /// PlayerLook.LookLag yaw) and into a strafe (driven by signed local
    /// lateral velocity). Both contributions merge into a single smoothed
    /// roll so they never fight. A small lateral position shift sells the
    /// lean as a head movement rather than a pure rotation.
    /// </summary>
    [Serializable]
    public class LookLean
    {
        [Header("Turn Lean")]
        [Tooltip("Degrees of roll per degree of remaining yaw lag.")]
        [SerializeField] private float _lagToLean = 0.2f;
        [Tooltip("Cap on the turn-lean contribution, degrees.")]
        [SerializeField] private float _maxTurnLean = 3f;

        [Header("Strafe Lean")]
        [Tooltip("Degrees of roll per m/s of lateral speed.")]
        [SerializeField] private float _speedToLean = 0.8f;
        [Tooltip("Cap on the strafe-lean contribution, degrees.")]
        [SerializeField] private float _maxStrafeLean = 2.5f;
        [Tooltip("Strafe lean fades out in the air (no ground to push against).")]
        [SerializeField] private bool _groundedOnly = true;

        [Header("Blending")]
        [Tooltip("How quickly the lean eases toward its target. Higher = snappier.")]
        [SerializeField] private float _blendSharpness = 10f;
        [Tooltip("Meters of sideways head shift per degree of roll. 0 disables.")]
        [SerializeField] private float _positionShiftPerDegree = 0.004f;

        private float _roll;

        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;
        public Vector3 PositionOffset { get; private set; }

        public void Tick(float yawLagDegrees, in CameraMotionData motion, float dt)
        {
            // Positive lag / positive lateral speed = rightward = negative Z roll.
            float turn = Mathf.Clamp(-yawLagDegrees * _lagToLean, -_maxTurnLean, _maxTurnLean);

            float strafe = Mathf.Clamp(-motion.LateralSpeed * _speedToLean, -_maxStrafeLean, _maxStrafeLean);
            if (_groundedOnly && !motion.Grounded)
                strafe = 0f;

            float target = turn + strafe;

            float k = 1f - Mathf.Exp(-_blendSharpness * dt);
            _roll = Mathf.Lerp(_roll, target, k);

            RotationOffset = Quaternion.Euler(0f, 0f, _roll);
            // Head shifts toward the lean side: negative roll (right lean) → +X shift.
            PositionOffset = new Vector3(-_roll * _positionShiftPerDegree, 0f, 0f);
        }

        public void ResetState()
        {
            _roll = 0f;
            RotationOffset = Quaternion.identity;
            PositionOffset = Vector3.zero;
        }
    }
}
