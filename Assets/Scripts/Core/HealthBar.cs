using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

namespace SpookyGame.Core
{
    [Serializable]
    public sealed class HealthBar 
    {
        private int _maximumHp;
            public int MaxHp => _maximumHp;
            
        private int _currentHp;
            public int CurrentHp => _currentHp;

        // Measured in seconds
        private readonly float _iFrames = 0.1f;
        private bool _isInvincible = false;

        // C# "Delegates" that ping out a signal when a specific event happens. This is pattern of programming is called The Observer Pattern.
        public Action<int, int> OnHealthChanged;
        public Action<int> OnDamaged;
        public Action OnHealed;
        public Action OnDeath;
 
        // Shortcut API
        public bool IsInvincible => _isInvincible;
        public bool IsDead => _currentHp <= 0;
        public float HealthPercentage => (float)_currentHp / _maximumHp;

        /// <summary>
        /// Initialize the health bar with a max health value.
        /// </summary>
        /// <param name="value"></param>
        public void InitHealthBar(int value)
        {
            _maximumHp = value;
            _currentHp = _maximumHp;
        }
        
        public void SetCurrentHealth(int value)
        {
            _currentHp = value;
        }

        // Pass in positive values to heal, and negative values to damage health
        public bool ChangeHealth(int hpDelta)
        {    
            // Taken Damage, Activate iFrames
            if (hpDelta < 0)
            {
                if (_isInvincible)
                    return false;

                _ = InvincibilityFrames();
            }
            
            // Callback for when we are gaining more health (needs to be prior to changing health)
            if(hpDelta > 0 && (_currentHp + hpDelta) <= _maximumHp)
                OnHealed?.Invoke();
            
            // Change our current health
            _currentHp = Mathf.Clamp(_currentHp + hpDelta, 0, _maximumHp);
            
            // Callback for damage FX and feedback (After changing health to make sure we aren't dead)
            if (hpDelta< 0 && _currentHp > 0)
                OnDamaged?.Invoke(hpDelta);

            // Health changing callback
            if(hpDelta != 0)
                OnHealthChanged?.Invoke(_currentHp, _maximumHp);

            // Callback for Death
            if (_currentHp <= 0)
                OnDeath?.Invoke();

            // Returns true if health was changed successfully
            return true;
        }

        
        public int IFrameCount()
        {
            return (int) (_iFrames * 1000);
        }

        private async UniTask InvincibilityFrames()
        {
            _isInvincible = true;        
            await UniTask.Delay((int)(_iFrames * 1000));
            _isInvincible = false;
        }

        public async UniTask MakeTemporarilyInvincible(float duration)
        {
            _isInvincible = true;
            while(duration > 0) 
            {
                duration -= Time.deltaTime;
                await UniTask.Yield();
            }
            _isInvincible = false;
        }

        public void Reset()
        {
            _currentHp = _maximumHp;
            OnHealthChanged?.Invoke(_currentHp, _maximumHp);
        }
    }
}