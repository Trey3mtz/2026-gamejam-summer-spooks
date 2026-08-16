using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Enemies
{
    /// <summary>
    /// Non-combat possum prototype. It wanders on the baked NavMesh, then
    /// permanently aggros and runs toward the player after a close approach or
    /// after remaining nearby but outside the player's unobstructed view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider), typeof(Rigidbody))]
    public sealed class PossumMob : MonoBehaviour
    {
        [Header("Player Awareness")]
        [Tooltip("Immediate proximity aggro radius, even if a wall is between the possum and player.")]
        [SerializeField, Min(0f)] private float _closeAggroRadius = 15f;
        [Tooltip("Within this distance, staying outside the player's unobstructed view builds unnoticed time.")]
        [SerializeField, Min(0f)] private float _unnoticedAwarenessRadius = 18f;
        [SerializeField, Min(0f)] private float _unnoticedSecondsToAggro = 4.5f;
        [SerializeField] private LayerMask _visibilityBlockers;

        [Header("Wandering")]
        [SerializeField, Min(0f)] private float _wanderSpeed = 10f;
        [SerializeField, Min(0.5f)] private float _wanderRadius = 30f;
        [SerializeField] private Vector2 _idleSecondsRange = new Vector2(0.15f, 0.5f);
        [SerializeField, Range(1, 30)] private int _wanderDestinationAttempts = 24;
        [SerializeField, Min(0.1f)] private float _navMeshSampleRadius = 1.5f;
        [SerializeField, Min(0.05f)] private float _zigZagInterval = 0.45f;
        [SerializeField, Min(0f)] private float _zigZagWidth = 2f;
        [SerializeField, Min(0.5f)] private float _zigZagForwardStep = 3.5f;

        [Header("Aggro Chase")]
        [SerializeField, Min(0f)] private float _aggroSpeed = 15f;
        [SerializeField, Min(0f)] private float _chaseStoppingDistance = 1.1f;
        [SerializeField, Min(0.02f)] private float _repathInterval = 0.2f;

        private NavMeshAgent _agent;
        private Transform _player;
        private Camera _playerCamera;
        private SpriteRenderer _spriteRenderer;
        private float _waitUntil;
        private float _nextRepathTime;
        private float _unnoticedTime;
        private float _nextZigZagTime;
        private Vector3 _wanderGoal;
        private bool _initialized;
        private bool _isAggro;
        private bool _hasWanderGoal;
        private bool _zigZagRight;
        private string _aggroReason = "None";
        private int _wanderDestinationCount;

        public bool IsInitialized => _initialized;
        public bool IsAggro => _isAggro;
        public string AggroReason => _aggroReason;
        public bool IsOnNavMesh => _agent != null && _agent.isOnNavMesh;
        public bool HasPath => IsOnNavMesh && _agent.hasPath;
        public float CurrentSpeed => _agent == null ? 0f : _agent.speed;
        public int WanderDestinationCount => _wanderDestinationCount;
        public float UnnoticedTime => _unnoticedTime;
        public float DistanceToPlayer => _player == null
            ? float.PositiveInfinity
            : PlanarDistance(transform.position, _player.position);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (_visibilityBlockers == 0)
                _visibilityBlockers = LayerMask.GetMask("GroundWall");

            ConfigurePhysics();
            ConfigureAgent();
        }

        private IEnumerator Start()
        {
            // Let the scene's baked NavMesh, player, and camera register first.
            yield return null;

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogError("[PossumMob] No GameObject tagged Player was found.", this);
                enabled = false;
                yield break;
            }

            _player = playerObject.transform;
            _playerCamera = Camera.main;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit spawnHit,
                    _navMeshSampleRadius, NavMesh.AllAreas) || !_agent.Warp(spawnHit.position))
            {
                Debug.LogError("[PossumMob] Spawn point is not on the baked NavMesh.", this);
                enabled = false;
                yield break;
            }

            _initialized = true;
            BeginIdle();
        }

        private void Update()
        {
            if (!_initialized || _player == null || !_agent.isOnNavMesh)
                return;

            UpdateAwareness();

            if (_isAggro)
                UpdateChase();
            else
                UpdateWander();
        }

        private void ConfigurePhysics()
        {
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var capsule = GetComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.32f;
            capsule.height = 0.7f;
            capsule.center = new Vector3(0f, 0.35f, 0f);
            capsule.isTrigger = false;
        }

        private void ConfigureAgent()
        {
            _agent.radius = 0.28f;
            _agent.height = 0.7f;
            _agent.baseOffset = 0f;
            _agent.speed = _wanderSpeed;
            _agent.acceleration = 14f;
            _agent.angularSpeed = 0f;
            _agent.stoppingDistance = 0.3f;
            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.updateRotation = false;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        private void UpdateAwareness()
        {
            float distance = DistanceToPlayer;
            if (distance <= _closeAggroRadius)
            {
                BeginAggro("CloseRadius");
                return;
            }

            if (distance > _unnoticedAwarenessRadius)
            {
                _unnoticedTime = 0f;
                return;
            }

            if (IsVisibleToPlayer())
            {
                _unnoticedTime = 0f;
                return;
            }

            _unnoticedTime += Time.deltaTime;
            if (_unnoticedTime >= _unnoticedSecondsToAggro)
                BeginAggro("UnnoticedNearby");
        }

        private bool IsVisibleToPlayer()
        {
            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_playerCamera == null)
                return false;

            Vector3 target = _spriteRenderer != null
                ? _spriteRenderer.bounds.center
                : transform.position + Vector3.up * 0.35f;
            Vector3 viewport = _playerCamera.WorldToViewportPoint(target);
            bool insideView = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f &&
                              viewport.y > 0f && viewport.y < 1f;
            if (!insideView)
                return false;

            Vector3 origin = _playerCamera.transform.position;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
                return true;

            if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance,
                    _visibilityBlockers, QueryTriggerInteraction.Ignore))
                return true;

            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        private void UpdateWander()
        {
            if (_hasWanderGoal)
            {
                if (PlanarDistance(transform.position, _wanderGoal) <= 1.25f)
                {
                    _agent.ResetPath();
                    BeginIdle();
                    return;
                }

                if (!_agent.pathPending && (Time.time >= _nextZigZagTime || !_agent.hasPath))
                {
                    if (!SetNextZigZagWaypoint())
                        BeginIdle();
                }

                return;
            }

            if (Time.time < _waitUntil)
                return;

            if (TryChooseWanderDestination(out Vector3 destination))
            {
                _agent.speed = _wanderSpeed;
                _agent.stoppingDistance = 0.3f;
                _wanderGoal = destination;
                _hasWanderGoal = true;
                _nextZigZagTime = 0f;
                _zigZagRight = Random.value >= 0.5f;
                if (SetNextZigZagWaypoint())
                    _wanderDestinationCount++;
                else
                    BeginIdle();
            }
            else
            {
                BeginIdle();
            }
        }

        private bool SetNextZigZagWaypoint()
        {
            Vector3 towardGoal = _wanderGoal - transform.position;
            towardGoal.y = 0f;
            float remainingDistance = towardGoal.magnitude;
            if (remainingDistance <= 0.1f)
                return false;

            Vector3 forward = towardGoal / remainingDistance;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float forwardDistance = Mathf.Min(_zigZagForwardStep, remainingDistance);
            float widthAtGoal = Mathf.Clamp01(remainingDistance / _zigZagForwardStep) * _zigZagWidth;
            float lateralOffset = _zigZagRight ? widthAtGoal : -widthAtGoal;
            Vector3 candidate = transform.position + forward * forwardDistance + right * lateralOffset;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                    _navMeshSampleRadius, NavMesh.AllAreas))
                return _agent.SetDestination(_wanderGoal);

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, hit.position,
                    NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return _agent.SetDestination(_wanderGoal);

            _zigZagRight = !_zigZagRight;
            _nextZigZagTime = Time.time + _zigZagInterval;
            return _agent.SetDestination(hit.position);
        }

        private bool TryChooseWanderDestination(out Vector3 destination)
        {
            destination = default;
            for (int i = 0; i < _wanderDestinationAttempts; i++)
            {
                Vector2 circle = Random.insideUnitCircle * _wanderRadius;
                Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                        _navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                if (PlanarDistance(transform.position, hit.position) < 2f)
                    continue;

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(transform.position, hit.position,
                        NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;

                destination = hit.position;
                return true;
            }

            return false;
        }

        private void UpdateChase()
        {
            _agent.speed = _aggroSpeed;
            _agent.stoppingDistance = _chaseStoppingDistance;

            if (Time.time < _nextRepathTime || _agent.pathPending)
                return;

            _nextRepathTime = Time.time + _repathInterval;
            if (NavMesh.SamplePosition(_player.position, out NavMeshHit targetHit, 2.5f, NavMesh.AllAreas))
                _agent.SetDestination(targetHit.position);
        }

        private void BeginAggro(string reason)
        {
            if (_isAggro)
                return;

            _isAggro = true;
            _aggroReason = reason;
            _hasWanderGoal = false;
            _agent.ResetPath();
            _agent.speed = _aggroSpeed;
            _agent.stoppingDistance = _chaseStoppingDistance;
            _nextRepathTime = 0f;
        }

        private void BeginIdle()
        {
            _hasWanderGoal = false;
            float minimum = Mathf.Min(_idleSecondsRange.x, _idleSecondsRange.y);
            float maximum = Mathf.Max(_idleSecondsRange.x, _idleSecondsRange.y);
            _waitUntil = Time.time + Random.Range(minimum, maximum);
        }

        /// <summary>Used by automated scene validation; it does not add combat.</summary>
        public void DebugForceAggro()
        {
            if (_initialized)
                BeginAggro("Validation");
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, _closeAggroRadius);
            Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, _unnoticedAwarenessRadius);
        }
    }
}
