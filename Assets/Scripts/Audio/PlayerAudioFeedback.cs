using SpookyGame.Core;
using SpookyGame.Player;
using UnityEngine;

namespace SpookyGame.Audio
{
    /// <summary>
    /// First-person flashlight weapon feedback. The flashlight's physical toggle
    /// click remains owned by Player; this component only handles weapon-state events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player.Player))]
    public sealed class PlayerAudioFeedback : MonoBehaviour
    {
        [Header("Flashlight weapon")]
        [SerializeField] private AudioClip _shotSfx;
        [SerializeField] private AudioClip _reloadSfx;
        [SerializeField] private AudioClip _dryFireSfx;
        [SerializeField] private AudioClip _powerBlockedSfx;
        [SerializeField, Range(0f, 1f)] private float _shotVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _mechanismVolume = 0.65f;

        private Player.Player _player;
        private FlashlightWeaponState _weapon;
        private bool _subscribed;

        private void Awake()
        {
            _player = GetComponent<Player.Player>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || _player == null)
                return;

            _weapon = _player.FlashlightWeapon;
            if (_weapon == null)
                return;

            _weapon.ShotResolved += OnShotResolved;
            _weapon.ReloadStarted += OnReloadStarted;
            _weapon.DryFired += OnDryFired;
            _weapon.PowerBlocked += OnPowerBlocked;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _weapon == null)
                return;

            _weapon.ShotResolved -= OnShotResolved;
            _weapon.ReloadStarted -= OnReloadStarted;
            _weapon.DryFired -= OnDryFired;
            _weapon.PowerBlocked -= OnPowerBlocked;
            _subscribed = false;
        }

        private void OnShotResolved(bool hitDamageable)
        {
            Play(_shotSfx, _shotVolume, Random.Range(0.97f, 1.03f));
        }

        private void OnReloadStarted()
        {
            Play(_reloadSfx, _mechanismVolume, 1f);
        }

        private void OnDryFired()
        {
            Play(_dryFireSfx, _mechanismVolume, Random.Range(0.98f, 1.02f));
        }

        private void OnPowerBlocked()
        {
            Play(_powerBlockedSfx, _mechanismVolume, 1f);
        }

        private static void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayUISoundFX(clip, volume, pitch);
        }
    }
}
