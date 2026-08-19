using System;
using SpookyGame.Core;
using UnityEngine;

namespace SpookyGame.Audio
{
    /// <summary>
    /// One authored sound: the clip plus the volume and pitch spread that belong to it.
    /// Grunts, squeals and screams are recorded at different levels and registers, so the
    /// mix values live next to each clip rather than being shared across a whole set.
    /// </summary>
    [Serializable]
    public class HurtSound
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 0.9f;
        [Tooltip("Pitch is randomized between these two values (x = min, y = max).")]
        public Vector2 PitchRange = new Vector2(0.92f, 1.08f);

        public bool IsValid => Clip != null;
        public float RandomPitch => UnityEngine.Random.Range(PitchRange.x, PitchRange.y);
    }

    /// <summary>
    /// Per-enemy hurt/death vocalizations, driven by <see cref="ActorHealth"/> events.
    /// Authored on each enemy prefab so every character gets its own clips and mix.
    /// Sounds play through the AudioManager's pool, so a death cry keeps playing even
    /// after the enemy GameObject is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class EnemyHurtSFX : MonoBehaviour
    {
        [Tooltip("One is picked at random per hit; each carries its own volume and pitch range.")]
        [SerializeField] private HurtSound[] _hurtSounds;
        [SerializeField] private HurtSound _deathSound = new HurtSound();
        [Tooltip("Ignore hits landing closer together than this, so burst damage doesn't stack cries.")]
        [SerializeField, Min(0f)] private float _minimumInterval = 0.1f;

        private ActorHealth _health;
        private float _nextHurtTime;
        private int _lastIndex = -1;

        private void Awake()
        {
            _health = GetComponent<ActorHealth>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null)
                return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void OnDamaged(int amount)
        {
            // The death cry covers the killing blow.
            if (_health.IsDead || Time.time < _nextHurtTime)
                return;

            HurtSound sound = ChooseHurtSound();
            if (sound == null)
                return;

            _nextHurtTime = Time.time + _minimumInterval;
            Play(sound);
        }

        private void OnDied() => Play(_deathSound);

        private void Play(HurtSound sound)
        {
            if (sound == null || !sound.IsValid || AudioManager.Instance == null)
                return;

            AudioManager.Instance.PlaySoundFX(sound.Clip, transform, sound.Volume, sound.RandomPitch);
        }

        private HurtSound ChooseHurtSound()
        {
            if (_hurtSounds == null || _hurtSounds.Length == 0)
                return null;
            if (_hurtSounds.Length == 1)
                return _hurtSounds[0];

            // Avoid repeating the clip we just played.
            int index = UnityEngine.Random.Range(0, _hurtSounds.Length);
            if (index == _lastIndex)
                index = (index + 1) % _hurtSounds.Length;
            _lastIndex = index;
            return _hurtSounds[index];
        }
    }
}