using System.Collections;
using SpookyGame.Core;
using SpookyGame.Interfaces;
using UnityEngine;

namespace SpookyGame.Player
{
    [DisallowMultipleComponent]
    public sealed class FlashlightShooter : MonoBehaviour
    {
        [Header("Shot")]
        [SerializeField, Min(1)] private int _batteryCost = 1;
        [SerializeField, Min(1)] private int _damage = 25;
        [SerializeField, Min(0.05f)] private float _cooldownSeconds = 0.5f;
        [SerializeField, Min(1f)] private float _range = 30f;
        [SerializeField] private LayerMask _hitLayers = ~0;

        [Header("Feedback")]
        [SerializeField] private Light _flashlight;
        [SerializeField, Min(1f)] private float _pulseIntensityMultiplier = 2.5f;
        [SerializeField, Min(0.01f)] private float _pulseSeconds = 0.09f;
        [SerializeField, Min(0.001f)] private float _beamWidth = 0.035f;
        [SerializeField] private Color _beamColor = new Color(1f, 0.035f, 0.02f, 0.98f);

        private Camera _aimCamera;
        private LineRenderer _beam;
        private Material _beamMaterial;
        private Coroutine _feedbackRoutine;
        private FlashlightFlicker _flicker;
        private BatteryLight _batterySource;
        private float _baseIntensity;
        private float _nextFireTime;

        public int BatteryCost => _batteryCost;
        public int Damage => _damage;
        public float CooldownSeconds => _cooldownSeconds;
        public float Range => _range;

        private void Awake()
        {
            if (_flashlight == null)
                _flashlight = GetComponentInChildren<Light>(true);

            if (_flashlight != null)
                _baseIntensity = _flashlight.intensity;

            // When present, the flicker owns the light's intensity - see ShowFeedback.
            _flicker = GetComponentInChildren<FlashlightFlicker>(true);

            CreateBeamRenderer();
        }

        private void OnEnable()
        {
            _aimCamera = Camera.main;
            if (_beam != null)
                _beam.enabled = false;
        }

        private void OnDisable()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }

            if (_beam != null)
                _beam.enabled = false;

            RestoreLight();
        }

        private void OnDestroy()
        {
            if (_beamMaterial != null)
                Destroy(_beamMaterial);
        }

        public bool TryFire(BatteryLight battery, FlashlightWeaponState weapon)
        {
            if (battery == null || weapon == null || Time.time < _nextFireTime)
                return false;

            if (_aimCamera == null)
                _aimCamera = Camera.main;

            if (_aimCamera == null)
                return false;

            if (!weapon.CanFire)
            {
                if (!weapon.IsReloading && weapon.RoundsInMagazine == 0 && weapon.ReserveAmmo > 0)
                {
                    weapon.TryBeginReload();
                    return false;
                }

                weapon.TryConsumeRound();
                return false;
            }

            if (!battery.TrySpend(_batteryCost))
            {
                weapon.ReportPowerBlocked();
                return false;
            }

            if (!weapon.TryConsumeRound())
            {
                battery.Recharge(_batteryCost);
                return false;
            }

            _nextFireTime = Time.time + _cooldownSeconds;
            _batterySource = battery;

            Transform cameraTransform = _aimCamera.transform;
            Vector3 rayOrigin = cameraTransform.position + cameraTransform.forward * 0.05f;
            Vector3 direction = cameraTransform.forward;
            Vector3 endPoint = rayOrigin + direction * _range;
            bool hitDamageable = false;

            if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, _range,
                    _hitLayers, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                hitDamageable = NotifyTarget(hit, direction);
            }

            Vector3 beamOrigin = _flashlight != null ? _flashlight.transform.position : rayOrigin;
            ShowFeedback(beamOrigin, endPoint);
            weapon.ReportShot(hitDamageable);
            return true;
        }

        private bool NotifyTarget(RaycastHit hit, Vector3 direction)
        {
            bool damaged = false;
            ActorHealth health = hit.collider.GetComponentInParent<ActorHealth>();
            if (health != null)
                damaged = health.TakeDamage(_damage);

            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IFlashlightReactive reactive)
                {
                    reactive.OnFlashlightHit(hit.point, direction);
                    break;
                }
            }

            return damaged;
        }

        private void ShowFeedback(Vector3 origin, Vector3 endPoint)
        {
            if (_feedbackRoutine != null)
                StopCoroutine(_feedbackRoutine);

            _feedbackRoutine = StartCoroutine(FeedbackRoutine(origin, endPoint));
        }

        private IEnumerator FeedbackRoutine(Vector3 origin, Vector3 endPoint)
        {
            if (_beam != null)
            {
                _beam.SetPosition(0, origin);
                _beam.SetPosition(1, endPoint);
                _beam.enabled = true;
            }

            if (_flicker != null)
            {
                // Compose with whatever the bulb is doing: a shot fired mid-failure
                // flares from the dim level rather than snapping back to full.
                _flicker.Pulse(_pulseIntensityMultiplier, _pulseSeconds);
            }
            else if (_flashlight != null)
            {
                _flashlight.enabled = true;
                _flashlight.intensity = _baseIntensity * _pulseIntensityMultiplier;
            }

            yield return new WaitForSeconds(_pulseSeconds);

            if (_beam != null)
                _beam.enabled = false;

            RestoreLight();
            _feedbackRoutine = null;
        }

        private void RestoreLight()
        {
            if (_flashlight == null || _flicker != null) return; // flicker restores its own state
            _flashlight.intensity = _baseIntensity;
            _flashlight.enabled = _batterySource != null && _batterySource.IsOn;
        }

        private void CreateBeamRenderer()
        {
            _beam = gameObject.AddComponent<LineRenderer>();
            _beam.useWorldSpace = true;
            _beam.positionCount = 2;
            _beam.startWidth = _beamWidth;
            _beam.endWidth = _beamWidth * 0.35f;
            _beam.startColor = _beamColor;
            _beam.endColor = new Color(_beamColor.r, _beamColor.g, _beamColor.b, 0f);
            _beam.numCapVertices = 4;
            _beam.alignment = LineAlignment.View;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            _beam.enabled = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _beamMaterial = new Material(shader) { color = _beamColor };
                _beam.material = _beamMaterial;
            }
        }
    }
}