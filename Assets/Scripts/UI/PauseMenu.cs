using UnityEngine;
using UnityEngine.EventSystems;
using SpookyGame.Input;
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

        private void OnEnable()  => _inputReader.Pause += OnPausePressed;
        private void OnDisable() => _inputReader.Pause -= OnPausePressed;

        private void Start()
        {
            _rootPanel.SetActive(false);
            _settingsPanel.SetActive(false);
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
    }
}