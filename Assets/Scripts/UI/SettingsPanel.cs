using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpookyGame.Utilities;

namespace SpookyGame.UI
{
    /// <summary>
    /// Binds the uGUI settings widgets to GameSettings. Each control is initialized from the
    /// stored value with SetValueWithoutNotify so seeding the UI does not fire the setter
    /// (which would redundantly write PlayerPrefs). User changes route to the matching
    /// setter; GameManager.Resume() flushes to disk.
    ///
    /// Uses TMP_Dropdown; swap for UnityEngine.UI.Dropdown if the project uses legacy text.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Slider _sensitivity;
        [SerializeField] private Toggle _invertY;
        [SerializeField] private float _minSensitivity = 0.02f;
        [SerializeField] private float _maxSensitivity = 0.5f;
        [SerializeField] private TMP_Dropdown _dynamicCamera;

        [Header("Audio")]
        [SerializeField] private Slider _master;
        [SerializeField] private Slider _music;
        [SerializeField] private Slider _sfx;
        [SerializeField] private Slider _ambience;

        [Header("Video")]
        [SerializeField] private Toggle _fullscreen;
        [SerializeField] private Toggle _vsync;
        [SerializeField] private TMP_Dropdown _quality;

        private void Awake()
        {
            InitControls();
            InitAudio();
            InitVideo();
        }

        private void InitControls()
        {
            _sensitivity.minValue = _minSensitivity;
            _sensitivity.maxValue = _maxSensitivity;
            _sensitivity.SetValueWithoutNotify(GameSettings.LookSensitivity);
            _sensitivity.onValueChanged.AddListener(GameSettings.SetLookSensitivity);

            _invertY.SetIsOnWithoutNotify(GameSettings.InvertY);
            _invertY.onValueChanged.AddListener(GameSettings.SetInvertY);
            
            _dynamicCamera.ClearOptions();
            _dynamicCamera.AddOptions(new List<string> { "Off", "Lite", "On" });
            _dynamicCamera.SetValueWithoutNotify((int)GameSettings.DynamicCamera);
            _dynamicCamera.onValueChanged.AddListener(GameSettings.SetDynamicCamera);
        }

        private void InitAudio()
        {
            BindVolume(_master,   GameSettings.MasterVolume,   GameSettings.SetMasterVolume);
            BindVolume(_music,    GameSettings.MusicVolume,    GameSettings.SetMusicVolume);
            BindVolume(_sfx,      GameSettings.SfxVolume,      GameSettings.SetSfxVolume);
            BindVolume(_ambience, GameSettings.AmbienceVolume, GameSettings.SetAmbienceVolume);
        }

        private static void BindVolume(Slider slider, float value,
                                       UnityEngine.Events.UnityAction<float> setter)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(setter);
        }

        private void InitVideo()
        {
            _fullscreen.SetIsOnWithoutNotify(GameSettings.FullScreen);
            _fullscreen.onValueChanged.AddListener(GameSettings.SetFullScreen);

            _vsync.SetIsOnWithoutNotify(GameSettings.VSync);
            _vsync.onValueChanged.AddListener(GameSettings.SetVSync);

            _quality.ClearOptions();
            _quality.AddOptions(new List<string>(QualitySettings.names));
            _quality.SetValueWithoutNotify(GameSettings.QualityLevel);
            _quality.onValueChanged.AddListener(GameSettings.SetQualityLevel);
        }
    }
}
