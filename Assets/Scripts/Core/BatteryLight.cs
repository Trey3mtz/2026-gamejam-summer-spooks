using System;
using UnityEngine;


namespace SpookyGame.Core
{
    [Serializable]
    public class BatteryLight
    {
        [SerializeField, Min(0.01f)] private float _secondsPerPoint = 2f;
        public int BatteryLife = 100;          // 0-100; public for Save/Load
        public bool IsOn { get; private set; }
        public event Action<bool> Toggled = delegate { };
        public event Action<int> BatteryChanged = delegate { };
    
        private float _onDuration;
    
        public bool Toggle()
        {
            if (IsOn)                      SetOn(false);
            else if (BatteryLife > 0)      SetOn(true);
            return IsOn;
        }
    
        public void Tick(float dt)
        {
            if (!IsOn) return;
            _onDuration += dt;
            float secondsPerPoint = Mathf.Max(0.01f, _secondsPerPoint);
            while (_onDuration >= secondsPerPoint && IsOn)
            {
                _onDuration -= secondsPerPoint;
                TrySpend(1);
            }
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (BatteryLife < amount) return false;

            BatteryLife = Mathf.Max(0, BatteryLife - amount);
            BatteryChanged(BatteryLife);
            if (BatteryLife == 0)
                SetOn(false);
            return true;
        }

        public void SetBatteryLife(int value)
        {
            BatteryLife = Mathf.Clamp(value, 0, 100);
            BatteryChanged(BatteryLife);
            if (BatteryLife == 0)
                SetOn(false);
        }

        public int Recharge(int amount)
        {
            if (amount <= 0 || BatteryLife >= 100)
                return 0;

            int previous = BatteryLife;
            BatteryLife = Mathf.Clamp(BatteryLife + amount, 0, 100);
            BatteryChanged(BatteryLife);
            return BatteryLife - previous;
        }
    
        private void SetOn(bool on)
        {
            if (IsOn == on) return;
            IsOn = on;
            Toggled(on);
        }

        public void TurnOff() => SetOn(false);
    }
}
