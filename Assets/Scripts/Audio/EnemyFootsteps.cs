using SpookyGame.Core;
using SpookyGame.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Audio
{
    /// <summary>
    /// Plays positional, distance-based footsteps for a moving NavMesh enemy.
    /// The component is authored on each enemy prefab so cadence and clip set can
    /// match that character without coupling audio to its chase behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(ActorHealth))]
    public sealed class EnemyFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _clips;
        [SerializeField, Min(0.1f)] private float _strideLength = 1.6f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.35f;
        [SerializeField] private Vector2 _pitchRange = new Vector2(0.92f, 1.08f);
        [SerializeField, Min(0f)] private float _minimumSpeed = 0.15f;
        [SerializeField, Min(0.05f)] private float _minimumStepInterval = 0.22f;
        [SerializeField, Min(1f)] private float _audibleDistance = 28f;

        private NavMeshAgent _agent;
        private ActorHealth _health;
        private Transform _listener;
        private float _distanceSinceStep;
        private float _nextStepTime;
        private int _lastClipIndex = -1;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<ActorHealth>();

            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
                _listener = listener.transform;
        }

        private void Update()
        {
            if (_agent == null || _health == null || _health.IsDead ||
                _clips == null || _clips.Length == 0)
                return;

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsPaused)
                return;

            if (_listener != null &&
                (transform.position - _listener.position).sqrMagnitude > _audibleDistance * _audibleDistance)
            {
                _distanceSinceStep = 0f;
                return;
            }

            Vector3 velocity = _agent.velocity;
            float planarSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            if (!_agent.isOnNavMesh || _agent.isStopped || planarSpeed < _minimumSpeed)
                return;

            _distanceSinceStep += planarSpeed * Time.deltaTime;
            if (_distanceSinceStep < _strideLength || Time.time < _nextStepTime)
                return;

            _distanceSinceStep %= _strideLength;
            _nextStepTime = Time.time + _minimumStepInterval;
            AudioClip clip = ChooseClip();
            if (clip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySoundFX(clip, transform, _volume,
                    Random.Range(_pitchRange.x, _pitchRange.y));
            }
        }

        private AudioClip ChooseClip()
        {
            if (_clips.Length == 1)
                return _clips[0];

            int index = Random.Range(0, _clips.Length);
            if (index == _lastClipIndex)
                index = (index + 1) % _clips.Length;
            _lastClipIndex = index;
            return _clips[index];
        }
    }
}
