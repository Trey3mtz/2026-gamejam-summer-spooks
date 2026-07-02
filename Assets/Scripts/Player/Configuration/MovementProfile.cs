using UnityEngine;

namespace SummerSpooks.Player.Configuration
{
    /// <summary>
    /// Tunable, per-character movement feel. Lives as an asset so designers can
    /// tweak the player without touching code or the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "SummerSpooks/Movement Profile", fileName = "MovementProfile")]
    public class MovementProfile : ScriptableObject
    {
        [Header("Ground Speeds (m/s)")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeed = 6.5f;
        [SerializeField] private float _crouchSpeed = 1.8f;

        [Header("Acceleration")]
        [Tooltip("SmoothDamp time to reach target speed on the ground. Lower = snappier.")]
        [SerializeField] private float _moveSmoothTime = 0.07f;
        [Tooltip("How much steering authority you keep in the air (0 = none, 1 = full).")]
        [Range(0f, 1f)]
        [SerializeField] private float _airControl = 0.5f;

        [Header("Jump & Gravity")]
        [Tooltip("Peak height of a full jump, in meters.")]
        [SerializeField] private float _jumpHeight = 1.1f;
        [Tooltip("Downward acceleration magnitude (m/s^2). Higher feels less floaty.")]
        [SerializeField] private float _gravity = 22f;
        [Tooltip("Small constant downward speed while grounded to stay glued to slopes/steps.")]
        [SerializeField] private float _groundStickSpeed = 2f;
        [SerializeField] private float _maxFallSpeed = 45f;

        [Header("Safety Clamps")]
        [SerializeField] private float _maxHorizontalSpeed = 12f;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CrouchSpeed => _crouchSpeed;
        public float MoveSmoothTime => _moveSmoothTime;
        public float AirControl => _airControl;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float GroundStickSpeed => _groundStickSpeed;
        public float MaxFallSpeed => _maxFallSpeed;
        public float MaxHorizontalSpeed => _maxHorizontalSpeed;

        /// <summary>Initial upward velocity required to reach <see cref="JumpHeight"/> under <see cref="Gravity"/>.</summary>
        public float JumpVelocity => Mathf.Sqrt(2f * Mathf.Max(0.01f, _gravity) * Mathf.Max(0f, _jumpHeight));
    }
}
