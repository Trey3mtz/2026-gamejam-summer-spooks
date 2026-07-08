using SpookyGame.Core;
using UnityEngine;
using SpookyGame.Player.Data;
using SpookyGame.Player.Configuration;
using SpookyGame.Player.CameraFX;
using SpookyGame.Utilities;

namespace SpookyGame.Player
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
        [Tooltip("Owning Player. Found on this GameObject if left empty.")]
        [SerializeField] private Player _player;
        [Tooltip("Procedural camera motion (bob, impacts). Optional; skipped if unassigned.")]
        [SerializeField] private CameraRig _cameraRig;

        private PlayerInputInterpreter _input;
        private CapsuleCollider _capsule;
        private CharacterStatePayload _state;
        private int _tick;

        // ================================================================
        //  Unity Setup 
        // ================================================================

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
            
            if (_player == null)
                _player = GetComponent<Player>(); 

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


        // ================================================================
        //  Update Callbacks 
        // ================================================================
        
        // 1st
        private void FixedUpdate()
        {
            if (GameManager.Instance.IsPaused)
                return;

            float dt = Time.deltaTime;

            // First, update our movement and camera.
            UpdateLocomotion(dt); 
        }
        
        // 2nd
        private void Update()
        {
            if (GameManager.Instance.IsPaused)
                return;
            
            // Look runs at render rate so smoothing stays fluid and per-frame
            // mouse deltas are consumed exactly once per frame.
            if (_look)
            {
                _look.Tick(_input.LookInput, _input.CurrentDevice, Time.deltaTime);
                if (_cameraRig)
                    _cameraRig.SetLookLag(_look.LookLag.x);
            }
            
            // Check for non-locomotion inputs.
            HandleInteractInput();
            HandleItemInput();
        }

        // 3rd
        private void LateUpdate()
        {            
            // Lastly, Consume single-frame input edges.
            _input.EndFrame();
        }


        // ================================================================
        //  Helper Methods 
        // ================================================================

        // Handle movement and camera rotation.
        private void UpdateLocomotion(float dt)
        {            
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

            // Feed the camera systems a snapshot of the fresh state.
            if (_cameraRig)
            {
                Vector3 v = _state.Velocity;
                _cameraRig.SetMotionData(new CameraMotionData
                {
                    PlanarSpeed = new Vector2(v.x, v.z).magnitude,
                    LateralSpeed = Vector3.Dot(v, transform.right),   // signed, local X
                    VerticalVelocity = v.y,
                    Grounded = _state.Grounded,
                    Sprinting = _input.SprintHeld,
                    Crouching = _input.CrouchHeld
                });
            }
        }

        private void HandleInteractInput()
        {
            if(_input.InteractPressed)
                _interactableSensor.TryInteract();
        }
        
        private void HandleItemInput()
        {
            if (!_player) return;
            var inv = _player.Inventory;
        
            if (_input.NextPressed)     inv.SelectNextItem();
            if (_input.PreviousPressed) inv.SelectPreviousItem();
            if (_input.ItemPressed)     inv.TryUseItem(gameObject);
        }
    }
}
