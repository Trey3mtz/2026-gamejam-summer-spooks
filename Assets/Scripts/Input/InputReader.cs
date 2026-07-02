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

    public interface IInputReader
    {
        void EnablePlayerActions();
        void DisablePlayerActions();
    }
}
