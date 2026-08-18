using UnityEngine;

namespace SpookyGame.Interfaces
{
    public interface IFlashlightReactive
    {
        void OnFlashlightHit(Vector3 hitPoint, Vector3 shotDirection);
    }
}
