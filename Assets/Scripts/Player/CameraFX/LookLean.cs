using System;
using UnityEngine;

namespace SpookyGame.Player.CameraFX
{
    /// <summary>
    /// Rolls the camera into the direction it is turning, like a head leaning
    /// into a glance. Driven by PlayerLook.LookLag.x (remaining yaw, degrees):
    /// the farther the head still has to travel, the harder the lean.
    /// Owned and ticked by CameraRig; outputs a rotation offset only.
    /// </summary>
    [Serializable]
    public class LookLean
    {
        [Tooltip("Maximum roll in degrees.")]
        [SerializeField] private float _maxLeanDegrees = 3f;
        [Tooltip("Degrees of roll per degree of remaining yaw lag.")]
        [SerializeField] private float _lagToLean = 0.2f;
        [Tooltip("How quickly the lean eases toward its target. Higher = snappier.")]
        [SerializeField] private float _blendSharpness = 10f;

        private float _roll;

        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;

        public void Tick(float yawLagDegrees, float dt)
        {
            // Turning right → positive yaw lag → lean right → negative Z roll in Unity.
            float target = Mathf.Clamp(-yawLagDegrees * _lagToLean, -_maxLeanDegrees, _maxLeanDegrees);

            float k = 1f - Mathf.Exp(-_blendSharpness * dt);
            _roll = Mathf.Lerp(_roll, target, k);
            RotationOffset = Quaternion.Euler(0f, 0f, _roll);
        }

        public void ResetState()
        {
            _roll = 0f;
            RotationOffset = Quaternion.identity;
        }
    }
}
