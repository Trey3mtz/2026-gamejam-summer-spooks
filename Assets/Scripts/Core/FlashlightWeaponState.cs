using System;
using UnityEngine;

namespace SpookyGame.Core
{
    /// <summary>Persistent magazine/reserve state owned by the player, not by the swappable held model.</summary>
    [Serializable]
    public sealed class FlashlightWeaponState
    {
        [SerializeField, Min(1)] private int _magazineSize = 6;
        [SerializeField, Min(0)] private int _roundsInMagazine = 6;
        [SerializeField, Min(0)] private int _reserveAmmo = 24;
        [SerializeField, Min(0)] private int _maximumReserveAmmo = 48;
        [SerializeField, Min(0.1f)] private float _reloadSeconds = 1.4f;

        private float _reloadStartedAt;
        private float _reloadEndsAt;

        public event Action StateChanged = delegate { };
        public event Action ReloadStarted = delegate { };
        public event Action ReloadCompleted = delegate { };
        public event Action DryFired = delegate { };
        public event Action PowerBlocked = delegate { };
        public event Action<bool> ShotResolved = delegate { };

        public int MagazineSize => _magazineSize;
        public int RoundsInMagazine => _roundsInMagazine;
        public int ReserveAmmo => _reserveAmmo;
        public int MaximumReserveAmmo => _maximumReserveAmmo;
        public float ReloadSeconds => _reloadSeconds;
        public bool IsReloading { get; private set; }
        public bool CanFire => !IsReloading && _roundsInMagazine > 0;
        public float ReloadProgress => !IsReloading
            ? 0f
            : Mathf.InverseLerp(_reloadStartedAt, _reloadEndsAt, Time.time);

        public bool TryConsumeRound()
        {
            if (!CanFire)
            {
                DryFired();
                return false;
            }

            _roundsInMagazine--;
            StateChanged();
            return true;
        }

        public bool TryBeginReload()
        {
            if (IsReloading || _roundsInMagazine >= _magazineSize || _reserveAmmo <= 0)
                return false;

            IsReloading = true;
            _reloadStartedAt = Time.time;
            _reloadEndsAt = Time.time + _reloadSeconds;
            ReloadStarted();
            StateChanged();
            return true;
        }

        public void Tick()
        {
            if (!IsReloading || Time.time < _reloadEndsAt)
                return;

            int needed = _magazineSize - _roundsInMagazine;
            int loaded = Mathf.Min(needed, _reserveAmmo);
            _roundsInMagazine += loaded;
            _reserveAmmo -= loaded;
            IsReloading = false;
            ReloadCompleted();
            StateChanged();
        }

        public int AddReserveAmmo(int amount)
        {
            if (amount <= 0)
                return 0;

            int previous = _reserveAmmo;
            _reserveAmmo = Mathf.Clamp(_reserveAmmo + amount, 0, _maximumReserveAmmo);
            int added = _reserveAmmo - previous;
            if (added > 0)
                StateChanged();
            return added;
        }

        public void SetAmmo(int magazine, int reserve)
        {
            _roundsInMagazine = Mathf.Clamp(magazine, 0, _magazineSize);
            _reserveAmmo = Mathf.Clamp(reserve, 0, _maximumReserveAmmo);
            IsReloading = false;
            StateChanged();
        }

        public void CancelReload()
        {
            if (!IsReloading)
                return;

            IsReloading = false;
            StateChanged();
        }

        public void ReportShot(bool hitDamageable) => ShotResolved(hitDamageable);
        public void ReportPowerBlocked() => PowerBlocked();
    }
}
