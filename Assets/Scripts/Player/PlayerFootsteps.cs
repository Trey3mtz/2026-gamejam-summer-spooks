using SpookyGame.Audio;
using SpookyGame.Utilities;
using UnityEngine;

namespace SpookyGame.Player
{
    /// <summary>
    /// Plays footstep SFX while the player is grounded and moving. Cadence is
    /// distance-based: a step fires every StrideLength meters of planar travel, so
    /// sprinting naturally steps faster and crouching slower with a single tunable.
    /// Random pitch keeps one clip from sounding machine-gunned.
    /// Sits on the Player next to <see cref="PlayerController"/>.
    /// </summary>
    public class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioClip _footstepSFX;
        [Tooltip("Meters of horizontal travel between steps.")]
        [SerializeField] private float _strideLength = 2.2f;
        [SerializeField] private float _volume = 0.8f;
        [Tooltip("Each step's pitch is randomized in this range (x = min, y = max).")]
        [SerializeField] private Vector2 _pitchRange = new Vector2(0.92f, 1.08f);
        [Tooltip("Planar speed (m/s) below which we are considered standing still.")]
        [SerializeField] private float _minSpeed = 0.5f;
        [Tooltip("Optional. Found on this GameObject if left empty.")]
        [SerializeField] private PlayerController _controller;

        private float _distanceSinceStep;
        private bool _wasGrounded;

        private void Awake()
        {
            if (!_controller)
                _controller = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (!_controller || !_footstepSFX)
                return;

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsPaused)
                return;

            bool grounded = _controller.Grounded;
            Vector3 velocity = _controller.Velocity;
            float planarSpeed = new Vector2(velocity.x, velocity.z).magnitude;

            // Landing: place the next step shortly after touchdown instead of a full stride away.
            if (grounded && !_wasGrounded)
                _distanceSinceStep = _strideLength * 0.6f;
            _wasGrounded = grounded;

            if (!grounded || planarSpeed < _minSpeed)
                return;

            _distanceSinceStep += planarSpeed * Time.deltaTime;
            if (_distanceSinceStep < _strideLength)
                return;

            _distanceSinceStep = 0f;
            if (AudioManager.Instance)
                AudioManager.Instance.PlaySoundFX(_footstepSFX, transform, _volume,
                    Random.Range(_pitchRange.x, _pitchRange.y));
        }
    }
}
