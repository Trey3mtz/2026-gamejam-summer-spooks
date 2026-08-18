using System.Collections;
using SpookyGame.Gameplay;
using UnityEngine;

namespace SpookyGame.Player
{
    /// <summary>
    /// Treats falling below the level as a death, then returns the player to the
    /// spawn location with clean movement state and restored health.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(Player))]
    public sealed class FallRespawn : MonoBehaviour
    {
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _killHeight = -18f;
        [SerializeField, Min(0f)] private float _respawnDelay = 0.35f;
        [SerializeField] private bool _restoreHealth = true;

        private PlayerController _controller;
        private Player _player;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private bool _isRespawning;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _player = GetComponent<Player>();

            Transform spawn = _spawnPoint != null ? _spawnPoint : transform;
            _spawnPosition = spawn.position;
            _spawnRotation = spawn.rotation;
        }

        private void OnEnable()
        {
            if (_player != null)
                _player.Health.Died += HandlePlayerDeath;
        }

        private void OnDisable()
        {
            if (_player != null)
                _player.Health.Died -= HandlePlayerDeath;
        }

        private void Update()
        {
            if (!_isRespawning && transform.position.y <= _killHeight)
                _player.Health.Kill();
        }

        private void HandlePlayerDeath()
        {
            if (GameRunDirector.Instance != null && GameRunDirector.Instance.HandlePlayerDeath())
                return;

            if (!_isRespawning)
                StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            _isRespawning = true;

            if (_controller != null)
                _controller.enabled = false;

            if (_respawnDelay > 0f)
                yield return new WaitForSecondsRealtime(_respawnDelay);

            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);

            if (_controller != null)
            {
                _controller.Teleport(_spawnPosition);
                _controller.enabled = true;
            }

            if (_restoreHealth && _player != null)
                _player.Health.ResetHealth();

            _isRespawning = false;
        }
    }
}
