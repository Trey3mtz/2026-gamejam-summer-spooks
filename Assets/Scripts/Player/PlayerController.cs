using SpookyGame.Core;
using UnityEngine;
using SummerSpooks.Player.Data;
using SummerSpooks.Player.Configuration;

namespace SummerSpooks.Player
{
    /// <summary>
    /// Orchestrates the player: reads the interpreted input, drives the camera look,
    /// asks <see cref="PlayerPhysics3D"/> to simulate the next state, and commits the
    /// predicted position. Holds no movement logic of its own.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerInputInterpreter))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private MovementProfile _movementProfile;
        [Tooltip("Layers treated as ground/walls. Auto-defaults to 'GroundWall' if left as Nothing.")]
        [SerializeField] private LayerMask _groundMask;
        [Tooltip("Optional. Found on this GameObject if left empty.")]
        [SerializeField] private PlayerLook _look;
        [SerializeField] private InteractableSensor _interactableSensor;

        private PlayerInputInterpreter _input;
        private CapsuleCollider _capsule;
        private CharacterStatePayload _state;
        private int _tick;

        private void Awake()
        {
            _input = GetComponent<PlayerInputInterpreter>();
            _capsule = GetComponent<CapsuleCollider>();
            if (_look == null)
                _look = GetComponent<PlayerLook>();

            if (_groundMask == 0)
            {
                int groundWall = LayerMask.GetMask("GroundWall");
                _groundMask = groundWall != 0 ? groundWall : LayerMask.GetMask("Default");
            }

            _state = new CharacterStatePayload
            {
                Position = transform.position,
                Velocity = Vector3.zero,
                Grounded = false
            };

            if (_movementProfile == null)
                Debug.LogError("[PlayerController] No MovementProfile assigned.", this);
        }

        private void OnEnable() => _input.Subscribe();
        private void OnDisable() => _input.Unsubscribe();

        private void Update()
        {
            if (!_movementProfile)
                return;

            float dt = Time.deltaTime;

            // First, update our movement and camera.
            UpdateLocomotion(dt);
            
            // Then, check for other non-movement inputs.
            HandleInteractInput();
            
            // Lastly, Consume single-frame input edges.
            _input.EndFrame();
        }

        // Handle movement and camera rotation.
        private void UpdateLocomotion(float dt)
        {
            // Look first so movement is relative to the freshly-rotated body.
            if (_look)
                _look.Tick(_input.LookInput, _input.CurrentDevice);
            
            Vector2 move = _input.MovementInput;
            Vector3 worldMove = transform.right * move.x + transform.forward * move.y;
            worldMove.y = 0f;
            if (worldMove.sqrMagnitude > 1f)
                worldMove.Normalize();

            var cmd = new PlayerInputPayload
            {
                WorldMove = worldMove,
                JumpPressed = _input.JumpPressed,
                JumpReleased = _input.JumpReleased,
                Sprint = _input.SprintHeld,
                Crouch = _input.CrouchHeld,
                MoveCanceled = _input.MoveCanceled,
                DeltaTime = dt,
                Tick = _tick++
            };

            // Simulate the next state, then commit the predicted position.
            _state.Position = transform.position; // resync in case something else moved us
            _state = PlayerPhysics3D.Simulate(_state, cmd, _movementProfile, _capsule, _groundMask);
            transform.position = _state.Position;
        }

        private void HandleInteractInput()
        {
            if(_input.InteractPressed)
                _interactableSensor.TryInteract();
        }

    }
}
