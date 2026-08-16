using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Enemies
{
    /// <summary>
    /// Vaquero behaviour: begin at its saved scene position, then follow a valid
    /// NavMesh route until close enough to play VUp.
    /// Combat, damage, health, and repeated spawning deliberately live outside
    /// this prototype so those rules can be designed separately.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VaqueroStalker : MonoBehaviour
    {
        private static readonly int VUpTrigger = Animator.StringToHash("VUp");
        private static readonly int IsCloseParameter = Animator.StringToHash("IsClose");

        [Header("Player")]
        [Tooltip("Found automatically from the Player tag when left empty.")]
        [SerializeField] private Transform _player;

        [Header("Stalking")]
        [SerializeField, Min(0f)] private float _movementSpeed = 10f;
        [SerializeField, Min(0f)] private float _stoppingDistance = 5.5f;
        [SerializeField, Min(0.02f)] private float _repathInterval = 0.2f;

        [Header("Close Animation")]
        [Tooltip("Starts just before the agent reaches its stopping distance so the player can clearly see it.")]
        [SerializeField, Min(0f)] private float _closeAnimationDistance = 6.25f;
        [Tooltip("The player must move this far away before VUp stops looping. The gap prevents rapid state flicker.")]
        [SerializeField, Min(0f)] private float _closeAnimationExitDistance = 7.25f;

        [Header("Procedural Walk")]
        [Tooltip("The imported Vaquero has no walk sprites, so motion is conveyed with a subtle step bob and lean.")]
        [SerializeField, Min(0f)] private float _walkBobHeight = 0.08f;
        [SerializeField, Min(0f)] private float _walkStepsPerSecond = 2.5f;
        [SerializeField, Range(0f, 15f)] private float _walkLeanDegrees = 3.5f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Transform _visual;
        private Camera _playerCamera;
        private Vector3 _visualRestPosition;
        private Quaternion _visualRestRotation;
        private float _nextRepathTime;
        private float _walkPhase;
        private bool _initialized;
        private bool _isCloseAnimationActive;
        private int _closeAnimationPlayCount;
        private float _initialSpawnDistance = float.PositiveInfinity;

        public bool IsInitialized => _initialized;
        public bool IsWalking => _initialized && !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.01f;
        public bool HasPlayedCloseAnimation => _closeAnimationPlayCount > 0;
        public int CloseAnimationPlayCount => _closeAnimationPlayCount;
        public bool IsCloseAnimating => _isCloseAnimationActive;
        public float InitialSpawnDistance => _initialSpawnDistance;
        public float DistanceToPlayer => _player == null
            ? float.PositiveInfinity
            : PlanarDistance(transform.position, _player.position);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>(true);
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            _visual = _spriteRenderer != null ? _spriteRenderer.transform : transform;
            _visualRestPosition = _visual.localPosition;
            _visualRestRotation = _visual.localRotation;

            ConfigureAgent();
        }

        private void Start()
        {
            FindPlayerReferences();
            if (_player == null)
            {
                Debug.LogError("[VaqueroStalker] No GameObject tagged Player was found.", this);
                enabled = false;
                return;
            }

            if (!PlaceAtSavedPosition())
            {
                Debug.LogError("[VaqueroStalker] The saved position is not on the baked NavMesh.", this);
                enabled = false;
                return;
            }

            _initialSpawnDistance = DistanceToPlayer;
            _initialized = true;
            RequestPathToPlayer();
        }

        private void Update()
        {
            if (!_initialized || _player == null || !_agent.isOnNavMesh)
                return;

            float distance = DistanceToPlayer;
            bool shouldAnimateClose = _isCloseAnimationActive
                ? distance <= Mathf.Max(_closeAnimationDistance, _closeAnimationExitDistance)
                : distance <= _closeAnimationDistance;

            if (shouldAnimateClose != _isCloseAnimationActive)
            {
                if (shouldAnimateClose)
                    BeginCloseAnimation();
                else
                    EndCloseAnimation();
            }

            bool closeAnimationLocked = _isCloseAnimationActive;
            if (closeAnimationLocked || distance <= _stoppingDistance)
            {
                _agent.isStopped = true;
            }
            else
            {
                _agent.isStopped = false;
                // Do not restart a long campus path while Unity is still calculating it.
                if (Time.time >= _nextRepathTime && !_agent.pathPending)
                    RequestPathToPlayer();
            }

            UpdateWalkMotion(closeAnimationLocked);
        }

        private void LateUpdate()
        {
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            // Keep this 2D character readable inside the 3D first-person scene.
            if (_playerCamera != null)
            {
                Vector3 cameraForward = _playerCamera.transform.forward;
                cameraForward.y = 0f;
                if (cameraForward.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(cameraForward.normalized, Vector3.up);
            }
        }

        private void ConfigureAgent()
        {
            _agent.speed = _movementSpeed;
            _agent.acceleration = 10f;
            _agent.angularSpeed = 0f;
            _agent.stoppingDistance = _stoppingDistance;
            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.updateRotation = false;
        }

        private void FindPlayerReferences()
        {
            if (_player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                    _player = playerObject.transform;
            }

            _playerCamera = Camera.main;
        }

        private bool PlaceAtSavedPosition()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit currentHit,
                    2f, NavMesh.AllAreas))
            {
                transform.position = currentHit.position;
                return _agent.Warp(currentHit.position);
            }

            return false;
        }

        private void RequestPathToPlayer()
        {
            _nextRepathTime = Time.time + _repathInterval;

            if (NavMesh.SamplePosition(_player.position, out NavMeshHit targetHit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(targetHit.position);
        }

        private void BeginCloseAnimation()
        {
            _isCloseAnimationActive = true;
            _closeAnimationPlayCount++;
            _agent.isStopped = true;
            ResetWalkVisual();

            if (_animator != null)
            {
                _animator.SetBool(IsCloseParameter, true);
                _animator.ResetTrigger(VUpTrigger);
                _animator.SetTrigger(VUpTrigger);
            }
        }

        private void EndCloseAnimation()
        {
            _isCloseAnimationActive = false;
            if (_animator != null)
                _animator.SetBool(IsCloseParameter, false);
        }

        private void OnDisable()
        {
            if (_animator != null)
            {
                _animator.SetBool(IsCloseParameter, false);
                _animator.ResetTrigger(VUpTrigger);
            }
        }

        private void UpdateWalkMotion(bool closeAnimationLocked)
        {
            bool walking = !closeAnimationLocked && !_agent.isStopped &&
                           _agent.velocity.sqrMagnitude > 0.01f;

            if (!walking)
            {
                ResetWalkVisual();
                return;
            }

            _walkPhase += Time.deltaTime * _walkStepsPerSecond * Mathf.PI * 2f;
            float step = Mathf.Sin(_walkPhase);
            float bob = Mathf.Abs(step) * _walkBobHeight;
            _visual.localPosition = _visualRestPosition + Vector3.up * bob;
            _visual.localRotation = _visualRestRotation *
                                    Quaternion.Euler(0f, 0f, step * _walkLeanDegrees);

            if (_spriteRenderer != null && _playerCamera != null)
            {
                float horizontalMotion = Vector3.Dot(_agent.velocity, _playerCamera.transform.right);
                if (Mathf.Abs(horizontalMotion) > 0.05f)
                    _spriteRenderer.flipX = horizontalMotion < 0f;
            }
        }

        private void ResetWalkVisual()
        {
            _visual.localPosition = Vector3.Lerp(_visual.localPosition, _visualRestPosition,
                12f * Time.deltaTime);
            _visual.localRotation = Quaternion.Slerp(_visual.localRotation, _visualRestRotation,
                12f * Time.deltaTime);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.65f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _closeAnimationDistance);
            Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _stoppingDistance);
        }
    }
}
