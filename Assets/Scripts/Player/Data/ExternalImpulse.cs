using UnityEngine;

namespace SummerSpooks.Player.Data
{
    /// <summary>
    /// A one-shot velocity change applied on top of the player's own motion
    /// (knockback, explosions, scripted shoves, etc.).
    /// </summary>
    [System.Serializable]
    public struct ExternalImpulse
    {
        public Vector3 Force;

        public ExternalImpulse(Vector3 force)
        {
            Force = force;
        }
    }
}
