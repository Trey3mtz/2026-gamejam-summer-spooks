using UnityEngine;
using System;
using SpookyGame.Interfaces;
using SpookyGame.Player;

namespace SpookyGame.Core.Item_Effects
{
    [Serializable]
    public class ToggleLightEffect : IItemEffect
    {
        public LightKind Kind;
    
        public void Execute(GameObject user, Vector3 targetPosition)
        {
            if (user.TryGetComponent(out Player.Player player))
                player.ToggleLight(Kind);
        }
    }
}
