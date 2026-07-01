using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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
    public class InputReader : ScriptableObject
    {
        [Tooltip("The Input Actions asset to drive. Assign InputSystem_Actions. " +
                 "If left empty, the project-wide actions are used.")]
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private string _playerMapName = "Player";

        // --- Public event channel: subscribe to these from anywhere ---
        public event UnityAction<Vector2> Move = delegate { };
        public event UnityAction<bool> MoveCancel = delegate { };
        public event UnityAction<Vector2> Look = delegate { };
        public event UnityAction<bool> Jump = delegate { };
        public event UnityAction<bool> Sprint = delegate { };
        public event UnityAction<bool> Crouch = delegate { };
        public event UnityAction<bool> Interact = delegate { };

        private Vector2 _direction;
        /// <summary>Last raw move vector reported by the device.</summary>
        public Vector2 Direction => _direction;

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _interactAction;
        private bool _wired;

        public void EnablePlayerActions()
        {
            EnsureWired();
            _playerMap?.Enable();
        }

        public void DisablePlayerActions()
        {
            _playerMap?.Disable();
        }

        private void EnsureWired()
        {
            if (_wired) return;

            if (_actions == null)
                _actions = UnityEngine.InputSystem.InputSystem.actions; // project-wide fallback

            if (_actions == null)
            {
                Debug.LogError("[InputReader] No InputActionAsset assigned and no project-wide actions found.");
                return;
            }

            _playerMap = _actions.FindActionMap(_playerMapName, throwIfNotFound: true);
            _moveAction = _playerMap.FindAction("Move", throwIfNotFound: true);
            _lookAction = _playerMap.FindAction("Look", throwIfNotFound: true);
            _jumpAction = _playerMap.FindAction("Jump", throwIfNotFound: true);
            _sprintAction = _playerMap.FindAction("Sprint", throwIfNotFound: false);
            _crouchAction = _playerMap.FindAction("Crouch", throwIfNotFound: false);
            _interactAction = _playerMap.FindAction("Interact", throwIfNotFound: false);

            _moveAction.started += OnMove;
            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;

            _lookAction.performed += OnLook;
            _lookAction.canceled += OnLook;

            _jumpAction.started += OnJump;
            _jumpAction.canceled += OnJump;

            if (_sprintAction != null)
            {
                _sprintAction.started += OnSprint;
                _sprintAction.canceled += OnSprint;
            }
            if (_crouchAction != null)
            {
                _crouchAction.started += OnCrouch;
                _crouchAction.canceled += OnCrouch;
            }
            if (_interactAction != null)
            {
                _interactAction.performed += OnInteract;
                _interactAction.canceled += OnInteract;
            }

            _wired = true;
        }

        // --- Input System callbacks ---
        public void OnMove(InputAction.CallbackContext context)
        {
            _direction = context.ReadValue<Vector2>();
            Move.Invoke(_direction);
            if (context.canceled)
                MoveCancel.Invoke(true);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            Look.Invoke(context.ReadValue<Vector2>());
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started)
                Jump.Invoke(true);
            else if (context.canceled)
                Jump.Invoke(false);
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
}
