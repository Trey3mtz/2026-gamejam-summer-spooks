using System;
using UnityEngine;


namespace SpookyGame.Core
{
    [Serializable]
    public class BatteryLight
    {
        [SerializeField] private float _secondsPerPoint = 2f;
        public int BatteryLife = 100;          // 0-100; public for Save/Load
        public bool IsOn { get; private set; }
        public event Action<bool> Toggled = delegate { };
    
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
            if (_onDuration >= _secondsPerPoint)
            {
                _onDuration = 0f;
                if (--BatteryLife <= 0) { BatteryLife = 0; SetOn(false); }
            }
        }
    
        private void SetOn(bool on)
        {
            if (IsOn == on) return;
            IsOn = on;
            _onDuration = 0f;
            Toggled(on);
        }
    }
}
