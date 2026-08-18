using System.Collections;
using SpookyGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.Gameplay
{
    public enum CollegeSupplyType
    {
        Notebook,
        Calculator,
        StudentId,
        LabGoggles,
        PencilCase,
        Textbook,
        ResearchDrive
    }

    /// <summary>A readable, glowing school-supply objective with automatic close-range collection.</summary>
    [DisallowMultipleComponent]
    public sealed class CollegeCollectible : MonoBehaviour
    {
        [SerializeField] private string _objectiveId;
        [SerializeField] private string _displayName = "Campus Item";
        [SerializeField] private string _buildingName = "Campus Building";
        [SerializeField] private CollegeSupplyType _supplyType;
        [SerializeField] private bool _registerObjective = true;
        [SerializeField, Min(0f)] private float _bobHeight = 0.18f;
        [SerializeField, Min(0f)] private float _bobSpeed = 2.1f;

        private Transform _visual;
        private Transform _label;
        private SphereCollider _trigger;
        private Camera _camera;
        private Material _darkMaterial;
        private Material _accentMaterial;
        private Material _paperMaterial;
        private bool _collected;

        public string ObjectiveId => _objectiveId;

        public void Configure(string objectiveId, string displayName, string buildingName,
            CollegeSupplyType supplyType, bool registerObjective = true)
        {
            _objectiveId = objectiveId;
            _displayName = displayName;
            _buildingName = buildingName;
            _supplyType = supplyType;
            _registerObjective = registerObjective;
        }

        private void Start()
        {
            BuildPhysics();
            BuildVisual();
            if (_registerObjective)
                GameRunDirector.Instance?.RegisterObjective(_objectiveId, _displayName, _buildingName);
        }

        private void Update()
        {
            if (_collected || _visual == null)
                return;

            _visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * _bobSpeed) * _bobHeight);
            _visual.Rotate(Vector3.up, 22f * Time.deltaTime, Space.Self);

            if (_label != null)
            {
                if (_camera == null)
                    _camera = Camera.main;
                if (_camera != null)
                {
                    Vector3 away = _label.position - _camera.transform.position;
                    if (away.sqrMagnitude > 0.001f)
                        _label.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || other.GetComponentInParent<Player.Player>() == null)
                return;

            GameRunDirector director = GameRunDirector.Instance;
            if (director == null || !director.TryCollectObjective(_objectiveId))
                return;

            _collected = true;
            StartCoroutine(CollectRoutine());
        }

        private IEnumerator CollectRoutine()
        {
            _trigger.enabled = false;
            Vector3 startScale = _visual.localScale;
            float elapsed = 0f;
            const float duration = 0.32f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _visual.localScale = Vector3.Lerp(startScale, startScale * 1.7f, t);
                _visual.localPosition += Vector3.up * (2.2f * Time.deltaTime);
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
        }

        private void BuildVisual()
        {
            _visual = new GameObject("School Supply Visual").transform;
            _visual.SetParent(transform, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Color accent = AccentFor(_supplyType);
            _darkMaterial = CreateMaterial(shader, new Color(0.025f, 0.045f, 0.055f, 1f), Color.black);
            _accentMaterial = CreateMaterial(shader, accent, accent * 2.5f);
            _paperMaterial = CreateMaterial(shader, new Color(0.78f, 0.84f, 0.8f, 1f), Color.black);

            switch (_supplyType)
            {
                case CollegeSupplyType.Notebook: BuildBook(false); break;
                case CollegeSupplyType.Textbook: BuildBook(true); break;
                case CollegeSupplyType.Calculator: BuildCalculator(); break;
                case CollegeSupplyType.StudentId: BuildStudentId(); break;
                case CollegeSupplyType.LabGoggles: BuildGoggles(); break;
                case CollegeSupplyType.PencilCase: BuildPencilCase(); break;
                case CollegeSupplyType.ResearchDrive: BuildResearchDrive(); break;
            }

            BuildLabel(accent);
        }

        private void BuildBook(bool thick)
        {
            float thickness = thick ? 0.34f : 0.18f;
            CreatePrimitive("Pages", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f),
                new Vector3(1.05f, thickness, 0.78f), Quaternion.Euler(0f, 12f, 0f), _paperMaterial);
            CreatePrimitive("Cover", PrimitiveType.Cube, new Vector3(0f, 0.4f + thickness * 0.58f, 0f),
                new Vector3(1.14f, 0.07f, 0.86f), Quaternion.Euler(0f, 12f, 0f), _accentMaterial);
            CreatePrimitive("Spine", PrimitiveType.Cube, new Vector3(-0.54f, 0.4f, 0f),
                new Vector3(0.09f, thickness + 0.08f, 0.86f), Quaternion.Euler(0f, 12f, 0f), _darkMaterial);
        }

        private void BuildCalculator()
        {
            CreatePrimitive("Calculator Body", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f),
                new Vector3(0.75f, 0.16f, 1.05f), Quaternion.Euler(0f, -15f, 0f), _darkMaterial);
            CreatePrimitive("Display", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.28f),
                new Vector3(0.52f, 0.05f, 0.22f), Quaternion.Euler(0f, -15f, 0f), _accentMaterial);
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                CreatePrimitive($"Key {row}-{column}", PrimitiveType.Cube,
                    new Vector3((column - 1) * 0.18f, 0.55f, -0.02f - row * 0.18f),
                    new Vector3(0.1f, 0.04f, 0.1f), Quaternion.identity,
                    (row + column) % 3 == 0 ? _accentMaterial : _paperMaterial);
        }

        private void BuildStudentId()
        {
            CreatePrimitive("ID Card", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f),
                new Vector3(1.05f, 0.08f, 0.68f), Quaternion.Euler(0f, 20f, -8f), _paperMaterial);
            CreatePrimitive("ID Stripe", PrimitiveType.Cube, new Vector3(0f, 0.51f, 0.18f),
                new Vector3(0.88f, 0.03f, 0.14f), Quaternion.Euler(0f, 20f, -8f), _accentMaterial);
            CreatePrimitive("Portrait", PrimitiveType.Cube, new Vector3(-0.29f, 0.515f, -0.1f),
                new Vector3(0.22f, 0.03f, 0.25f), Quaternion.Euler(0f, 20f, -8f), _darkMaterial);
        }

        private void BuildGoggles()
        {
            CreatePrimitive("Left Lens", PrimitiveType.Cylinder, new Vector3(-0.32f, 0.5f, 0f),
                new Vector3(0.32f, 0.08f, 0.32f), Quaternion.Euler(90f, 0f, 0f), _accentMaterial);
            CreatePrimitive("Right Lens", PrimitiveType.Cylinder, new Vector3(0.32f, 0.5f, 0f),
                new Vector3(0.32f, 0.08f, 0.32f), Quaternion.Euler(90f, 0f, 0f), _accentMaterial);
            CreatePrimitive("Bridge", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.28f, 0.1f, 0.1f), Quaternion.identity, _darkMaterial);
            CreatePrimitive("Band", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.18f),
                new Vector3(1.25f, 0.08f, 0.08f), Quaternion.identity, _darkMaterial);
        }

        private void BuildPencilCase()
        {
            CreatePrimitive("Case", PrimitiveType.Capsule, new Vector3(0f, 0.45f, 0f),
                new Vector3(0.42f, 0.75f, 0.42f), Quaternion.Euler(0f, 0f, 90f), _accentMaterial);
            for (int i = -1; i <= 1; i++)
                CreatePrimitive($"Pencil {i}", PrimitiveType.Cylinder, new Vector3(i * 0.16f, 0.72f, 0f),
                    new Vector3(0.055f, 0.42f, 0.055f), Quaternion.Euler(0f, 0f, 90f), _paperMaterial);
        }

        private void BuildResearchDrive()
        {
            CreatePrimitive("Drive Body", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.92f, 0.26f, 0.5f), Quaternion.Euler(0f, 18f, 0f), _darkMaterial);
            CreatePrimitive("Energy Core", PrimitiveType.Cube, new Vector3(0f, 0.66f, 0f),
                new Vector3(0.58f, 0.05f, 0.26f), Quaternion.Euler(0f, 18f, 0f), _accentMaterial);
            CreatePrimitive("Connector", PrimitiveType.Cube, new Vector3(0.57f, 0.5f, 0f),
                new Vector3(0.28f, 0.16f, 0.3f), Quaternion.Euler(0f, 18f, 0f), _paperMaterial);
        }

        private void BuildLabel(Color accent)
        {
            GameObject canvasObject = new GameObject("Objective Label", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _label = canvasObject.transform;
            _label.localPosition = Vector3.up * 1.75f;
            _label.localScale = Vector3.one * 0.0075f;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(280f, 78f);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(Shadow));
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 19f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = accent;
            text.text = $"OBJECTIVE\n<size=72%><color=#E3F4F1>{_displayName}</color></size>";
            text.raycastTarget = false;
            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        private void CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(_visual, false);
            primitive.transform.localPosition = position;
            primitive.transform.localRotation = rotation;
            primitive.transform.localScale = scale;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            primitive.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(Shader shader, Color color, Color emission)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.color = color;
            if (emission.maxColorComponent > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        private static Color AccentFor(CollegeSupplyType type) => type switch
        {
            CollegeSupplyType.Calculator => new Color(0.15f, 0.92f, 0.85f),
            CollegeSupplyType.StudentId => new Color(1f, 0.42f, 0.1f),
            CollegeSupplyType.LabGoggles => new Color(0.3f, 0.72f, 1f),
            CollegeSupplyType.PencilCase => new Color(0.95f, 0.18f, 0.5f),
            CollegeSupplyType.Textbook => new Color(0.6f, 0.3f, 1f),
            CollegeSupplyType.ResearchDrive => new Color(0.85f, 0.12f, 1f),
            _ => new Color(1f, 0.72f, 0.08f)
        };

        private void OnDestroy()
        {
            if (_darkMaterial != null) Destroy(_darkMaterial);
            if (_accentMaterial != null) Destroy(_accentMaterial);
            if (_paperMaterial != null) Destroy(_paperMaterial);
        }
    }
}
