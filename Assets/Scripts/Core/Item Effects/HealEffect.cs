using UnityEngine;
using System;
using SpookyGame.Interfaces;

namespace SpookyGame.Core.Item_Effects
{
    [Serializable]
    public class HealEffect : IItemEffect 
    {
        public int HealAmount = 1;

        public void Execute(GameObject user, Vector3 targetPosition) 
        {
            if (user.TryGetComponent(out ActorHealth health))
            {
                health.Heal(HealAmount);
            }
        }
    }
}
