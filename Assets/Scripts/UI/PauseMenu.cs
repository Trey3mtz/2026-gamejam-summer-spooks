using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SpookyGame.Input;
using SpookyGame.Player;
using SpookyGame.Utilities;

namespace SpookyGame.UI
{
    /// <summary>
    /// Pause menu: owns navigation and presentation, and drives pause state through
    /// GameManager. A Pause press opens the menu, backs out of Settings, or resumes,
    /// depending on where you are. Sets EventSystem focus on each transition so gamepad
    /// navigation has a starting selection.
    ///
    /// Wire the public On*/Show* methods to the matching Button.onClick in the inspector.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private InputReader _inputReader;

        [Header("Panels")]
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Settings tabs")]
        [SerializeField] private GameObject _controlsTab;
        [SerializeField] private GameObject _audioTab;
        [SerializeField] private GameObject _videoTab;

        [Header("Gamepad focus (first selected)")]
        [SerializeField] private GameObject _rootFirstSelected;     // e.g. Resume button
        [SerializeField] private GameObject _settingsFirstSelected; // e.g. Back button

        private bool _inSettings;
        private PlayerController _playerController;
        private Font _menuFont;
        private Button _resumeButton;
        private Button _quitButton;

        private static readonly Color BackdropColor = new Color(0.004f, 0.01f, 0.018f, 0.94f);
        private static readonly Color CardColor = new Color(0.015f, 0.12f, 0.115f, 0.98f);
        private static readonly Color AccentColor = new Color(0.02f, 0.92f, 0.8f, 1f);
        private static readonly Color OrangeColor = new Color(1f, 0.31f, 0.08f, 1f);

        private void OnEnable()  => _inputReader.Pause += OnPausePressed;
        private void OnDisable() => _inputReader.Pause -= OnPausePressed;

