using UnityEngine;

namespace SummerSpooks.Player.Data
{
    /// <summary>
    /// The full motion state of the player for a single simulation tick.
    /// PlayerPhysics3D consumes one of these, advances it, and returns the next one,
    /// so the controller can predict the next position before committing the move.
    /// </summary>
    [System.Serializable]
    public struct CharacterStatePayload
    {
        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>Scratch reference velocity used by SmoothDamp on the horizontal plane.</summary>
        public Vector3 HorizontalRef;

        public bool Grounded;
        public bool JustJumped;

        public float JumpBufferTimer;
        public float CoyoteTimer;
        public int AirJumpsUsed;

        /// <summary>Tick this state was last resolved on (mirrors the command tick).</summary>
        public int Tick;
    }
}
