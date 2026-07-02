using UnityEngine;
using SummerSpooks.Input;
using SummerSpooks.Player.Data;

namespace SummerSpooks.Player
{
    /// <summary>
    /// Subscribes to the <see cref="InputReader"/> event channel and turns raw input
    /// into clean, frame-stable values other systems can read. Edge-triggered inputs
    /// (jump press/release, interact) are latched and cleared once per frame via
    /// <see cref="EndFrame"/>, called by the orchestrating PlayerController.
    /// </summary>
    public class PlayerInputInterpreter : MonoBehaviour
    {
        [SerializeField] private InputReader _inputReader;

        private InputBooleans _inputBuffers;
        private Vector2 _movementInput;
        private Vector2 _lookInput;

        // Other systems can read these values
        
        // --- Movement Related ---
        public Vector2 MovementInput => _movementInput;
        public Vector2 LookInput => _lookInput;
        public bool SprintHeld => _inputBuffers.Sprint;
        public bool CrouchHeld => _inputBuffers.Crouch;
        public bool JumpPressed => _inputBuffers.JumpPressed;
        public bool JumpReleased => _inputBuffers.JumpReleased;
        public bool MoveCanceled => _inputBuffers.MoveCanceled;
        
        // --- Interaction Related ---
        public bool InteractPressed => _inputBuffers.Interact;
        public bool NextPressed => _inputBuffers.Next;
        public bool PreviousPressed => _inputBuffers.Previous;
        public bool ItemPressed => _inputBuffers.Item;
        
        // --- Control Scheme ---
        public ControlDeviceType CurrentDevice => _inputReader.CurrentDevice;
        public bool IsKeyboardMouse => _inputReader.CurrentDevice == ControlDeviceType.KeyboardMouse;
        public bool IsGamepad => _inputReader.CurrentDevice == ControlDeviceType.Gamepad;
        

        public void Subscribe()
        {
            if (_inputReader == null)
            {
                Debug.LogError("[PlayerInputInterpreter] No InputReader assigned.", this);
                return;
            }

            _inputReader.Move += OnMove;
            _inputReader.MoveCancel += OnMoveCancel;
            _inputReader.Look += OnLook;
            _inputReader.Jump += OnJump;
            _inputReader.Sprint += OnSprint;
            _inputReader.Crouch += OnCrouch;
            _inputReader.Interact += OnInteract;
            _inputReader.Item += OnItem;
            _inputReader.Next += OnNext;
            _inputReader.Previous += OnPrevious;
            _inputReader.EnablePlayerActions();
        }

        public void Unsubscribe()
        {
            if (_inputReader == null) return;

            _inputReader.Move -= OnMove;
            _inputReader.MoveCancel -= OnMoveCancel;
            _inputReader.Look -= OnLook;
            _inputReader.Jump -= OnJump;
            _inputReader.Sprint -= OnSprint;
            _inputReader.Crouch -= OnCrouch;
            _inputReader.Interact -= OnInteract;
            _inputReader.Item -= OnItem;
            _inputReader.Next -= OnNext;
            _inputReader.Previous -= OnPrevious;
            _inputReader.DisablePlayerActions();
        }

        /// <summary>Clears single-frame input edges. Call once at the end of the owning update.</summary>
        public void EndFrame()
        {
            _inputBuffers.JumpPressed = false;
            _inputBuffers.JumpReleased = false;
            _inputBuffers.Interact = false;
            // Pointer delta is per-frame: zero it so the camera does not keep drifting
            // on frames where the mouse did not move.
            if(IsKeyboardMouse)
                _lookInput = Vector2.zero;
        }

        private void OnMove(Vector2 input)
        {
            _movementInput = input;
            _inputBuffers.MoveCanceled = false;
        }

        private void OnMoveCancel(bool isCanceled)
        {
            if (!isCanceled) return;
            _inputBuffers.MoveCanceled = true;
            _movementInput = Vector2.zero;
        }

        private void OnLook(Vector2 input)
        {
            _lookInput = input;
        }

        private void OnJump(bool pressed)
        {
            if (pressed) _inputBuffers.JumpPressed = true;
            else _inputBuffers.JumpReleased = true;
        }

        private void OnSprint(bool held) => _inputBuffers.Sprint = held;
        private void OnCrouch(bool held) => _inputBuffers.Crouch = held;

        private void OnInteract(bool pressed)
        {
            if (pressed) _inputBuffers.Interact = true;
        }
        
        private void OnItem(bool pressed)
        {
            if (pressed) _inputBuffers.Item = true;
        }
        
        private void OnNext(bool pressed)
        {
            if (pressed) _inputBuffers.Next = true;
        }
        
        private void OnPrevious(bool pressed)
        {
            if (pressed) _inputBuffers.Previous = true;
        }
    }
}
