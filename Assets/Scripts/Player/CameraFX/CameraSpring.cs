using System;
using UnityEngine;

namespace SpookyGame.Player.CameraFX
{
    /// <summary>
    /// One-dimensional damped spring used for transient camera kicks
    /// (landing thumps, jump lift-off, scripted scares). Impulses inject
    /// velocity; the spring always settles back to zero.
    /// </summary>
    [Serializable]
    public class CameraSpring
    {
        [Tooltip("Restoring force per meter of displacement. Higher = faster oscillation.")]
        [SerializeField] private float _stiffness = 160f;
        [Tooltip("Velocity damping. Raise this to kill overshoot; ~2*sqrt(stiffness) is critically damped.")]
        [SerializeField] private float _damping = 16f;

        private float _value;
        private float _velocity;

        /// <summary>Current displacement from rest, in meters.</summary>
        public float Value => _value;

        /// <summary>Injects velocity (m/s). Negative = downward kick.</summary>
        public void AddImpulse(float velocity) => _velocity += velocity;

        public void Tick(float dt)
        {
            // Semi-implicit Euler: stable for the stiffness range used here.
            _velocity += (-_stiffness * _value - _damping * _velocity) * dt;
            _value += _velocity * dt;
        }

        public void ResetState()
        {
            _value = 0f;
            _velocity = 0f;
        }
    }
}
