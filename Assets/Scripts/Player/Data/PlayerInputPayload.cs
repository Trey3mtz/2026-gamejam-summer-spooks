using UnityEngine;

namespace SummerSpooks.Player.Data
{
    /// <summary>
    /// A snapshot of the player's intent for a single simulation tick.
    /// Built by PlayerController from the interpreted input and fed into PlayerPhysics3D.
    /// </summary>
    [System.Serializable]
    public struct PlayerInputPayload
    {
        /// <summary>Desired horizontal move direction in world space (already camera-relative, magnitude 0..1).</summary>
        public Vector3 WorldMove;

        public bool JumpPressed;
        public bool JumpReleased;
        public bool Sprint;
        public bool Crouch;
        public bool MoveCanceled;

        public float DeltaTime;
        public int Tick;
    }
}
