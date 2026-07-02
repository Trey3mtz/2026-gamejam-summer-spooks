using UnityEngine;
using System;

namespace SpookyGame.Core.ItemEffects
{
    [Serializable]
    public class HealEffect : IItemEffect 
    {
        public int HealAmount = 50;

        public void Execute(GameObject user, Vector3 targetPosition) 
        {
            // Since we are running a Listen Server topology, 
            // the Host will run this logic and update the user's networked health variable.
            if (user.TryGetComponent(out HealthComponent health)) 
            {
                health.Heal(HealAmount);
            }
        }
    }
}
