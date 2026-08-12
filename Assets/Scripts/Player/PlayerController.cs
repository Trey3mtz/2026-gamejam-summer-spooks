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

        [Header("Simulation")]
        [Tooltip("Fixed simulation rate in Hz. Locomotion integrates at exactly this rate regardless of frame rate.")]
        [SerializeField] private float _simulationHz = 60f;
        [Tooltip("Max sim steps per rendered frame. Prevents a spiral of death after a hitch; excess time is dropped (game slows briefly).")]
        [SerializeField] private int _maxStepsPerFrame = 5;
        
        private float _accumulator;
        private float _fixedDt;
        private Vector3 _prevSimPosition;
        private Vector3 _currSimPosition;
        
        // Edge latches: survive rendered frames in which zero sim steps run, consumed exactly once by the first step that sees them.
        private bool _pendJumpPressed;
        private bool _pendJumpReleased;
        private bool _pendMoveCanceled;

        private PlayerInputInterpreter _input;
        private CapsuleCollider _capsule;
        private CharacterStatePayload _state;
        private CameraMotionData _camMotionData;
        private int _tick;

        // ================================================================
        //  Unity Setup 
        // ================================================================

        private void Awake()
        {
            _fixedDt = 1f / _simulationHz;
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

            _prevSimPosition = _currSimPosition = transform.position;

            if (_movementProfile == null)
                Debug.LogError("[PlayerController] No MovementProfile assigned.", this);
        }

        private void OnEnable() => _input.Subscribe();
        private void OnDisable() => _input.Unsubscribe();


        // ================================================================
        //  Update Callbacks 
        // ================================================================

        // 1st FixedUpdate() would be here
        
        // 2nd
        private void Update()
        {
            if (GameManager.Instance.IsPaused)
                return;
            
            // Look runs at render rate so smoothing stays fluid and per-frame mouse deltas are consumed exactly once per frame.
            if (_look)
            {
                _look.Tick(_input.LookInput, _input.CurrentDevice, Time.deltaTime);
                if (_cameraRig)
                    _cameraRig.SetLookLag(_look.LookLag.x);
            }

            // Latch input edges so a press on a zero-step frame is not lost, and a press seen by one step is not re-fired by the next.
            _pendJumpPressed  |= _input.JumpPressed;
            _pendJumpReleased |= _input.JumpReleased;
            _pendMoveCanceled |= _input.MoveCanceled;

            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, _fixedDt * _maxStepsPerFrame);
        
            while (_accumulator >= _fixedDt)
            {
                UpdateLocomotion(_fixedDt);
                _accumulator -= _fixedDt;
            }
            
            // Render between the two most recent sim states. alpha in [0,1).
            float alpha = _accumulator / _fixedDt; 
            transform.position = Vector3.Lerp(_prevSimPosition, _currSimPosition, alpha);

            //UpdateLocomotion(Time.deltaTime); 
            
            // Check for non-locomotion inputs.
            HandleInteractInput();
            HandleItemInput();
        }

        // 3rd
        private void LateUpdate()
        {            
            HandleCameraRig();
            
            // Consume single-frame input edges.
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
                JumpPressed = _pendJumpPressed,
                JumpReleased = _pendJumpReleased,
                MoveCanceled = _pendMoveCanceled,
                Sprint = _input.SprintHeld,
                Crouch = _input.CrouchHeld,
                DeltaTime = dt,
                Tick = _tick++
            };
            
            // Edges are consumed by this step. If two steps run this frame, the second must not see the same press again.
            _pendJumpPressed = _pendJumpReleased = _pendMoveCanceled = false;
            _prevSimPosition = _currSimPosition;
            
            // Simulate the next state, Sim state is authoritative. 
            _state.Position = _currSimPosition;
            _state = PlayerPhysics3D.Simulate(_state, cmd, _movementProfile, _capsule, _groundMask);
            _currSimPosition = _state.Position;

            // Feed the camera systems a snapshot of the fresh state.
            if (_cameraRig)
            {
                Vector3 v = _state.Velocity;
                _camMotionData = new CameraMotionData
                {
                    PlanarSpeed = new Vector2(v.x, v.z).magnitude,
                    LateralSpeed = Vector3.Dot(v, transform.right),   // signed, local X
                    VerticalVelocity = v.y,
                    Grounded = _state.Grounded,
                    Sprinting = _input.SprintHeld,
                    Crouching = _input.CrouchHeld
                };
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

            if (_input.HolsterPressed)
                _player.ToggleHolster();
        
            // Cycling implies intent to use — switching also draws.
            if (_input.NextPressed)     { _player.SetHolstered(false); inv.SelectNextItem(); }
            if (_input.PreviousPressed) { _player.SetHolstered(false); inv.SelectPreviousItem(); }

            if (_input.ItemPressed)
            {
                if (_player.IsHolstered) _player.SetHolstered(false); // draw; consume the press
                else                     inv.TryUseItem(gameObject);
            }
        }

        private void HandleCameraRig() { _cameraRig.SetMotionData(_camMotionData); }

        /// <summary>Moves the player instantly, without interpolation smearing across the jump.</summary>
        public void Teleport(Vector3 position)
        {
            _state.Position = position;
            _prevSimPosition = _currSimPosition = position;
            transform.position = position;
        }
    }
}
