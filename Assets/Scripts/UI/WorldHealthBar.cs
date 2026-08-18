using SpookyGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.UI
{
    /// <summary>Camera-facing world-space health display used by every enemy prefab.</summary>
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

        private ActorHealth _health;
        private Camera _camera;
        private RectTransform _canvasRect;
        private RectTransform _fill;
        private TextMeshProUGUI _label;
        private CanvasGroup _group;
        private float _targetAlpha;

        private void Awake()
        {
            _health = GetComponent<ActorHealth>();
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

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null || _canvasRect == null)
                return;

            Vector3 toCanvas = _canvasRect.position - _camera.transform.position;
            if (toCanvas.sqrMagnitude > 0.001f)
                _canvasRect.rotation = Quaternion.LookRotation(toCanvas.normalized, Vector3.up);

            float distance = Vector3.Distance(_camera.transform.position, transform.position);
            bool damaged = _health.CurrentHp < _health.MaxHp;
            bool shouldShow = !_health.IsDead && distance <= _maximumVisibilityDistance &&
                              (_alwaysVisible || damaged || distance <= _nearVisibilityDistance);
            _targetAlpha = shouldShow ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.deltaTime * 5f);
        }

        private void Refresh(int current, int maximum)
        {
            float normalized = maximum <= 0 ? 0f : (float)current / maximum;
            if (_fill != null)
                _fill.localScale = new Vector3(Mathf.Clamp01(normalized), 1f, 1f);
            if (_label != null)
                _label.text = $"{_displayName}   {current}/{maximum}";
        }

        private void HandleDamaged(int amount)
        {
            _group.alpha = 1f;
        }

        private void HandleDeath()
        {
            _group.alpha = 0f;
            _group.gameObject.SetActive(false);
        }

        private void BuildBar()
        {
            GameObject canvasObject = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvasRect.localPosition = Vector3.up * _height;
            _canvasRect.sizeDelta = new Vector2(_width, 42f);
            _canvasRect.localScale = Vector3.one * _worldScale;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;
            _group = canvasObject.GetComponent<CanvasGroup>();

            Image backdrop = CreateImage("Backdrop", canvasObject.transform, new Color(0.01f, 0.02f, 0.025f, 0.94f));
            Stretch(backdrop.rectTransform, Vector2.zero, Vector2.zero);
            Outline outline = backdrop.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.95f, 0.88f, 0.85f);
            outline.effectDistance = new Vector2(2f, 2f);

            Image track = CreateImage("Track", backdrop.transform, new Color(0.08f, 0.1f, 0.11f, 1f));
            track.rectTransform.anchorMin = new Vector2(0f, 0f);
            track.rectTransform.anchorMax = new Vector2(1f, 0f);
            track.rectTransform.pivot = new Vector2(0.5f, 0f);
            track.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            track.rectTransform.sizeDelta = new Vector2(-10f, 12f);

            Image fill = CreateImage("Fill", track.transform, _barColor);
            Stretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _fill = fill.rectTransform;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(Shadow));
            labelObject.transform.SetParent(backdrop.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            labelRect.sizeDelta = new Vector2(-8f, 22f);
            _label = labelObject.GetComponent<TextMeshProUGUI>();
            _label.font = TMP_Settings.defaultFontAsset;
            _label.fontSize = _labelFontSize;
            _label.fontStyle = FontStyles.Bold;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            labelObject.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 minimumOffset, Vector2 maximumOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimumOffset;
            rect.offsetMax = maximumOffset;
        }
    }
}
