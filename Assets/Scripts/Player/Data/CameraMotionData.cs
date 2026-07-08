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

        public bool Grounded;
        public bool Sprinting;
        public bool Crouching;
    }
}
