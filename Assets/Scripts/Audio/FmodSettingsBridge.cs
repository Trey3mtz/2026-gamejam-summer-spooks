using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace SpookyGame.Audio
{
    /// <summary>
    /// Bridges <see cref="Utilities.GameSettings"/> audio scalars onto FMOD VCAs.
    /// Plain C# subsystem owned by GameManager: constructed after banks are loaded,
    /// subscribes to AudioChanged, and pushes the four linear 0..1 values whenever
    /// they change. GameSettings never references FMOD; this class never writes
    /// PlayerPrefs. Call Shutdown() from the owner's OnDestroy.
    /// </summary>
    public class FmodSettingsBridge
    {
        // VCA paths as authored in FMOD Studio's mixer.
        private const string P_Master   = "vca:/Master";
        private const string P_Music    = "vca:/Music";
        private const string P_Sfx      = "vca:/SFX";
        private const string P_Ambience = "vca:/Ambience";

        private VCA _master;
        private VCA _music;
        private VCA _sfx;
        private VCA _ambience;
        private bool _initialized;

        /// <summary>
        /// Resolves the VCAs and applies the current settings. Requires the master
        /// bank (with .strings) to be loaded — with the integration's default
        /// "Load All Banks at Initialization", any time from Start() onward is safe.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // GetVCA throws EventNotFoundException on a bad path — deliberate.
            // A silent audio-settings failure is worse than a loud one.
            _master   = RuntimeManager.GetVCA(P_Master);
            _music    = RuntimeManager.GetVCA(P_Music);
            _sfx      = RuntimeManager.GetVCA(P_Sfx);
            _ambience = RuntimeManager.GetVCA(P_Ambience);
            _initialized = true;

            Utilities.GameSettings.AudioChanged += Apply;
            Apply();
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            Utilities.GameSettings.AudioChanged -= Apply;
            _initialized = false;
        }

        private void Apply()
        {
            // setVolume is a linear amplitude multiplier, matching the 0..1
            // scalars GameSettings stores. If sliders end up feeling like all
            // the audible change happens in the top third, square the value
            // here (v * v) for a cheap perceptual curve — settings storage
            // and UI stay untouched.
            _master.setVolume(Utilities.GameSettings.MasterVolume);
            _music.setVolume(Utilities.GameSettings.MusicVolume);
            _sfx.setVolume(Utilities.GameSettings.SfxVolume);
            _ambience.setVolume(Utilities.GameSettings.AmbienceVolume);
        }
    }
}
