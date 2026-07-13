using UnityEngine;
using System;

namespace SpookyGame.Interfaces
{
    public interface IItemEffect 
    {
        // Passed the player who used it, and where they used it
        void Execute(GameObject user, Vector3 targetPosition);
    }
}
