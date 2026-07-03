using System;
using SpookyGame.Interfaces;
using UnityEngine;

namespace SpookyGame.Core.Item_Effects
{
    [Serializable]
    public class SelfHurtEffect : IItemEffect
    {
        public int HurtAmount = -1;
        
        public void Execute(GameObject user, Vector3 targetPosition) 
        {
            if (user.TryGetComponent(out HealthBar health)) 
            {
                health.ChangeHealth(HurtAmount);
            }
        }
    }
}