using SpookyGame.Core;
using SpookyGame.Core.Interactables;
using SpookyGame.Interfaces;
using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Enemies
{
    /// <summary>
    /// Keeps La Llorona on the Science/Planetarium second floor. She remains in a
    /// normal idle pose until the player enters her territory or shoots her, then
    /// pursues on the baked NavMesh. EnemyMeleeAttack owns the close scream/damage.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(ActorHealth))]
    public sealed class LloronaSecondFloorChase : MonoBehaviour, IFlashlightReactive
    {
        [Header("Encounter Gate")]
        [SerializeField] private SwingDoor _bossArenaDoor;

        [Header("Aggro")]
        [SerializeField, Min(1f)] private float _aggroDistance = 22f;
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.15f;

        [Header("Second-Floor Chase")]
        [SerializeField, Min(0f)] private float _movementSpeed = 11f;
        [SerializeField, Min(0.25f)] private float _stoppingDistance = 2.65f;
        [SerializeField, Min(0.5f)] private float _allowedVerticalDifference = 2.25f;
        [SerializeField, Min(0f)] private float _boundsInset = 0.45f;

        [Header("Movement Animation")]
        [SerializeField, Min(0f)] private float _walkBobHeight = 0.055f;
        [SerializeField, Min(0f)] private float _walkCyclesPerSecond = 3.2f;
        [SerializeField, Range(0f, 12f)] private float _walkLeanDegrees = 2.5f;

        [Header("Flashlight Response")]
        [SerializeField, Min(0f)] private float _flashlightStunSeconds = 0.35f;

        private NavMeshAgent _agent;
        private ActorHealth _health;
        private EnemyMeleeAttack _attack;
        private Transform _player;
        private Camera _playerCamera;
        private SpriteRenderer _sprite;
        private Transform _visual;
        private Vector3 _visualRestPosition;
        private Quaternion _visualRestRotation;
        private Vector3 _homePosition;
        private Bounds _secondFloorBounds;
        private float _secondFloorY;
        private float _nextRepathTime;
        private float _stunnedUntil;
        private float _walkPhase;
        private bool _initialized;
        private bool _aggroed;
        private bool _doorHasOpened;

        public bool IsAggroed => _aggroed;
        public bool DoorHasOpened => _doorHasOpened;
        public bool IsWalking => _initialized && _agent.isOnNavMesh &&
                                 !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.01f;
        public float MovementSpeed => _movementSpeed;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<ActorHealth>();
            _attack = GetComponent<EnemyMeleeAttack>();
            _sprite = GetComponentInChildren<SpriteRenderer>(true);
            _visual = _sprite != null ? _sprite.transform : transform;
            _visualRestPosition = _visual.localPosition;
            _visualRestRotation = _visual.localRotation;

            _agent.speed = _movementSpeed;
            _agent.acceleration = 36f;
            _agent.angularSpeed = 0f;
            _agent.stoppingDistance = _stoppingDistance;
            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.updateRotation = false;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // Damage and the scream animation must not start before the player
            // actually aggros this boss.
            if (_attack != null)
                _attack.enabled = false;
        }

        private void Start()
        {
            if (_bossArenaDoor == null)
            {
                Debug.LogError("[LloronaSecondFloorChase] Boss arena door is not assigned.", this);
                enabled = false;
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogError("[LloronaSecondFloorChase] No Player-tagged object was found.", this);
                enabled = false;
                return;
            }

            _player = playerObject.transform;
            _playerCamera = Camera.main;
            _homePosition = transform.position;
            _secondFloorY = _homePosition.y;
            _secondFloorBounds = FindSecondFloorBounds();

            if (!NavMesh.SamplePosition(_homePosition, out NavMeshHit homeHit, 3.5f, NavMesh.AllAreas) ||
                Mathf.Abs(homeHit.position.y - _secondFloorY) > _allowedVerticalDifference)
            {
                Debug.LogError("[LloronaSecondFloorChase] Boss spawn is not on the second-floor NavMesh.", this);
                enabled = false;
                return;
            }

            _homePosition = homeHit.position;
            _secondFloorY = homeHit.position.y;
            transform.position = homeHit.position;
            if (!_agent.Warp(homeHit.position))
            {
                Debug.LogError("[LloronaSecondFloorChase] Could not place La Llorona on the NavMesh.", this);
                enabled = false;
                return;
            }

            _agent.isStopped = true;
            _initialized = true;
            Debug.Log($"[LloronaSecondFloorChase] Ready at speed {_movementSpeed:0.##}; " +
                      $"second-floor bounds {_secondFloorBounds}.", this);
        }

        private void Update()
        {
            if (!_initialized || _player == null || !_agent.isOnNavMesh)
                return;

            if (_health.IsDead)
            {
                StopAgent();
                return;
            }

            EnforceSecondFloorBoundary();

            if (!ReleaseWhenBossDoorOpens())
            {
                StopAgent();
                UpdateWalkAnimation(false);
                return;
            }

            bool playerOnSecondFloor = IsInsideSecondFloor(_player.position);
            float distance = PlanarDistance(transform.position, _player.position);
            if (!_aggroed && playerOnSecondFloor && distance <= _aggroDistance)
                BeginAggro();

            if (!_aggroed || !playerOnSecondFloor || Time.time < _stunnedUntil)
            {
                StopAgent();
                UpdateWalkAnimation(false);
                return;
            }

            if (distance <= _stoppingDistance)
            {
                StopAgent();
            }
            else
            {
                _agent.isStopped = false;
                if (Time.time >= _nextRepathTime && !_agent.pathPending)
                    RequestPathToPlayer();
            }

            UpdateWalkAnimation(IsWalking);
        }

        private void LateUpdate()
        {
            if (_playerCamera == null)
                _playerCamera = Camera.main;
            if (_playerCamera == null)
                return;

            Vector3 cameraForward = _playerCamera.transform.forward;
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(cameraForward.normalized, Vector3.up);
        }

        private void BeginAggro()
        {
            if (_aggroed)
                return;

            _aggroed = true;
            if (_attack != null)
                _attack.enabled = true;
            _nextRepathTime = 0f;
            Debug.Log("[LloronaSecondFloorChase] Player entered the second-floor boss territory; chase active.", this);
        }

        private bool ReleaseWhenBossDoorOpens()
        {
            if (_doorHasOpened)
                return true;

            if (!_bossArenaDoor.IsOpen)
                return false;

            _doorHasOpened = true;
            Debug.Log("[LloronaSecondFloorChase] Boss arena door opened; La Llorona can now aggro.", this);
            return true;
        }

        private void RequestPathToPlayer()
        {
            _nextRepathTime = Time.time + _repathInterval;
            Vector3 target = ClampToSecondFloor(_player.position);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) &&
                IsInsideSecondFloor(hit.position))
                _agent.SetDestination(hit.position);
        }

        private void EnforceSecondFloorBoundary()
        {
            if (IsInsideSecondFloor(transform.position))
                return;

            _agent.ResetPath();
            _agent.Warp(_homePosition);
            transform.position = _homePosition;
        }

        private bool IsInsideSecondFloor(Vector3 point)
        {
            if (Mathf.Abs(point.y - _secondFloorY) > _allowedVerticalDifference)
                return false;

            return point.x >= _secondFloorBounds.min.x + _boundsInset &&
                   point.x <= _secondFloorBounds.max.x - _boundsInset &&
                   point.z >= _secondFloorBounds.min.z + _boundsInset &&
                   point.z <= _secondFloorBounds.max.z - _boundsInset;
        }

        private Vector3 ClampToSecondFloor(Vector3 point)
        {
            point.x = Mathf.Clamp(point.x, _secondFloorBounds.min.x + _boundsInset,
                _secondFloorBounds.max.x - _boundsInset);
            point.z = Mathf.Clamp(point.z, _secondFloorBounds.min.z + _boundsInset,
                _secondFloorBounds.max.z - _boundsInset);
            point.y = _secondFloorY;
            return point;
        }

        private Bounds FindSecondFloorBounds()
        {
            GameObject floorRoot = GameObject.Find("07_Second_Floor");
            if (floorRoot != null && TryCalculateBounds(floorRoot.transform, out Bounds bounds))
            {
                // Only X/Z define the territory. Y is locked to the boss NavMesh height.
                bounds.Expand(new Vector3(-1f, 0f, -1f));
                return bounds;
            }

            // Safe fallback covers the authored upper floor without ever allowing
            // La Llorona to leave the building.
            return new Bounds(_homePosition, new Vector3(52f, 5f, 32f));
        }

        private static bool TryCalculateBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private void StopAgent()
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        private void UpdateWalkAnimation(bool walking)
        {
            if (!walking)
            {
                _visual.localPosition = Vector3.Lerp(_visual.localPosition, _visualRestPosition,
                    12f * Time.deltaTime);
                _visual.localRotation = Quaternion.Slerp(_visual.localRotation, _visualRestRotation,
                    12f * Time.deltaTime);
                return;
            }

            _walkPhase += Time.deltaTime * _walkCyclesPerSecond * Mathf.PI * 2f;
            float wave = Mathf.Sin(_walkPhase);
            _visual.localPosition = _visualRestPosition + Vector3.up * (Mathf.Abs(wave) * _walkBobHeight);
            _visual.localRotation = _visualRestRotation * Quaternion.Euler(0f, 0f, wave * _walkLeanDegrees);
        }

        public void OnFlashlightHit(Vector3 hitPoint, Vector3 shotDirection)
        {
            if (!_initialized || _health.IsDead)
                return;

            if (!ReleaseWhenBossDoorOpens())
                return;

            BeginAggro();
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + _flashlightStunSeconds);
            StopAgent();
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.65f, 0.1f, 0.85f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, _aggroDistance);
            if (_secondFloorBounds.size.sqrMagnitude > 0f)
                Gizmos.DrawWireCube(_secondFloorBounds.center, _secondFloorBounds.size);
        }
    }
}
