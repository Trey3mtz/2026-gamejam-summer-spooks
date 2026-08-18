using System;
using System.Collections.Generic;
using SpookyGame.Core;
using SpookyGame.Enemies;
using SpookyGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpookyGame.Gameplay
{
    /// <summary>
    /// Owns the complete run state: briefing, objective progress, victory, and defeat.
    /// It is bootstrapped only for the UTRGV game scene, keeping test scenes unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRunDirector : MonoBehaviour
    {
        private enum RunState { Briefing, Active, Won, Lost }

        private sealed class ObjectiveRecord
        {
            public string DisplayName;
            public string Location;
            public bool Collected;
        }

        private static readonly Color Ink = new Color(0.012f, 0.022f, 0.03f, 0.98f);
        private static readonly Color Panel = new Color(0.025f, 0.055f, 0.065f, 0.97f);
        private static readonly Color Cyan = new Color(0.12f, 0.95f, 0.85f, 1f);
        private static readonly Color Orange = new Color(1f, 0.38f, 0.08f, 1f);
        private static readonly Color Pale = new Color(0.76f, 0.93f, 0.92f, 1f);

        private readonly Dictionary<string, ObjectiveRecord> _objectives =
            new Dictionary<string, ObjectiveRecord>(StringComparer.Ordinal);

        private PlayerController _controller;
        private Canvas _canvas;
        private GameObject _overlay;
        private GameObject _objectiveHud;
        private TextMeshProUGUI _menuTitle;
        private TextMeshProUGUI _menuKicker;
        private TextMeshProUGUI _objectiveDescription;
        private TextMeshProUGUI _resultDetails;
        private TextMeshProUGUI _progressText;
        private TextMeshProUGUI _progressDetails;
        private TextMeshProUGUI _notification;
        private Button _primaryButton;
        private TextMeshProUGUI _primaryButtonLabel;
        private RunState _state = RunState.Briefing;
        private int _collectedCount;
        private float _notificationUntil;
        private bool _restartRequested;

        public static GameRunDirector Instance { get; private set; }
        public bool IsRunActive => _state == RunState.Active;
        public int TotalObjectives => _objectives.Count;
        public int CollectedObjectives => _collectedCount;

        public event Action<int, int> ProgressChanged = delegate { };
        public event Action<string> ObjectiveCollected = delegate { };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _controller = GetComponent<PlayerController>();
            BuildInterface();
            SetGameplayEnabled(false);
            Time.timeScale = 0f;
        }

        private void Start()
        {
            PossumSpawnManager spawnManager = FindAnyObjectByType<PossumSpawnManager>();
            if (spawnManager != null && spawnManager.GetComponent<CampusObjectiveSpawner>() == null)
                spawnManager.gameObject.AddComponent<CampusObjectiveSpawner>();

            SelectButton(_primaryButton);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (_notification != null && Time.unscaledTime >= _notificationUntil)
                _notification.gameObject.SetActive(false);

            // Keep the primary menu action usable when a detached Game view reports
            // pointer movement but its UI input module loses the click action.
            if (_overlay != null && _overlay.activeInHierarchy && _primaryButton != null &&
                Mouse.current != null &&
                (Mouse.current.leftButton.wasPressedThisFrame ||
                 Mouse.current.leftButton.wasReleasedThisFrame) &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    _primaryButton.transform as RectTransform,
                    Mouse.current.position.ReadValue(), null))
            {
                if (_state == RunState.Briefing)
                    StartRun();
                else if (_state == RunState.Won || _state == RunState.Lost)
                    RestartRun();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            if (!confirm)
                return;

            if (_state == RunState.Briefing)
                StartRun();
            else if (_state == RunState.Won || _state == RunState.Lost)
                RestartRun();
        }

        public void RegisterObjective(string id, string displayName, string location)
        {
            if (string.IsNullOrWhiteSpace(id) || _objectives.ContainsKey(id))
                return;

            _objectives.Add(id, new ObjectiveRecord
            {
                DisplayName = displayName,
                Location = location,
                Collected = false
            });
            RefreshProgress();
        }

        public bool TryCollectObjective(string id)
        {
            if (_state != RunState.Active || !_objectives.TryGetValue(id, out ObjectiveRecord record) ||
                record.Collected)
                return false;

            record.Collected = true;
            _collectedCount++;
            ObjectiveCollected(record.DisplayName);
            ShowNotification($"RECOVERED  //  {record.DisplayName.ToUpperInvariant()}");
            RefreshProgress();

            if (_collectedCount >= _objectives.Count && _objectives.Count > 0)
                EndRun(true);

            return true;
        }

        /// <summary>Returns true when the game-over flow consumed this death.</summary>
        public bool HandlePlayerDeath()
        {
            if (_state != RunState.Active)
                return false;

            EndRun(false);
            return true;
        }

        private void StartRun()
        {
            if (_state != RunState.Briefing)
                return;

            _state = RunState.Active;
            _overlay.SetActive(false);
            _objectiveHud.SetActive(true);
            Time.timeScale = 1f;
            SetGameplayEnabled(true);
            ShowNotification("OBJECTIVE ACTIVE  //  SEARCH EVERY BUILDING");
        }

        private void EndRun(bool victory)
        {
            _state = victory ? RunState.Won : RunState.Lost;
            SetGameplayEnabled(false);
            Time.timeScale = 0f;
            _objectiveHud.SetActive(false);
            _overlay.SetActive(true);

            _menuKicker.text = victory ? "CAMPUS RECOVERY COMPLETE" : "RUN TERMINATED";
            _menuKicker.color = victory ? Cyan : Orange;
            _menuTitle.text = victory ? "YOU SURVIVED" : "YOU DIED";
            _objectiveDescription.text = victory
                ? "Every missing campus item was recovered. The nightmare has released its hold on UTRGV."
                : "The campus claimed another victim. Recover every item before Vaquero and the possums overwhelm you.";
            _resultDetails.text = $"ITEMS RECOVERED  {_collectedCount:00} / {_objectives.Count:00}";
            _resultDetails.gameObject.SetActive(true);
            ConfigurePrimaryButton("START A NEW RUN", RestartRun);
            SelectButton(_primaryButton);
        }

        private void RestartRun()
        {
            if (_restartRequested)
                return;

            _restartRequested = true;
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadSceneAsync(active.buildIndex, LoadSceneMode.Single);
        }

        private void SetGameplayEnabled(bool enabled)
        {
            if (_controller != null)
            {
                _controller.enabled = enabled;
                _controller.SetCursorLocked(enabled);
            }

            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        private void RefreshProgress()
        {
            int total = _objectives.Count;
            if (_progressText != null)
                _progressText.text = $"CAMPUS RECOVERY  {_collectedCount:00} / {total:00}";
            if (_progressDetails != null)
                _progressDetails.text = _collectedCount < total
                    ? "SEARCH CLASSROOMS  //  FINAL ITEM: LA LLORONA"
                    : "ALL CAMPUS ITEMS RECOVERED";
            if (_objectiveDescription != null && _state == RunState.Briefing)
            {
                _objectiveDescription.text = total == 0
                    ? "Search the campus buildings for missing school supplies. One final item is held by La Llorona."
                    : $"Recover all {total} missing campus items. Search a different room in every building, defeat La Llorona for the final item, and do not die.";
            }

            ProgressChanged(_collectedCount, total);
        }

        private void ShowNotification(string message)
        {
            if (_notification == null)
                return;
            _notification.text = message;
            _notification.gameObject.SetActive(true);
            _notificationUntil = Time.unscaledTime + 2.4f;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new GameObject("Game Run Interface", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            BuildBriefingOverlay(canvasObject.transform);
            BuildObjectiveHud(canvasObject.transform);
        }

        private void BuildBriefingOverlay(Transform parent)
        {
            Image background = CreateImage("Briefing Backdrop", parent, Ink);
            Stretch(background.rectTransform);
            background.raycastTarget = true;
            _overlay = background.gameObject;

            Image topLine = CreateImage("Top Accent", background.transform, Orange);
            SetAnchored(topLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -72f),
                new Vector2(760f, 5f));

            Image panel = CreateImage("Mission Panel", background.transform, Panel);
            SetAnchored(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(900f, 680f));
            AddOutline(panel, Cyan, new Vector2(2f, -2f));

            _menuKicker = CreateText("Kicker", panel.transform, "UTRGV CAMPUS RECOVERY", 20f, Orange,
                TextAlignmentOptions.Center, new Vector2(0f, -46f), new Vector2(800f, 34f),
                new Vector2(0.5f, 1f));
            _menuTitle = CreateText("Title", panel.transform, "SUMMER SPOOKS", 58f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, -102f), new Vector2(800f, 82f),
                new Vector2(0.5f, 1f));

            CreateText("Mission Header", panel.transform, "PRIMARY OBJECTIVE", 18f, Cyan,
                TextAlignmentOptions.Left, new Vector2(64f, -222f), new Vector2(760f, 28f),
                new Vector2(0f, 1f));
            _objectiveDescription = CreateText("Mission", panel.transform,
                "Search the campus buildings for missing school supplies. One final item is held by La Llorona.",
                25f, Pale, TextAlignmentOptions.TopLeft, new Vector2(64f, -264f),
                new Vector2(770f, 132f), new Vector2(0f, 1f), false);

            Image warning = CreateImage("Warning", panel.transform, new Color(0.22f, 0.055f, 0.035f, 0.95f));
            SetAnchored(warning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f),
                new Vector2(770f, 72f));
            CreateText("Warning Text", warning.transform,
                "SURVIVAL RULE  //  DEATH ENDS THE RUN", 21f, new Color(1f, 0.58f, 0.28f),
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(720f, 50f), new Vector2(0.5f, 0.5f));

            Color controlsColor = new Color(0.56f, 0.72f, 0.73f);
            CreateText("Controls Movement", panel.transform,
                "WASD  MOVE     •     SHIFT  SPRINT     •     E  INTERACT",
                15f, controlsColor, TextAlignmentOptions.Center,
                new Vector2(0f, 176f), new Vector2(700f, 24f), new Vector2(0.5f, 0f));
            CreateText("Controls Combat", panel.transform,
                "F  FLASHLIGHT   •   LMB  FIRE   •   R  RELOAD   •   MOVE MOUSE TO LOOK",
                15f, controlsColor, TextAlignmentOptions.Center,
                new Vector2(0f, 146f), new Vector2(700f, 24f), new Vector2(0.5f, 0f));

            _primaryButton = CreateButton("Primary Button", panel.transform, "BEGIN SEARCH",
                new Vector2(0f, 55f), new Vector2(390f, 76f));
            _primaryButtonLabel = _primaryButton.GetComponentInChildren<TextMeshProUGUI>();
            ConfigurePrimaryButton("BEGIN SEARCH", StartRun);

            _resultDetails = CreateText("Result Details", panel.transform, string.Empty, 22f, Pale,
                TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(600f, 36f),
                new Vector2(0.5f, 0.5f));
            _resultDetails.gameObject.SetActive(false);
        }

        private void BuildObjectiveHud(Transform parent)
        {
            Image panel = CreateImage("Objective HUD", parent, new Color(0.015f, 0.04f, 0.05f, 0.94f));
            SetAnchored(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(480f, 68f));
            AddOutline(panel, Cyan, new Vector2(2f, -2f));
            _objectiveHud = panel.gameObject;
            _objectiveHud.SetActive(false);

            _progressText = CreateText("Progress", panel.transform, "CAMPUS RECOVERY  00 / 00", 18f,
                Color.white, TextAlignmentOptions.Center, new Vector2(0f, -10f),
                new Vector2(440f, 24f), new Vector2(0.5f, 1f));
            _progressDetails = CreateText("Progress Details", panel.transform,
                "SEARCH CLASSROOMS  //  FINAL ITEM: LA LLORONA", 11f, Pale,
                TextAlignmentOptions.Center, new Vector2(0f, 10f), new Vector2(450f, 18f),
                new Vector2(0.5f, 0f));

            _notification = CreateText("Objective Notification", parent, string.Empty, 17f, Cyan,
                TextAlignmentOptions.Center, new Vector2(0f, -110f), new Vector2(620f, 32f),
                new Vector2(0.5f, 1f));
            _notification.gameObject.SetActive(false);
        }

        private void ConfigurePrimaryButton(string label, UnityEngine.Events.UnityAction action)
        {
            _primaryButton.onClick.RemoveAllListeners();
            _primaryButton.onClick.AddListener(action);
            _primaryButtonLabel.text = label;
        }

        private static Button CreateButton(string name, Transform parent, string label,
            Vector2 position, Vector2 size)
        {
            Image image = CreateImage(name, parent, new Color(0.04f, 0.42f, 0.38f, 1f));
            SetAnchored(image.rectTransform, new Vector2(0.5f, 0f), position, size);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.04f, 0.42f, 0.38f, 1f);
            colors.highlightedColor = new Color(0.08f, 0.68f, 0.58f, 1f);
            colors.pressedColor = Orange;
            colors.selectedColor = new Color(0.08f, 0.68f, 0.58f, 1f);
            button.colors = colors;
            AddOutline(image, Cyan, new Vector2(2f, -2f));
            CreateText("Label", image.transform, label, 25f, Color.white, TextAlignmentOptions.Center,
                Vector2.zero, size - new Vector2(20f, 12f), new Vector2(0.5f, 0.5f));
            return button;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            instance.transform.SetParent(parent, false);
            Image image = instance.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions,
            Vector2 anchor, bool noWrap = true)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(Shadow));
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            TextMeshProUGUI label = instance.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = noWrap ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;

            Shadow shadow = instance.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.82f);
            outline.effectDistance = distance;
        }

        private static void SelectButton(Button button)
        {
            if (EventSystem.current != null && button != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
