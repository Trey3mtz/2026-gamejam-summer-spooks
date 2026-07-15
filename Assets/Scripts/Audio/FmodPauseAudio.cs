using FMOD.Studio;
using FMODUnity;

namespace SpookyGame.Audio
{
    /// <summary>
    /// Drives the "Paused" FMOD snapshot from <see cref="Utilities.GameManager.PauseChanged"/>.
    /// FMOD ignores Time.timeScale, so without this, one-shots and loops keep playing
    /// at full volume behind the pause menu. The snapshot ducks/lowpasses gameplay
    /// buses while leaving the UI bus untouched; fade times are authored on the
    /// snapshot's intensity envelope in FMOD Studio, not here.
    ///
    /// Plain C# subsystem owned by GameManager. Call Shutdown() from OnDestroy —
    /// snapshot instances are manually released, unlike PlayOneShot.
    /// </summary>
    public class FmodPauseAudio
    {
        private const string P_Snapshot = "snapshot:/Paused";

        private EventInstance _snapshot;
        private Utilities.GameManager _owner;
        private bool _initialized;

        public void Initialize(Utilities.GameManager owner)
        {
            if (_initialized) return;

            _owner = owner;
            _snapshot = RuntimeManager.CreateInstance(P_Snapshot);
            _owner.PauseChanged += OnPauseChanged;
            _initialized = true;

            // Cover the (unlikely) case of initializing while already paused.
            if (_owner.IsPaused)
                _snapshot.start();
        }

        public void Shutdown()
        {
            if (!_initialized) return;

            _owner.PauseChanged -= OnPauseChanged;
            _snapshot.stop(STOP_MODE.IMMEDIATE);
            _snapshot.release();
            _initialized = false;
        }

        private void OnPauseChanged(bool paused)
        {
            if (paused)
                _snapshot.start();
            else
                _snapshot.stop(STOP_MODE.ALLOWFADEOUT); // release curve authored in Studio
        }
    }
}
