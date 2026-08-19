using SpookyGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyGame.UI
{
    /// <summary>
    /// Camera-facing world-space health display used by every enemy prefab.
    /// The bar is real 3D geometry (quads with the URP Lit material), so the render
    /// pipeline lights it like any other mesh — invisible in darkness, revealed by the
    /// flashlight or room lights. Only the TMP label is unlit by nature, so its color is
    /// dimmed from a cheap sample of the light hitting the bar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private string _displayName = "HOSTILE";
        [SerializeField, Min(0.1f)] private float _height = 2f;
        [SerializeField, Min(80f)] private float _width = 180f;
        [SerializeField, Min(0.001f)] private float _worldScale = 0.008f;
        [SerializeField, Min(8f)] private float _labelFontSize = 15f;
        [SerializeField] private bool _alwaysVisible;
        [SerializeField, Min(1f)] private float _nearVisibilityDistance = 12f;
        [SerializeField, Min(1f)] private float _maximumVisibilityDistance = 45f;
        [SerializeField] private Color _barColor = new Color(0.92f, 0.12f, 0.16f, 1f);

        [Header("Label lighting (TMP text cannot be lit by the pipeline)")]
        [SerializeField, Range(0f, 1f)] private float _labelMinBrightness = 0.04f;
        [SerializeField, Min(0f)] private float _lightSensitivity = 1f;
        [Tooltip("Layers that block light (walls). Auto-defaults to 'GroundWall' if left as Nothing.")]
        [SerializeField] private LayerMask _occluderMask;

        private ActorHealth _health;
        private Camera _camera;
        private Transform _root;
        private Transform _fillPivot;
        private TextMeshPro _label;

        private Material[] _materials;
        private Color[] _baseColors;
        private float _alpha = 1f;
        private float _targetAlpha;

        private float _labelBrightness = 1f;
        private float _targetLabelBrightness = 1f;
        private float _sampleTimer;

        // Shared across all bars so many enemies cost one scene scan, refreshed every couple seconds.
        private static Light[] _sceneLights = System.Array.Empty<Light>();
        private static float _lightsRefreshedAt = float.NegativeInfinity;

        private void Awake()
        {
            _health = GetComponent<ActorHealth>();
            if (_occluderMask == 0)
            {
                int groundWall = LayerMask.GetMask("GroundWall");
                _occluderMask = groundWall != 0 ? groundWall : LayerMask.GetMask("Default");
            }
            BuildBar();
        }

        private void OnEnable()
        {
            _health.HealthChanged += Refresh;
            _health.Damaged += HandleDamaged;
            _health.Died += HandleDeath;
        }

        private void Start()
        {
            _camera = Camera.main;
            Refresh(_health.CurrentHp, _health.MaxHp);
        }

        private void OnDisable()
        {
            if (_health == null)
                return;
            _health.HealthChanged -= Refresh;
            _health.Damaged -= HandleDamaged;
            _health.Died -= HandleDeath;
        }

        private void OnDestroy()
        {
            if (_materials == null)
                return;
            foreach (Material material in _materials)
                if (material)
                    Destroy(material);
        }

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null || _root == null)
                return;

            Vector3 toBar = _root.position - _camera.transform.position;
            if (toBar.sqrMagnitude > 0.001f)
                _root.rotation = Quaternion.LookRotation(toBar.normalized, Vector3.up);

            float distance = Vector3.Distance(_camera.transform.position, transform.position);
            bool damaged = _health.CurrentHp < _health.MaxHp;
            bool shouldShow = !_health.IsDead && distance <= _maximumVisibilityDistance &&
                              (_alwaysVisible || damaged || distance <= _nearVisibilityDistance);
            _targetAlpha = shouldShow ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, Time.deltaTime * 5f);

            if (_alpha > 0.001f)
                UpdateLabelLighting();

            ApplyVisuals();
        }

        private void Refresh(int current, int maximum)
        {
            float normalized = maximum <= 0 ? 0f : (float)current / maximum;
            if (_fillPivot != null)
                _fillPivot.localScale = new Vector3(Mathf.Clamp01(normalized), 1f, 1f);
            if (_label != null)
                _label.text = $"{_displayName}   {current}/{maximum}";
        }

        private void HandleDamaged(int amount)
        {
            _alpha = 1f;
        }

        private void HandleDeath()
        {
            _alpha = 0f;
            if (_root != null)
            {
                ApplyVisuals();
                _root.gameObject.SetActive(false);
            }
        }

        private void ApplyVisuals()
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                Color c = _baseColors[i];
                c.a *= _alpha;
                _materials[i].SetColor("_BaseColor", c);
            }

            if (_label != null)
            {
                float b = _labelBrightness;
                _label.color = new Color(b, b, b, _alpha);
            }
        }

        private void UpdateLabelLighting()
        {
            _sampleTimer -= Time.deltaTime;
            if (_sampleTimer <= 0f)
            {
                _sampleTimer = 0.1f;
                _targetLabelBrightness = SampleBrightness(_root.position);
            }
            _labelBrightness = Mathf.MoveTowards(_labelBrightness, _targetLabelBrightness, Time.deltaTime * 4f);
        }

        /// <summary>
        /// Approximate light level at the bar: ambient plus every enabled light with simple
        /// distance/cone falloff. One linecast per contributing light so walls block it.
        /// </summary>
        private float SampleBrightness(Vector3 position)
        {
            if (Time.unscaledTime - _lightsRefreshedAt > 2f)
            {
                _lightsRefreshedAt = Time.unscaledTime;
                _sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            }

            float total = RenderSettings.ambientLight.grayscale * RenderSettings.ambientIntensity;

            foreach (Light light in _sceneLights)
            {
                if (!light || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;

                float contribution;
                Vector3 lightPosition = light.transform.position;

                if (light.type == LightType.Directional)
                {
                    contribution = light.intensity;
                    lightPosition = position - light.transform.forward * 50f;
                }
                else
                {
                    Vector3 toLight = lightPosition - position;
                    float distance = toLight.magnitude;
                    if (light.range <= 0f || distance >= light.range)
                        continue;

                    float attenuation = 1f - distance / light.range;
                    contribution = light.intensity * attenuation * attenuation;

                    if (light.type == LightType.Spot)
                    {
                        float angle = Vector3.Angle(light.transform.forward, -toLight);
                        float halfCone = light.spotAngle * 0.5f;
                        if (angle > halfCone)
                            continue;
                        // Soft edge over the outer 40% of the cone.
                        contribution *= Mathf.InverseLerp(halfCone, halfCone * 0.6f, angle);
                    }
                }

                if (contribution <= 0.01f)
                    continue;

                if (Physics.Linecast(position, lightPosition, _occluderMask, QueryTriggerInteraction.Ignore))
                    continue;

                total += contribution * _lightSensitivity;
            }

            return Mathf.Clamp01(Mathf.Max(_labelMinBrightness, total));
        }

// *********************************************************************************************  construction

        // Layout is authored in "pixel" units matching the old UI version (backdrop 180x42),
        // then the root's localScale converts to world units. The camera sees the quads'
        // front faces; more negative local Z is closer to the camera.
        private void BuildBar()
        {
            GameObject rootObject = new GameObject("Enemy Health Bar");
            rootObject.transform.SetParent(transform, false);
            _root = rootObject.transform;
            _root.localPosition = Vector3.up * _height;
            _root.localScale = Vector3.one * (_worldScale * (_width / 180f));

            var materials = new System.Collections.Generic.List<Material>();
            var baseColors = new System.Collections.Generic.List<Color>();

            // Teal frame the old UI Outline effect drew around the backdrop.
            CreateQuad("Outline", _root, new Vector3(0f, 0f, 2f), new Vector2(184f, 46f),
                new Color(0.16f, 0.95f, 0.88f, 0.85f), materials, baseColors);
            CreateQuad("Backdrop", _root, new Vector3(0f, 0f, 1f), new Vector2(180f, 42f),
                new Color(0.01f, 0.02f, 0.025f, 0.94f), materials, baseColors);
            CreateQuad("Track", _root, new Vector3(0f, -10f, 0f), new Vector2(170f, 12f),
                new Color(0.08f, 0.1f, 0.11f, 1f), materials, baseColors);

            // Fill scales from its left edge: pivot sits at the track's inner-left, quad hangs off +X.
            GameObject pivotObject = new GameObject("Fill Pivot");
            pivotObject.transform.SetParent(_root, false);
            pivotObject.transform.localPosition = new Vector3(-83f, -10f, -1f);
            _fillPivot = pivotObject.transform;
            CreateQuad("Fill", _fillPivot, new Vector3(83f, 0f, 0f), new Vector2(166f, 8f),
                _barColor, materials, baseColors);

            _materials = materials.ToArray();
            _baseColors = baseColors.ToArray();

            GameObject labelObject = new GameObject("Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(_root, false);
            _label = labelObject.GetComponent<TextMeshPro>();
            _label.rectTransform.localPosition = new Vector3(0f, 8f, -2f);
            _label.rectTransform.sizeDelta = new Vector2(172f, 22f);
            _label.font = TMP_Settings.defaultFontAsset;
            // World-space TMP sizes ~10x smaller than UGUI for the same font size value;
            // scale so the serialized size means what it did on the old canvas version.
            _label.fontSize = _labelFontSize * 10f;
            _label.fontStyle = FontStyles.Bold;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Replaces the old UI Shadow component: dark underlay behind the glyphs.
            Material fontMaterial = _label.fontMaterial;
            fontMaterial.EnableKeyword("UNDERLAY_ON");
            fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.9f));
            fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        }

        private static void CreateQuad(string name, Transform parent, Vector3 localPosition, Vector2 size,
            Color color, System.Collections.Generic.List<Material> materials,
            System.Collections.Generic.List<Color> baseColors)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Material material = CreateLitTransparentMaterial(color);
            renderer.sharedMaterial = material;

            materials.Add(material);
            baseColors.Add(color);
        }

        /// <summary>Standard URP Lit, switched to transparent so the visibility fade works.</summary>
        private static Material CreateLitTransparentMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", 1f); // 0 = opaque, 1 = transparent
            material.SetFloat("_Blend", 0f);   // alpha blend
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetColor("_BaseColor", color);
            return material;
        }
    }
}