        private void Start()
        {
            _playerController = FindAnyObjectByType<PlayerController>();
            BuildRuntimeMenuIfNeeded();
            _rootPanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        private void Update()
        {
            // The detached Game view can continue reporting pointer position while its
            // InputSystemUIInputModule drops the click action. Poll the same physical
            // mouse used by gameplay so essential pause buttons remain clickable.
            if (_rootPanel == null || !_rootPanel.activeInHierarchy || Mouse.current == null)
                return;

            if (!Mouse.current.leftButton.wasPressedThisFrame &&
                !Mouse.current.leftButton.wasReleasedThisFrame)
                return;

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            if (ContainsPointer(_resumeButton, pointerPosition))
                OnResume();
            else if (ContainsPointer(_quitButton, pointerPosition))
                OnQuit();
        }

        // Pause is edge-triggered (fires on 'true' from the reader).
        private void OnPausePressed(bool pressed)
        {
            if (!pressed) return;

            if (!GameManager.Instance.IsPaused) Open();
            else if (_inSettings)               OnBack();
            else                                OnResume();
        }

        private void Open()
        {
            GameManager.Instance.Pause();
            SetCursorLocked(false);
            _inSettings = false;
            _settingsPanel.SetActive(false);
            _rootPanel.SetActive(true);
            Select(_rootFirstSelected);
        }

        // --- Root buttons ---
        public void OnResume()
        {
            GameManager.Instance.Resume();
            _rootPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            Select(null);
            SetCursorLocked(true);
        }

        public void OnSettings()
        {
            _inSettings = true;
            _rootPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            ShowControls();
            Select(_settingsFirstSelected);
        }

        public void OnQuit()
        {
            SetCursorLocked(false);
            Time.timeScale = 1f; // don't leave the app frozen if something intercepts quit
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- Settings ---
        public void OnBack()
        {
            _inSettings = false;
            _settingsPanel.SetActive(false);
            _rootPanel.SetActive(true);
            Select(_rootFirstSelected);
        }

        public void ShowControls() => SetTab(_controlsTab);
        public void ShowAudio()    => SetTab(_audioTab);
        public void ShowVideo()    => SetTab(_videoTab);

        private void SetTab(GameObject active)
        {
            _controlsTab.SetActive(active == _controlsTab);
            _audioTab.SetActive(active == _audioTab);
            _videoTab.SetActive(active == _videoTab);
        }

        private static void Select(GameObject target)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(target);
        }

        private void SetCursorLocked(bool locked)
        {
            if (_playerController == null)
                _playerController = FindAnyObjectByType<PlayerController>();

            if (_playerController != null)
                _playerController.SetCursorLocked(locked);
            else
            {
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
            }
        }

        private void BuildRuntimeMenuIfNeeded()
        {
            if (_rootPanel == null || _rootPanel.transform.Find("Pause Card") != null)
                return;

            _menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Image backdrop = _rootPanel.GetComponent<Image>();
            if (backdrop != null)
                backdrop.color = BackdropColor;

            Canvas menuCanvas = _rootPanel.GetComponent<Canvas>();
            if (menuCanvas == null)
                menuCanvas = _rootPanel.AddComponent<Canvas>();
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = 1500;
            if (_rootPanel.GetComponent<GraphicRaycaster>() == null)
                _rootPanel.AddComponent<GraphicRaycaster>();

            RectTransform card = CreatePanel("Pause Card", _rootPanel.transform,
                new Vector2(760f, 620f), CardColor, AccentColor);

            CreateText("Eyebrow", card, "UTRGV CAMPUS RECOVERY", 22, FontStyle.Bold,
                OrangeColor, new Vector2(0f, -54f), new Vector2(680f, 38f));
            CreateText("Title", card, "GAME PAUSED", 58, FontStyle.Bold,
                Color.white, new Vector2(0f, -118f), new Vector2(680f, 80f));
            CreateText("Objective", card,
                "RECOVER ALL 7 CAMPUS ITEMS  //  SURVIVE THE NIGHT", 19, FontStyle.Bold,
                new Color(0.72f, 0.9f, 0.88f, 1f), new Vector2(0f, -186f), new Vector2(680f, 42f));

            _rootFirstSelected = CreateButton("Resume Button", card, "RESUME GAME",
                new Vector2(0f, -282f), new Vector2(500f, 82f), OnResume);
            _resumeButton = _rootFirstSelected.GetComponent<Button>();
            GameObject quitButtonObject = CreateButton("Quit Button", card, "QUIT TO DESKTOP",
                new Vector2(0f, -386f), new Vector2(500f, 76f), OnQuit);
            _quitButton = quitButtonObject.GetComponent<Button>();

            CreateText("Controls", card,
                "ESC  RESUME     |     M  MAP     |     F  LIGHT     |     R  RELOAD", 17,
                FontStyle.Bold, new Color(0.65f, 0.78f, 0.77f, 1f),
                new Vector2(0f, -505f), new Vector2(690f, 36f));
            CreateText("Mouse", card, "MOVE MOUSE TO LOOK  //  ESC PAUSES AND RELEASES CURSOR", 15,
                FontStyle.Normal, new Color(0.48f, 0.64f, 0.63f, 1f),
                new Vector2(0f, -544f), new Vector2(690f, 32f));
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 size,
            Color backgroundColor, Color outlineColor)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(parent, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = true;
            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            return rect;
        }

        private Text CreateText(string name, Transform parent, string value, int fontSize,
            FontStyle style, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = _menuFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private GameObject CreateButton(string name, Transform parent, string label,
            Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Outline), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.015f, 0.24f, 0.21f, 1f);
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.7f, 0.9f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            CreateText("Label", buttonObject.transform, label, 25, FontStyle.Bold, Color.white,
                new Vector2(0f, 0f), size);
            RectTransform labelRect = buttonObject.transform.Find("Label") as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = Vector2.zero;
            }

            return buttonObject;
        }

        private static bool ContainsPointer(Button button, Vector2 pointerPosition)
        {
            return button != null && button.isActiveAndEnabled &&
                   RectTransformUtility.RectangleContainsScreenPoint(
                       button.transform as RectTransform, pointerPosition, null);
        }
    }
}
