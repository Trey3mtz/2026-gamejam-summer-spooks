namespace SpookyGame.Player.Data
{
    /// <summary>
    /// Per-step snapshot of the locomotion state the camera systems care about.
    /// Written by PlayerController after each simulation step; read by CameraRig.
    /// Keeps the camera layer decoupled from the physics/state internals.
    /// </summary>
    public struct CameraMotionData
    {
        /// <summary>Horizontal speed in m/s (magnitude of XZ velocity).</summary>
        public float PlanarSpeed;

        /// <summary>Signed vertical velocity in m/s. Negative while falling.</summary>
        public float VerticalVelocity;

        /// <summary>Signed sideways speed in m/s, in the player's local frame. Positive = strafing right.</summary>
        public float LateralSpeed;

        public bool Grounded;
        public bool Sprinting;
        public bool Crouching;
    }
}
