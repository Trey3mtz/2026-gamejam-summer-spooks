using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static InputSystem_Actions;

namespace SummerSpooks.Input
{
    /// <summary>
    /// Single source of truth for raw player input. Wraps the generated Input Actions
    /// asset and re-broadcasts each action as a plain C# event so gameplay scripts can
    /// subscribe without ever touching the Input System directly.
    ///
    /// Lives as a ScriptableObject so it can be shared as one input "channel" across systems.
    /// </summary>
    [CreateAssetMenu(menuName = "SummerSpooks/Input Reader", fileName = "InputReader")]
    public class InputReader : ScriptableObject, IPlayerActions, IInputReader
    {
        [Tooltip("The Input Actions asset to drive. Assign InputSystem_Actions. " +
                 "If left empty, the project-wide actions are used.")]
        public InputSystem_Actions Actions;


        // --- Public event channel: subscribe to these from anywhere ---
        public event UnityAction<Vector2> Move = delegate { };
        public event UnityAction<bool> MoveCancel = delegate { };
        public event UnityAction<Vector2> Look = delegate { };
        public event UnityAction<bool> Jump = delegate { };
        public event UnityAction<bool> Sprint = delegate { };
        public event UnityAction<bool> Crouch = delegate { };
        public event UnityAction<bool> Interact = delegate { };
        public event UnityAction<bool> Item = delegate { };
        public event UnityAction<bool> Next = delegate { };
        public event UnityAction<bool> Previous = delegate { };
  
        // Input Properties
        public Vector2 Direction => Actions.Player.Move.ReadValue<Vector2>();
        
        public event UnityAction<ControlDeviceType> OnDeviceChanged = delegate { };
        public ControlDeviceType CurrentDevice { get; private set; }
        
        private void OnEnable()
        {
            // Hook into the Unity 6 Input System global device change callback
            InputSystem.onDeviceChange += HandleDeviceChange;
            // Also listen for button presses to dynamically switch schemes when a user grabs a controller
            InputSystem.onActionChange += HandleActionChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            InputSystem.onActionChange -= HandleActionChange;
        }
        
        public void EnablePlayerActions() 
        {
            if (Actions == null || Actions.asset == null)
            {
                Actions = new InputSystem_Actions();
                Actions.Player.SetCallbacks(this);
            } 
            Actions.Enable();
        }

        public void DisablePlayerActions()
        {
            if(Actions == null || Actions.asset == null)
            {
                Actions = new InputSystem_Actions();
                Actions.Player.SetCallbacks(this);
            }
            Actions.Disable();
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    // Evaluate the current state of devices when hardware topology changes
                    EvaluateActiveDevice(device);
                    break;
            }
        }

        private void HandleActionChange(object obj, InputActionChange change)
        {
            // Pragmatic check: If an action was performed, check what device performed it.
            // This handles the user seamlessly switching from keyboard to a controller mid-game.
            if (change == InputActionChange.ActionStarted && obj is InputAction action)
            {
                if (action.activeControl != null)
                {
                    EvaluateActiveDevice(action.activeControl.device);
                }
            }
        }
        
        private void EvaluateActiveDevice(InputDevice device)
        {
            ControlDeviceType detectedType = CurrentDevice;

            if (device is Gamepad)
            {
                detectedType = ControlDeviceType.Gamepad;
            }
            else if (device is Keyboard || device is Mouse)
            {
                detectedType = ControlDeviceType.KeyboardMouse;
            }

            // Only trigger transformations/events if the data actually changed
            if (detectedType != CurrentDevice)
            {
                CurrentDevice = detectedType;
                OnDeviceChanged?.Invoke(CurrentDevice);
            
#if UNITY_EDITOR
                Debug.Log($"[InputDeviceObserver] Control scheme swapped to: {CurrentDevice}");
#endif
            }
        }
        
        
        /*
        PHASE	    DESCRIPTION
        ---------------------------------------------------------
        Disabled	The Action is disabled and can't receive input.
        Waiting	    The Action is enabled and is actively waiting for input.
        Started	    The Input System has received input that started an Interaction with the Action.
        Performed	An Interaction with the Action has been completed.
        Canceled	An Interaction with the Action has been canceled.
        */
        
        // --- Input System callbacks ---
        public void OnMove(InputAction.CallbackContext context)
        {
            Move.Invoke(context.ReadValue<Vector2>());
            if (context.canceled)
                MoveCancel.Invoke(true);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            Look.Invoke(context.ReadValue<Vector2>());
        }

        public void OnItem(InputAction.CallbackContext context)
        {
            if(context.started)
                Item.Invoke(true);
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started)
                Jump.Invoke(true);
            else if (context.canceled)
                Jump.Invoke(false);
        }

        public void OnPrevious(InputAction.CallbackContext context)
        {
            if(context.started)
                Previous.Invoke(true);
        }

        public void OnNext(InputAction.CallbackContext context)
        {
            if(context.started)
                Next.Invoke(true);
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started) Sprint.Invoke(true);
            else if (context.canceled) Sprint.Invoke(false);
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.started) Crouch.Invoke(true);
            else if (context.canceled) Crouch.Invoke(false);
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed) Interact.Invoke(true);
            else if (context.canceled) Interact.Invoke(false);
        }
    }

    // --- Interface for enabling/disabling input ---
    public interface IInputReader
    {
        void EnablePlayerActions();
        void DisablePlayerActions();
    }
    
    // --- Enum for device type --- 
    public enum ControlDeviceType
    {
        KeyboardMouse,
        Gamepad
    }
}
