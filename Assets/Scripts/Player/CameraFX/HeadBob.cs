using System;
using UnityEngine;
using SpookyGame.Player.Data;

namespace SpookyGame.Player.CameraFX
{
    /// <summary>
    /// Plain C# subsystem that produces a positional/rotational offset simulating
    /// footstep bob and idle breathing sway. Owned and ticked by <see cref="CameraRig"/>,
    /// mirroring the Inventory/HealthBar subsystem pattern. It never touches a
    /// Transform itself; it only outputs offsets.
    ///
    /// Motion model:
    ///  - Lateral sway runs at step frequency; vertical bob at double frequency
    ///    (two footfalls per left-right sway cycle), with roll coupled to the sway.
    ///  - Amplitude/frequency targets come from the movement state (walk/sprint/crouch)
    ///    and every value is blended exponentially, so starting, stopping, and state
    ///    changes always ease in and out rather than snapping.
    ///  - A slow "breathing" sway fades in as movement fades out, so the camera is
    ///    never perfectly dead while idle.
    /// </summary>
    [Serializable]
    public class HeadBob
    {
        [Header("Amplitudes (meters)")]
        [SerializeField] private float _walkAmplitude   = 0.035f;
        [SerializeField] private float _sprintAmplitude = 0.075f;
        [SerializeField] private float _crouchAmplitude = 0.02f;

        [Header("Step Frequencies (Hz)")]
        [SerializeField] private float _walkFrequency   = 1.7f;
        [SerializeField] private float _sprintFrequency = 2.5f;
        [SerializeField] private float _crouchFrequency = 1.2f;

        [Header("Shape")]
        [Tooltip("Horizontal sway amplitude relative to the vertical bob amplitude.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lateralRatio = 0.6f;
        [Tooltip("Camera roll induced by lateral sway, in degrees at full intensity.")]
        [SerializeField] private float _rollDegrees = 0.4f;

        [Header("Idle Breathing")]
        [SerializeField] private float _idleAmplitude = 0.007f;
        [SerializeField] private float _idleFrequency = 0.22f;

        [Header("Blending")]
        [Tooltip("Planar speed (m/s) at which the bob reaches full intensity.")]
        [SerializeField] private float _referenceSpeed = 3f;
        [Tooltip("How quickly intensity eases between states. Higher = snappier.")]
        [SerializeField] private float _blendSharpness = 8f;

        private float _stepPhase;   // radians, one full cycle = one left-right sway
        private float _idlePhase;   // radians
        private float _weight;      // 0 = idle, 1 = full movement bob
        private float _amplitude;   // smoothed current amplitude
        private float _frequency;   // smoothed current frequency

        public Vector3 PositionOffset { get; private set; }
        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;

        public void Tick(in CameraMotionData data, float dt)
        {
            // Pick the target profile from the movement state.
            float targetAmplitude;
            float targetFrequency;
            if (data.Crouching)      { targetAmplitude = _crouchAmplitude; targetFrequency = _crouchFrequency; }
            else if (data.Sprinting) { targetAmplitude = _sprintAmplitude; targetFrequency = _sprintFrequency; }
            else                     { targetAmplitude = _walkAmplitude;   targetFrequency = _walkFrequency; }

            float speed01 = Mathf.Clamp01(data.PlanarSpeed / _referenceSpeed);
            float targetWeight = data.Grounded ? speed01 : 0f; // no footsteps in the air

            // Exponential, framerate-independent smoothing keeps every transition eased.
            float k = 1f - Mathf.Exp(-_blendSharpness * dt);
            _weight    = Mathf.Lerp(_weight, targetWeight, k);
            _amplitude = Mathf.Lerp(_amplitude, targetAmplitude, k);
            _frequency = Mathf.Lerp(_frequency, targetFrequency, k);

            _stepPhase += _frequency * 2f * Mathf.PI * dt;
            _idlePhase += _idleFrequency * 2f * Mathf.PI * dt;

            float lateral  = Mathf.Sin(_stepPhase) * _amplitude * _lateralRatio * _weight;
            float vertical = Mathf.Sin(_stepPhase * 2f) * _amplitude * _weight;

            // Breathing fades in as movement fades out.
            float breathe = Mathf.Sin(_idlePhase) * _idleAmplitude * (1f - _weight);

            PositionOffset = new Vector3(lateral, vertical + breathe, 0f);

            float roll = -Mathf.Sin(_stepPhase) * _rollDegrees * _weight;
            RotationOffset = Quaternion.Euler(0f, 0f, roll);
        }

        public void ResetState()
        {
            _weight = 0f;
            PositionOffset = Vector3.zero;
            RotationOffset = Quaternion.identity;
        }
    }
}
