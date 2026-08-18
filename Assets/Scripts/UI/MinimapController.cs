using System.Collections.Generic;
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
        private readonly struct BuildingLabelSpec
        {
            public readonly string ObjectName;
            public readonly string Initials;
            public readonly string DisplayName;

            public BuildingLabelSpec(string objectName, string initials, string displayName)
            {
                ObjectName = objectName;
                Initials = initials;
                DisplayName = displayName;
            }
        }

        private sealed class BuildingLabelRuntime
        {
            public Vector3 WorldPosition;
            public RectTransform Marker;
        }

        private static readonly BuildingLabelSpec[] BuildingLabels =
        {
            new BuildingLabelSpec("B10_Learning_Center", "LC", "Learning Center"),
            new BuildingLabelSpec("B11_B12_Science_Planetarium_Complex", "SP", "Science / Planetarium"),
            new BuildingLabelSpec("B13_Fieldhouse", "FH", "Fieldhouse"),
            new BuildingLabelSpec("B14_Engineering_Building", "ENG", "Engineering"),
            new BuildingLabelSpec("B15_Health_Physical_Education_II", "HPE", "Health & PE II"),
            new BuildingLabelSpec("B16_Academic_Services", "AS", "Academic Services"),
            new BuildingLabelSpec("B27_Health_PE_Complex", "HPC", "Health & PE Complex")
        };

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
        private GameObject _fullMapOverlayRoot;
        private GameObject _fullMapBackdrop;
        private GameObject _legendPanel;
        private Canvas _fullMapCanvas;
        private readonly List<BuildingLabelRuntime> _buildingLabelRuntime = new List<BuildingLabelRuntime>();
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
            DestroyFullMapOverlay();
            DestroyFullMapBackdrop();
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
            float availableWidth = Mathf.Max(220f, parentWidth - (_fullMapMargin * 2f));
            float availableHeight = Mathf.Max(220f, parentHeight - (_fullMapMargin * 2f));
            float side = Mathf.Max(220f, Mathf.Min(availableWidth, availableHeight) * 0.92f);

            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(side, side);
            _panel.SetAsLastSibling();

            // The objective and combat HUDs use their own high-order canvases. Give the
            // expanded map its own temporary sorting layer so no HUD text or crosshair
            // can draw through it.
            _fullMapCanvas = _panel.GetComponent<Canvas>();
            if (_fullMapCanvas == null)
                _fullMapCanvas = _panel.gameObject.AddComponent<Canvas>();
            _fullMapCanvas.overrideSorting = true;
            _fullMapCanvas.sortingOrder = 900;
            _fullMapCanvas.enabled = true;

            EnsureFullMapBackdrop(parentWidth, parentHeight);
            EnsureHint();
            EnsureFullMapOverlay();
            if (_fullMapHint != null)
            {
                _fullMapHint.gameObject.SetActive(true);
                _fullMapHint.transform.SetAsLastSibling();
            }
            if (_fullMapOverlayRoot != null)
                _fullMapOverlayRoot.SetActive(true);
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

            if (_fullMapCanvas != null)
            {
                _fullMapCanvas.overrideSorting = false;
                _fullMapCanvas.sortingOrder = 0;
                _fullMapCanvas.enabled = false;
            }

            if (_fullMapBackdrop != null)
                _fullMapBackdrop.SetActive(false);
            if (_fullMapHint != null)
                _fullMapHint.gameObject.SetActive(false);
            if (_fullMapOverlayRoot != null)
                _fullMapOverlayRoot.SetActive(false);
            if (_northLabel != null)
                _northLabel.SetActive(true);
        }

        private void EnsureFullMapBackdrop(float parentWidth, float parentHeight)
        {
            if (_panel == null)
                return;

            if (_fullMapBackdrop == null)
            {
                _fullMapBackdrop = new GameObject("Full Map Backdrop", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                _fullMapBackdrop.transform.SetParent(_panel, false);

                Image backdrop = _fullMapBackdrop.GetComponent<Image>();
                backdrop.color = new Color(0.004f, 0.008f, 0.014f, 0.97f);
                backdrop.raycastTarget = true;
            }

            RectTransform backdropRect = _fullMapBackdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = backdropRect.anchorMax = backdropRect.pivot = new Vector2(0.5f, 0.5f);
            backdropRect.anchoredPosition = Vector2.zero;
            backdropRect.sizeDelta = new Vector2(parentWidth + 8f, parentHeight + 8f);
            _fullMapBackdrop.transform.SetAsFirstSibling();
            _fullMapBackdrop.SetActive(true);
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
            hintRect.anchoredPosition = new Vector2(0f, -14f);
            hintRect.sizeDelta = new Vector2(520f, 38f);

            _fullMapHint = hintObject.GetComponent<Text>();
            _fullMapHint.text = "UTRGV CAMPUS MAP   //   M TO CLOSE";
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

        private void EnsureFullMapOverlay()
        {
            if (_fullMapOverlayRoot != null || _panel == null)
                return;

            _fullMapOverlayRoot = new GameObject("Full Map Building Overlay", typeof(RectTransform));
            _fullMapOverlayRoot.transform.SetParent(_panel, false);
            RectTransform overlayRect = _fullMapOverlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Font font = _playerMarker != null ? _playerMarker.GetComponent<Text>()?.font : null;
            foreach (BuildingLabelSpec spec in BuildingLabels)
            {
                GameObject building = GameObject.Find(spec.ObjectName);
                if (building == null || !TryGetBuildingCenter(building, out Vector3 worldPosition))
                    continue;

                RectTransform marker = CreateBuildingMarker(overlayRect, spec.Initials, font);
                _buildingLabelRuntime.Add(new BuildingLabelRuntime
                {
                    WorldPosition = worldPosition,
                    Marker = marker
                });
            }

            CreateLegend(overlayRect, font);
            _fullMapOverlayRoot.SetActive(_isFullMap);
        }

        private static RectTransform CreateBuildingMarker(RectTransform parent, string initials, Font font)
        {
            GameObject markerObject = new GameObject($"Building {initials}", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            markerObject.transform.SetParent(parent, false);
            RectTransform marker = markerObject.GetComponent<RectTransform>();
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(initials.Length > 2 ? 58f : 46f, 30f);

            Image background = markerObject.GetComponent<Image>();
            background.color = new Color(0.015f, 0.035f, 0.045f, 0.94f);
            background.raycastTarget = false;
            Outline frame = markerObject.GetComponent<Outline>();
            frame.effectColor = new Color(0.05f, 0.95f, 0.82f, 0.95f);
            frame.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Initials", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(marker, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.text = initials;
            text.font = font;
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 1f, 0.98f, 1f);
            text.raycastTarget = false;
            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            return marker;
        }

        private void CreateLegend(RectTransform parent, Font font)
        {
            _legendPanel = new GameObject("Building Legend", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            _legendPanel.transform.SetParent(parent, false);
            RectTransform legendRect = _legendPanel.GetComponent<RectTransform>();
            // Keep the legend in the intentionally empty lower-left map margin;
            // it must never cover a building footprint or its initials marker.
            legendRect.anchorMin = legendRect.anchorMax = legendRect.pivot = new Vector2(0f, 0f);
            legendRect.anchoredPosition = new Vector2(18f, 18f);
            legendRect.sizeDelta = new Vector2(310f, 176f);

            Image background = _legendPanel.GetComponent<Image>();
            background.color = new Color(0.01f, 0.025f, 0.035f, 0.94f);
            background.raycastTarget = false;
            Outline frame = _legendPanel.GetComponent<Outline>();
            frame.effectColor = new Color(0.05f, 0.78f, 0.72f, 0.9f);
            frame.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Legend Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(legendRect, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 12f);
            textRect.offsetMax = new Vector2(-12f, -12f);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.82f, 0.96f, 0.95f, 1f);
            text.raycastTarget = false;

            string legend = "BUILDING LEGEND\n";
            foreach (BuildingLabelSpec spec in BuildingLabels)
                legend += $"{spec.Initials,-4}  {spec.DisplayName}\n";
            text.text = legend.TrimEnd();
        }

        private static bool TryGetBuildingCenter(GameObject building, out Vector3 center)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                center = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);
                return true;
            }

            Collider[] colliders = building.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);
                center = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);
                return true;
            }

            center = building.transform.position;
            return true;
        }

        private void UpdateBuildingLabels()
        {
            if (!_isFullMap || _fullMapOverlayRoot == null || _minimapCamera == null || _panel == null)
                return;

            float width = Mathf.Max(1f, _panel.rect.width - 18f);
            float height = Mathf.Max(1f, _panel.rect.height - 18f);
            foreach (BuildingLabelRuntime label in _buildingLabelRuntime)
            {
                Vector3 viewport = _minimapCamera.WorldToViewportPoint(label.WorldPosition);
                bool visible = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                               viewport.y >= 0f && viewport.y <= 1f;
                label.Marker.gameObject.SetActive(visible);
                if (visible)
                {
                    label.Marker.anchoredPosition = new Vector2(
                        (viewport.x - 0.5f) * width,
                        (viewport.y - 0.5f) * height);
                }
            }
        }

        private void DestroyFullMapOverlay()
        {
            if (_fullMapOverlayRoot == null)
                return;

            GameObject overlay = _fullMapOverlayRoot;
            _fullMapOverlayRoot = null;
            _legendPanel = null;
            _buildingLabelRuntime.Clear();
            if (Application.isPlaying)
                Destroy(overlay);
            else
                DestroyImmediate(overlay);
        }

        private void DestroyFullMapBackdrop()
        {
            if (_fullMapBackdrop == null)
                return;

            GameObject backdrop = _fullMapBackdrop;
            _fullMapBackdrop = null;
            if (Application.isPlaying)
                Destroy(backdrop);
            else
                DestroyImmediate(backdrop);
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
            // The compact map keeps its slightly angled presentation. The expanded map
            // is a true overhead plan so roads/buildings are not visually skewed.
            float activePitch = _isFullMap ? 90f : _cameraPitch;
            Quaternion cameraRotation = Quaternion.Euler(activePitch, 0f, 0f);
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

            UpdateBuildingLabels();
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
