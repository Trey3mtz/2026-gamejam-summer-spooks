using System;
using System.Collections;
using UnityEngine;

namespace SpookyGame.Core
{
    /// <summary>
    /// Shared hit-point model for the player and every damageable enemy.
    /// It owns damage rules and broadcasts state; HUD and AI remain presentation/behaviour layers.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ActorHealth : MonoBehaviour
    {
        [Header("Hit Points")]
        [SerializeField, Min(1)] private int _maximumHp = 100;
        [SerializeField, Min(0f)] private float _damageInvulnerabilitySeconds = 0.12f;

        [Header("Enemy Death")]
        [SerializeField] private bool _destroyOnDeath;
        [SerializeField, Min(0f)] private float _deathDuration = 0.55f;
        [SerializeField] private Color _deathFlashColor = new Color(0.35f, 1f, 0.95f, 1f);

        private int _currentHp;
        private float _invulnerableUntil;
        private bool _deathRaised;
        private Vector3 _startingScale;
        private SpriteRenderer[] _sprites;

        public event Action<int, int> HealthChanged = delegate { };
        public event Action<int> Damaged = delegate { };
        public event Action<int> Healed = delegate { };
        public event Action Died = delegate { };

        public int MaxHp => _maximumHp;
        public int CurrentHp => _currentHp;
        public bool IsDead => _currentHp <= 0;
        public bool IsInvincible => Time.time < _invulnerableUntil;
        public float HealthPercentage => _maximumHp <= 0 ? 0f : (float)_currentHp / _maximumHp;

        private void Awake()
        {
            _maximumHp = Mathf.Max(1, _maximumHp);
            _currentHp = _maximumHp;
            _startingScale = transform.localScale;
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvincible)
                return false;

            int previous = _currentHp;
            _currentHp = Mathf.Max(0, _currentHp - amount);
            int applied = previous - _currentHp;
            if (applied <= 0)
                return false;

            _invulnerableUntil = Time.time + _damageInvulnerabilitySeconds;
            Damaged(applied);
            HealthChanged(_currentHp, _maximumHp);

            if (_currentHp == 0 && !_deathRaised)
            {
                _deathRaised = true;
                Died();
                if (_destroyOnDeath)
                    StartCoroutine(EnemyDeathRoutine());
            }

            return true;
        }

        public bool Heal(int amount)
        {
            if (amount <= 0 || IsDead || _currentHp >= _maximumHp)
                return false;

            int previous = _currentHp;
            _currentHp = Mathf.Min(_maximumHp, _currentHp + amount);
            int applied = _currentHp - previous;
            Healed(applied);
            HealthChanged(_currentHp, _maximumHp);
            return true;
        }

        public bool ChangeHealth(int hpDelta)
            => hpDelta < 0 ? TakeDamage(-hpDelta) : Heal(hpDelta);

        public void SetMaximumHealth(int maximumHp, bool fillHealth = true)
        {
            _maximumHp = Mathf.Max(1, maximumHp);
            _currentHp = fillHealth ? _maximumHp : Mathf.Clamp(_currentHp, 0, _maximumHp);
            _deathRaised = _currentHp <= 0;
            HealthChanged(_currentHp, _maximumHp);
        }

        public void SetCurrentHealth(int value)
        {
            _currentHp = Mathf.Clamp(value, 0, _maximumHp);
            _deathRaised = _currentHp <= 0;
            HealthChanged(_currentHp, _maximumHp);
        }

        public void Kill()
        {
            if (!IsDead)
            {
                _invulnerableUntil = 0f;
                TakeDamage(_currentHp);
            }
        }

        public void ResetHealth()
        {
            StopAllCoroutines();
            _currentHp = _maximumHp;
            _invulnerableUntil = 0f;
            _deathRaised = false;
            transform.localScale = _startingScale;
            HealthChanged(_currentHp, _maximumHp);
        }

        public void Reset() => ResetHealth();

        private IEnumerator EnemyDeathRoutine()
        {
            foreach (Collider hitbox in GetComponentsInChildren<Collider>(true))
                hitbox.enabled = false;

            Color[] startingColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
            {
                startingColors[i] = _sprites[i].color;
                _sprites[i].color = _deathFlashColor;
            }

            float duration = Mathf.Max(0.05f, _deathDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(_startingScale, _startingScale * 0.72f, t);

                for (int i = 0; i < _sprites.Length; i++)
                {
                    Color color = Color.Lerp(_deathFlashColor, startingColors[i], t);
                    color.a = 1f - t;
                    _sprites[i].color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
