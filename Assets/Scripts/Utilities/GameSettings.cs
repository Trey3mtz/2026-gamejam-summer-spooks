using System;
using UnityEngine;

namespace SpookyGame.Utilities
{
    /// <summary>
    /// Central, PlayerPrefs-backed settings store. Systems read the typed properties and
    /// subscribe to the *Changed events to react. Audio values are stored as linear 0..1
    /// scalars only — nothing here talks to FMOD yet. A future FMOD bridge subscribes to
    /// AudioChanged and pushes the values onto its VCAs.
    /// Call Load() once at boot (e.g. GameManager.Awake).
    /// </summary>
    public static class GameSettings
    {
        // --- Controls ---
        public static float LookSensitivity { get; private set; }
        public static bool  InvertY         { get; private set; }

        // --- Audio (linear 0..1) ---
        public static float MasterVolume   { get; private set; }
        public static float MusicVolume    { get; private set; }
        public static float SfxVolume      { get; private set; }
        public static float AmbienceVolume { get; private set; }

        // --- Video ---
        public static bool FullScreen   { get; private set; }
        public static bool VSync        { get; private set; }
        public static int  QualityLevel { get; private set; }

        public static event Action ControlsChanged = delegate { };
        public static event Action AudioChanged    = delegate { };
        public static event Action VideoChanged    = delegate { };

        public static void Load()
        {
            LookSensitivity = PlayerPrefs.GetFloat(K_Sensitivity, 0.12f);
            InvertY         = PlayerPrefs.GetInt(K_InvertY, 0) == 1;

            MasterVolume    = PlayerPrefs.GetFloat(K_Master,   1f);
            MusicVolume     = PlayerPrefs.GetFloat(K_Music,    1f);
            SfxVolume       = PlayerPrefs.GetFloat(K_Sfx,      1f);
            AmbienceVolume  = PlayerPrefs.GetFloat(K_Ambience, 1f);

            FullScreen      = PlayerPrefs.GetInt(K_FullScreen, 1) == 1;
            VSync           = PlayerPrefs.GetInt(K_VSync, 1) == 1;
            QualityLevel    = PlayerPrefs.GetInt(K_Quality, QualitySettings.GetQualityLevel());

            ApplyVideo();
            RaiseAll();
        }

        // --- Controls ---
        public static void SetLookSensitivity(float value)
        {
            LookSensitivity = Mathf.Max(0.001f, value);
            PlayerPrefs.SetFloat(K_Sensitivity, LookSensitivity);
            ControlsChanged();
        }

        public static void SetInvertY(bool value)
        {
            InvertY = value;
            PlayerPrefs.SetInt(K_InvertY, value ? 1 : 0);
            ControlsChanged();
        }

        // --- Audio (FMOD bridge listens to AudioChanged) ---
        public static void SetMasterVolume(float v)   { MasterVolume   = Clamp01Save(K_Master, v);   AudioChanged(); }
        public static void SetMusicVolume(float v)    { MusicVolume    = Clamp01Save(K_Music, v);    AudioChanged(); }
        public static void SetSfxVolume(float v)      { SfxVolume      = Clamp01Save(K_Sfx, v);      AudioChanged(); }
        public static void SetAmbienceVolume(float v) { AmbienceVolume = Clamp01Save(K_Ambience, v); AudioChanged(); }

        // --- Video ---
        public static void SetFullScreen(bool value)
        {
            FullScreen = value;
            PlayerPrefs.SetInt(K_FullScreen, value ? 1 : 0);
            ApplyVideo();
            VideoChanged();
        }

        public static void SetVSync(bool value)
        {
            VSync = value;
            PlayerPrefs.SetInt(K_VSync, value ? 1 : 0);
            ApplyVideo();
            VideoChanged();
        }

        public static void SetQualityLevel(int level)
        {
            QualityLevel = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            PlayerPrefs.SetInt(K_Quality, QualityLevel);
            ApplyVideo();
            VideoChanged();
        }

        public static void ApplyVideo()
        {
            QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Screen.fullScreenMode = FullScreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
        }

        public static void Save() => PlayerPrefs.Save();

        private static float Clamp01Save(string key, float v)
        {
            v = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(key, v);
            return v;
        }

        private static void RaiseAll()
        {
            ControlsChanged();
            AudioChanged();
            VideoChanged();
        }

        private const string K_Sensitivity = "set.look.sensitivity";
        private const string K_InvertY     = "set.look.invertY";
        private const string K_Master      = "set.audio.master";
        private const string K_Music       = "set.audio.music";
        private const string K_Sfx         = "set.audio.sfx";
        private const string K_Ambience    = "set.audio.ambience";
        private const string K_FullScreen  = "set.video.fullscreen";
        private const string K_VSync       = "set.video.vsync";
        private const string K_Quality     = "set.video.quality";
    }
}