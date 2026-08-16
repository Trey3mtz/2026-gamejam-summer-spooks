using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpookyGame.UI
{
    /// <summary>
    /// Keeps an orthographic camera above the player and presents its output in the HUD.
    /// The map is north-up while the center marker shows the player's facing direction.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Camera _minimapCamera;
        [SerializeField] private RawImage _display;
        [SerializeField] private RectTransform _playerMarker;
        [SerializeField] private RectTransform _panel;

        [Header("Map View")]
        [SerializeField, Min(1f)] private float _cameraHeight = 80f;
        [SerializeField, Range(55f, 90f)] private float _cameraPitch = 72f;
        [SerializeField, Min(1f)] private float _viewSize = 70f;
        [SerializeField, Range(64, 1024)] private int _textureSize = 256;
        [SerializeField] private LayerMask _cullingMask = 1 << 3;

        [Header("Full Map (M)")]
        [SerializeField] private Vector2 _fullMapCenter = new Vector2(24f, -24f);
        [SerializeField, Min(1f)] private float _fullMapViewSize = 160f;
        [SerializeField, Min(0f)] private float _fullMapMargin = 32f;
        [SerializeField, Range(64, 1024)] private int _fullMapTextureSize = 1024;

        private RenderTexture _renderTexture;
        private Text _fullMapHint;
        private GameObject _northLabel;
        private bool _isFullMap;
        private Vector2 _compactAnchorMin;
        private Vector2 _compactAnchorMax;
        private Vector2 _compactAnchoredPosition;
        private Vector2 _compactSizeDelta;
        private Vector2 _compactPivot;

        private void OnEnable()
        {
            ResolvePanel();
            RememberCompactLayout();
            CreateRenderTexture();
            ConfigureCamera();
            UpdateView();
        }

        private void Update()
        {
            if (!Application.isPlaying || Keyboard.current == null) return;

            if (Keyboard.current.mKey.wasPressedThisFrame)
                SetFullMap(!_isFullMap);
        }

        private void LateUpdate()
        {
            UpdateView();
        }

        private void OnValidate()
        {
            _cameraHeight = Mathf.Max(1f, _cameraHeight);
            _cameraPitch = Mathf.Clamp(_cameraPitch, 55f, 90f);
            _viewSize = Mathf.Max(1f, _viewSize);
            _textureSize = Mathf.Clamp(_textureSize, 64, 1024);
            _fullMapViewSize = Mathf.Max(1f, _fullMapViewSize);
            _fullMapMargin = Mathf.Max(0f, _fullMapMargin);
            _fullMapTextureSize = Mathf.Clamp(_fullMapTextureSize, 64, 1024);

            if (isActiveAndEnabled)
            {
                ConfigureCamera();
                UpdateView();
            }
        }

        private void OnDisable()
        {
            if (_isFullMap)
                ApplyCompactLayout();

            DestroyHint();
            ReleaseRenderTexture();
        }

        private void ResolvePanel()
        {
            if (_panel == null && _display != null)
                _panel = _display.rectTransform.parent as RectTransform;

            if (_northLabel == null && _panel != null)
            {
                Transform northTransform = _panel.Find("NorthLabel");
                if (northTransform != null)
                    _northLabel = northTransform.gameObject;
            }
        }

        private void RememberCompactLayout()
        {
            if (_panel == null) return;

            _compactAnchorMin = _panel.anchorMin;
            _compactAnchorMax = _panel.anchorMax;
            _compactAnchoredPosition = _panel.anchoredPosition;
            _compactSizeDelta = _panel.sizeDelta;
            _compactPivot = _panel.pivot;
        }

        private void SetFullMap(bool show)
        {
            _isFullMap = show;
            ResolvePanel();

            if (_isFullMap)
                ApplyFullMapLayout();
            else
                ApplyCompactLayout();

            CreateRenderTexture();
            ConfigureCamera();
            UpdateView();
        }

        private void ApplyFullMapLayout()
        {
            if (_panel == null) return;

            RectTransform parent = _panel.parent as RectTransform;
            float parentWidth = parent != null ? parent.rect.width : Screen.width;
            float parentHeight = parent != null ? parent.rect.height : Screen.height;
            float side = Mathf.Max(220f, Mathf.Min(parentWidth, parentHeight) - (_fullMapMargin * 2f));

            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(side, side);
            _panel.SetAsLastSibling();

            EnsureHint();
            if (_fullMapHint != null)
                _fullMapHint.gameObject.SetActive(true);
            if (_northLabel != null)
                _northLabel.SetActive(false);
        }

        private void ApplyCompactLayout()
        {
            if (_panel == null) return;

            _panel.anchorMin = _compactAnchorMin;
            _panel.anchorMax = _compactAnchorMax;
            _panel.pivot = _compactPivot;
            _panel.anchoredPosition = _compactAnchoredPosition;
            _panel.sizeDelta = _compactSizeDelta;

            if (_fullMapHint != null)
                _fullMapHint.gameObject.SetActive(false);
            if (_northLabel != null)
                _northLabel.SetActive(true);
        }

        private void EnsureHint()
        {
            if (_fullMapHint != null || _panel == null || _playerMarker == null) return;

            Text markerText = _playerMarker.GetComponent<Text>();
            GameObject hintObject = new GameObject("FullMapHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            hintObject.transform.SetParent(_panel, false);

            RectTransform hintRect = hintObject.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -10f);
            hintRect.sizeDelta = new Vector2(480f, 34f);

            _fullMapHint = hintObject.GetComponent<Text>();
            _fullMapHint.text = "CAMPUS MAP   |   M TO CLOSE";
            _fullMapHint.font = markerText != null ? markerText.font : null;
            _fullMapHint.fontSize = 20;
            _fullMapHint.fontStyle = FontStyle.Bold;
            _fullMapHint.alignment = TextAnchor.UpperCenter;
            _fullMapHint.color = new Color(0.85f, 1f, 0.96f, 1f);
            _fullMapHint.raycastTarget = false;

            Outline outline = hintObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.01f, 0.02f, 0.025f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void DestroyHint()
        {
            if (_fullMapHint == null) return;

            GameObject hintObject = _fullMapHint.gameObject;
            _fullMapHint = null;

            if (Application.isPlaying)
                Destroy(hintObject);
            else
                DestroyImmediate(hintObject);
        }

        private void CreateRenderTexture()
        {
            ReleaseRenderTexture();

            int activeTextureSize = _isFullMap ? _fullMapTextureSize : _textureSize;
            _renderTexture = new RenderTexture(activeTextureSize, activeTextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Minimap Runtime Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                hideFlags = HideFlags.DontSave
            };
            _renderTexture.Create();

            if (_display != null)
                _display.texture = _renderTexture;
        }

        private void ConfigureCamera()
        {
            if (_minimapCamera == null) return;

            _minimapCamera.orthographic = true;
            _minimapCamera.orthographicSize = _isFullMap ? _fullMapViewSize : _viewSize;
            _minimapCamera.nearClipPlane = 0.1f;
            _minimapCamera.farClipPlane = _cameraHeight + 50f;
            _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            _minimapCamera.backgroundColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            _minimapCamera.cullingMask = _cullingMask;
            _minimapCamera.targetTexture = _renderTexture;
            _minimapCamera.enabled = _renderTexture != null;
        }

        private void UpdateView()
        {
            if (_target == null || _minimapCamera == null) return;

            Vector3 targetPosition = _isFullMap
                ? new Vector3(_fullMapCenter.x, _target.position.y, _fullMapCenter.y)
                : _target.position;
            Quaternion cameraRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            Vector3 cameraForward = cameraRotation * Vector3.forward;
            float targetDistance = _cameraHeight / -cameraForward.y;
            _minimapCamera.transform.SetPositionAndRotation(
                targetPosition - cameraForward * targetDistance,
                cameraRotation);

            if (_playerMarker != null)
            {
                _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_target.eulerAngles.y);

                if (_isFullMap && _panel != null)
                {
                    float mapWidth = Mathf.Max(1f, _panel.rect.width - 14f);
                    float mapHeight = Mathf.Max(1f, _panel.rect.height - 14f);
                    Vector2 worldOffset = new Vector2(
                        _target.position.x - _fullMapCenter.x,
                        _target.position.z - _fullMapCenter.y);
                    _playerMarker.anchoredPosition = new Vector2(
                        worldOffset.x / (_fullMapViewSize * 2f) * mapWidth,
                        worldOffset.y / (_fullMapViewSize * 2f) * mapHeight);
                    _playerMarker.sizeDelta = new Vector2(42f, 42f);
                }
                else
                {
                    _playerMarker.anchoredPosition = Vector2.zero;
                    _playerMarker.sizeDelta = new Vector2(34f, 34f);
                }
            }
        }

        private void ReleaseRenderTexture()
        {
            if (_minimapCamera != null && _minimapCamera.targetTexture == _renderTexture)
            {
                _minimapCamera.targetTexture = null;
                _minimapCamera.enabled = false;
            }

            if (_display != null && _display.texture == _renderTexture)
                _display.texture = null;

            if (_renderTexture == null) return;

            _renderTexture.Release();
            if (Application.isPlaying)
                Destroy(_renderTexture);
            else
                DestroyImmediate(_renderTexture);

            _renderTexture = null;
        }
    }
}
