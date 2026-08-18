using System.Collections;
using SpookyGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.Core.Interactables
{
    /// <summary>Visible UV-cell pickup that supplies reserve shots and a small battery recharge.</summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightAmmoPickup : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _ammoAmount = 12;
        [SerializeField, Min(0)] private int _batteryRecharge = 35;
        [SerializeField, Min(0f)] private float _bobHeight = 0.22f;
        [SerializeField, Min(0f)] private float _bobSpeed = 2f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 34f;

        private Vector3 _basePosition;
        private Transform _visualRoot;
        private Transform _labelRoot;
        private SphereCollider _trigger;
        private Camera _camera;
        private TextMeshProUGUI _pickupLabel;
        private bool _collected;
        private Material _darkMaterial;
        private Material _glowMaterial;

        public int AmmoAmount => _ammoAmount;

        private void Awake()
        {
            _basePosition = transform.position;
            BuildPhysics();
            BuildVisual();
        }

        private void Update()
        {
            if (_collected || _visualRoot == null)
                return;

            float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            _visualRoot.localPosition = Vector3.up * bob;
            _visualRoot.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.Self);

            if (_labelRoot != null)
            {
                if (_camera == null)
                    _camera = Camera.main;
                if (_camera != null)
                {
                    Vector3 awayFromCamera = _labelRoot.position - _camera.transform.position;
                    if (awayFromCamera.sqrMagnitude > 0.001f)
                        _labelRoot.rotation = Quaternion.LookRotation(awayFromCamera.normalized, Vector3.up);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected)
                return;

            Player.Player player = other.GetComponentInParent<Player.Player>();
            if (player == null)
                return;

            int previousBattery = player.Flashlight.BatteryLife;
            int addedAmmo = player.AddFlashlightAmmo(_ammoAmount, _batteryRecharge);
            bool batteryAdded = player.Flashlight.BatteryLife > previousBattery;
            if (addedAmmo <= 0 && !batteryAdded)
                return;

            _collected = true;
            StartCoroutine(CollectRoutine());
        }

        public void Configure(int ammoAmount, int batteryRecharge)
        {
            _ammoAmount = Mathf.Max(1, ammoAmount);
            _batteryRecharge = Mathf.Max(0, batteryRecharge);
            RefreshLabel();
        }

        private IEnumerator CollectRoutine()
        {
            _trigger.enabled = false;
            float elapsed = 0f;
            const float duration = 0.28f;
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

        private void BuildPhysics()
        {
            _trigger = gameObject.AddComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius = 1.15f;

            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void BuildVisual()
        {
            _visualRoot = new GameObject("UV Cell Visual").transform;
            _visualRoot.SetParent(transform, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            _darkMaterial = new Material(shader);
            SetMaterialColor(_darkMaterial, new Color(0.025f, 0.045f, 0.055f, 1f));
            _glowMaterial = new Material(shader);
            Color glow = new Color(0.05f, 0.95f, 0.82f, 1f);
            SetMaterialColor(_glowMaterial, glow);
            if (_glowMaterial.HasProperty("_EmissionColor"))
            {
                _glowMaterial.EnableKeyword("_EMISSION");
                _glowMaterial.SetColor("_EmissionColor", glow * 3f);
            }

            CreatePrimitive("Cell Housing", PrimitiveType.Cube, _visualRoot,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.9f, 1.15f, 0.42f), _darkMaterial);
            CreatePrimitive("Energy Window", PrimitiveType.Cube, _visualRoot,
                new Vector3(0f, 0.64f, -0.23f), new Vector3(0.58f, 0.72f, 0.06f), _glowMaterial);
            CreatePrimitive("Top Contact", PrimitiveType.Cylinder, _visualRoot,
                new Vector3(0f, 1.28f, 0f), new Vector3(0.23f, 0.08f, 0.23f), _glowMaterial);
            CreatePrimitive("Bottom Contact", PrimitiveType.Cylinder, _visualRoot,
                new Vector3(0f, -0.02f, 0f), new Vector3(0.23f, 0.08f, 0.23f), _glowMaterial);

            BuildLabel(glow);
        }

        private void BuildLabel(Color accent)
        {
            GameObject canvasObject = new GameObject("Pickup Label", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _labelRoot = canvasObject.transform;
            _labelRoot.localPosition = Vector3.up * 2.05f;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(180f, 42f);
            canvasRect.localScale = Vector3.one * 0.008f;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(Shadow));
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _pickupLabel = textObject.GetComponent<TextMeshProUGUI>();
            _pickupLabel.font = TMP_Settings.defaultFontAsset;
            _pickupLabel.fontSize = 16f;
            _pickupLabel.fontStyle = FontStyles.Bold;
            _pickupLabel.color = accent;
            _pickupLabel.alignment = TextAlignmentOptions.Center;
            _pickupLabel.raycastTarget = false;
            RefreshLabel();
            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        private void RefreshLabel()
        {
            if (_pickupLabel != null)
                _pickupLabel.text = $"UV CELLS +{_ammoAmount}   |   POWER +{_batteryRecharge}%";
        }

        private static void CreatePrimitive(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            primitive.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            material.color = color;
        }

        private void OnDestroy()
        {
            if (_darkMaterial != null)
                Destroy(_darkMaterial);
            if (_glowMaterial != null)
                Destroy(_glowMaterial);
        }
    }
}
