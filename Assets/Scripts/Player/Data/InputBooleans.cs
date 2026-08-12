namespace SpookyGame.Player.Data
{
    /// <summary>
    /// Buffered boolean input state held by the interpreter.
    /// Edge-triggered flags (JumpPressed/JumpReleased/Interact) are cleared every frame,
    /// while held flags (Sprint/Crouch) persist until the matching release.
    /// </summary>
    public struct InputBooleans
    {
        public bool MoveCanceled;
        public bool JumpPressed;
        public bool JumpReleased;
        public bool Sprint;
        public bool Crouch;
        public bool Interact;
        public bool Item;
		public bool Holster;
        public bool Next;
        public bool Previous;
        public bool Pause;


		public void EndFrameReset()
		{
            MoveCanceled = false;
            JumpPressed = false;
            JumpReleased = false;
            Interact = false;
            Item = false;
			Holster = false;
            Next = false;
            Previous = false;
            Pause = false;
		}
        
        public void FullReset()
        {
            MoveCanceled = false;
            JumpPressed = false;
            JumpReleased = false;
            Sprint = false;
            Crouch = false;
            Interact = false;
            Item = false;
			Holster = false;
            Next = false;
            Previous = false;
            Pause = false;
        }
    }
}
