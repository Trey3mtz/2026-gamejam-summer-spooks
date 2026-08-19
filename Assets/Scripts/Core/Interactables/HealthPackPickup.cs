using System.Collections;
using SpookyGame.Audio;
using SpookyGame.Player;
using TMPro;
using UnityEngine;

namespace SpookyGame.Core.Interactables
{
    /// <summary>An authored medical pickup that restores player health on contact.</summary>
    [DisallowMultipleComponent]
    public sealed class HealthPackPickup : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _healAmount = 35;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _labelRoot;
        [SerializeField, Min(0f)] private float _bobHeight = 0.18f;
        [SerializeField, Min(0f)] private float _bobSpeed = 2f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 26f;
        [SerializeField] private AudioClip _pickupSound;
        [SerializeField, Range(0f, 1f)] private float _pickupVolume = 0.72f;

        private SphereCollider _trigger;
        private Camera _camera;
        private Vector3 _visualBasePosition;
        private bool _collected;

        public int HealAmount => _healAmount;

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            if (_visualRoot != null)
                _visualBasePosition = _visualRoot.localPosition;
        }

        private void Update()
        {
            if (_collected || _visualRoot == null)
                return;

            _visualRoot.localPosition = _visualBasePosition +
                                        Vector3.up * (Mathf.Sin(Time.time * _bobSpeed) * _bobHeight);
            _visualRoot.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.Self);

            if (_labelRoot == null)
                return;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            Vector3 awayFromCamera = _labelRoot.position - _camera.transform.position;
            if (awayFromCamera.sqrMagnitude > 0.001f)
                _labelRoot.rotation = Quaternion.LookRotation(awayFromCamera.normalized, Vector3.up);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected)
                return;

            Player.Player player = other.GetComponentInParent<Player.Player>();
            if (player == null || !player.Health.Heal(_healAmount))
                return;

            _collected = true;
            if (_pickupSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySoundFX(_pickupSound, transform, _pickupVolume, 1f);
            StartCoroutine(CollectRoutine());
        }

        private IEnumerator CollectRoutine()
        {
            if (_trigger != null)
                _trigger.enabled = false;

            if (_visualRoot == null)
            {
                Destroy(gameObject);
                yield break;
            }

            const float duration = 0.28f;
            float elapsed = 0f;
            Vector3 startScale = _visualRoot.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _visualRoot.localScale = Vector3.Lerp(startScale, startScale * 1.65f, t);
                _visualRoot.localPosition += Vector3.up * (Time.deltaTime * 1.8f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
