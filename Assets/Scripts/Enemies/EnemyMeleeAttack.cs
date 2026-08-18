using SpookyGame.Core;
using SpookyGame.UI;
using UnityEngine;

namespace SpookyGame.Enemies
{
    public enum EnemyAttackMode
    {
        Proximity,
        PhysicalContact
    }

    /// <summary>
    /// Enemy damage synchronized to an optional animation windup. Villains attack
    /// within a configured radius; contact enemies only attack while colliders touch.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class EnemyMeleeAttack : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _damage = 10;
        [SerializeField] private EnemyAttackMode _attackMode = EnemyAttackMode.Proximity;
        [SerializeField, Min(0.25f)] private float _attackRange = 1.7f;
        [SerializeField, Min(0.1f)] private float _attackCooldown = 1.1f;
        [SerializeField, Min(0f)] private float _damageWindup = 0.2f;
        [SerializeField] private string _attackAnimationTrigger;
        [SerializeField] private bool _showPossumScratchOnHit;
        [SerializeField] private Sprite[] _screenHitFrames;
        [SerializeField, Min(0.01f)] private float _screenHitSecondsPerFrame = 0.08f;
        [SerializeField, Min(0f)] private float _screenHitHoldSeconds = 0.16f;
        [SerializeField] private LayerMask _lineOfSightLayers = ~0;

        private ActorHealth _ownHealth;
        private ActorHealth _playerHealth;
        private Transform _player;
        private Animator _animator;
        private PossumScratchScreenEffect _possumScratchEffect;
        private Collider[] _ownColliders;
        private Collider[] _playerColliders;
        private float _nextAttackTime;
        private float _damageAt;
        private bool _attackPending;

        private void Awake()
        {
            _ownHealth = GetComponent<ActorHealth>();
            _animator = GetComponentInChildren<Animator>(true);
            _ownColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                return;
            _player = playerObject.transform;
            _playerHealth = playerObject.GetComponent<ActorHealth>();
            _playerColliders = playerObject.GetComponentsInChildren<Collider>(true);
            _possumScratchEffect = FindFirstObjectByType<PossumScratchScreenEffect>();
        }

        private void Update()
        {
            if (_ownHealth.IsDead || _playerHealth == null || _playerHealth.IsDead)
            {
                _attackPending = false;
                return;
            }

            if (_attackPending && Time.time >= _damageAt)
            {
                _attackPending = false;
                if (CanAttackNow())
                    ApplyDamage();
            }

            if (_attackPending || Time.time < _nextAttackTime || !CanAttackNow())
                return;

            BeginAttack();
        }

        private void BeginAttack()
        {
            _nextAttackTime = Time.time + _attackCooldown;
            if (_animator != null && !string.IsNullOrWhiteSpace(_attackAnimationTrigger) &&
                HasAnimatorParameter(_attackAnimationTrigger, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(_attackAnimationTrigger);
                _animator.SetTrigger(_attackAnimationTrigger);
            }

            if (_damageWindup <= 0f)
            {
                ApplyDamage();
                return;
            }

            _attackPending = true;
            _damageAt = Time.time + _damageWindup;
        }

        private void ApplyDamage()
        {
            if (!_playerHealth.TakeDamage(_damage))
                return;

            if (_showPossumScratchOnHit)
                _possumScratchEffect?.PlayScratch();
            else if (_screenHitFrames != null && _screenHitFrames.Length > 0)
                _possumScratchEffect?.Play(
                    _screenHitFrames, _screenHitSecondsPerFrame, _screenHitHoldSeconds);
        }

        private bool CanAttackNow()
        {
            if (_attackMode == EnemyAttackMode.PhysicalContact)
                return AreCollidersTouching();

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 target = _player.position + Vector3.up * 0.8f;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude > _attackRange * _attackRange)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude + 0.15f,
                _lineOfSightLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                ActorHealth hitHealth = hit.collider.GetComponentInParent<ActorHealth>();
                return hitHealth == _playerHealth;
            }

            return true;
        }

        private bool AreCollidersTouching()
        {
            if (_ownColliders == null || _playerColliders == null)
                return false;

            foreach (Collider own in _ownColliders)
            foreach (Collider playerCollider in _playerColliders)
            {
                if (own != null && playerCollider != null && own.enabled && playerCollider.enabled &&
                    own.bounds.Intersects(playerCollider.bounds))
                    return true;
            }

            return false;
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
        {
            int hash = Animator.StringToHash(parameterName);
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == type)
                    return true;
            }
            return false;
        }
    }
}
