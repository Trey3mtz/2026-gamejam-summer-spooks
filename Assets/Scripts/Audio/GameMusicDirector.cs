using System.Collections;
using SpookyGame.Gameplay;
using SpookyGame.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpookyGame.Audio
{
    /// <summary>
    /// Authored music state controller for briefing, active gameplay, pause, and victory.
    /// It listens to existing game-state events and owns two dedicated 2D AudioSources.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameMusicDirector : MonoBehaviour
    {
        [Header("Authored sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _ambienceSource;

        [Header("Music")]
        [SerializeField] private AudioClip _mainMenuMusic;
        [SerializeField] private AudioClip _gameplayMusic;
        [SerializeField] private AudioClip _pauseMusic;
        [SerializeField] private AudioClip _victoryCue;
        [SerializeField] private AudioClip _winningMusic;

        [Header("Ambience")]
        [SerializeField] private AudioClip _gameplayAmbience;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float _menuVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _gameplayVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float _pauseVolume = 0.38f;
        [SerializeField, Range(0f, 1f)] private float _winningVolume = 0.48f;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.18f;

        private GameRunDirector _runDirector;
        private GameManager _gameManager;
        private Coroutine _victoryRoutine;
        private float _musicBaseVolume;
        private float _gameplayResumeTime;
        private bool _runActive;
        private bool _runEnded;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameSettings.AudioChanged += ApplyVolumes;
        }

        private void Start()
        {
            BindGameManager();
            BindRunDirector();
            PlayMainMenu();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.AudioChanged -= ApplyVolumes;
            UnbindGameManager();
            UnbindRunDirector();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopVictoryRoutine();
            UnbindRunDirector();
            BindGameManager();
            BindRunDirector();
            _runActive = false;
            _runEnded = false;
            _gameplayResumeTime = 0f;
            PlayMainMenu();
        }

        private void BindGameManager()
        {
            GameManager current = GameManager.Instance;
            if (_gameManager == current)
                return;

            UnbindGameManager();
            _gameManager = current;
            if (_gameManager != null)
                _gameManager.PauseChanged += OnPauseChanged;
        }

        private void UnbindGameManager()
        {
            if (_gameManager != null)
                _gameManager.PauseChanged -= OnPauseChanged;
            _gameManager = null;
        }

        private void BindRunDirector()
        {
            _runDirector = FindAnyObjectByType<GameRunDirector>();
            if (_runDirector == null)
                return;

            _runDirector.RunStarted += OnRunStarted;
            _runDirector.RunEnded += OnRunEnded;
        }

        private void UnbindRunDirector()
        {
            if (_runDirector != null)
            {
                _runDirector.RunStarted -= OnRunStarted;
                _runDirector.RunEnded -= OnRunEnded;
            }
            _runDirector = null;
        }

        private void OnRunStarted()
        {
            StopVictoryRoutine();
            _runActive = true;
            _runEnded = false;
            _gameplayResumeTime = 0f;
            PlayMusic(_gameplayMusic, _gameplayVolume, true);
            PlayAmbience();
        }

        private void OnRunEnded(bool victory)
        {
            _runActive = false;
            _runEnded = true;
            StopAmbience();

            if (!victory)
            {
                StopVictoryRoutine();
                if (_musicSource != null)
                    _musicSource.Stop();
                return;
            }

            StopVictoryRoutine();
            _victoryRoutine = StartCoroutine(PlayVictorySequence());
        }

        private void OnPauseChanged(bool paused)
        {
            if (!_runActive || _runEnded)
                return;

            if (paused)
            {
                if (_musicSource != null && _musicSource.clip == _gameplayMusic)
                    _gameplayResumeTime = _musicSource.time;
                PlayMusic(_pauseMusic, _pauseVolume, true);
                if (_ambienceSource != null)
                    _ambienceSource.Pause();
            }
            else
            {
                PlayMusic(_gameplayMusic, _gameplayVolume, true, _gameplayResumeTime);
                if (_ambienceSource != null && _gameplayAmbience != null)
                    _ambienceSource.UnPause();
            }
        }

        private IEnumerator PlayVictorySequence()
        {
            PlayMusic(_victoryCue, _winningVolume, false);
            if (_victoryCue != null)
                yield return new WaitForSecondsRealtime(_victoryCue.length);

            if (_runEnded)
                PlayMusic(_winningMusic, _winningVolume, false);
            _victoryRoutine = null;
        }

        private void PlayMainMenu()
        {
            StopAmbience();
            PlayMusic(_mainMenuMusic, _menuVolume, true);
        }

        private void PlayMusic(AudioClip clip, float volume, bool loop, float startTime = 0f)
        {
            if (_musicSource == null || clip == null)
                return;

            _musicBaseVolume = volume;
            _musicSource.spatialBlend = 0f;
            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.time = clip.length > 0.05f
                ? Mathf.Clamp(startTime, 0f, clip.length - 0.05f)
                : 0f;
            ApplyVolumes();
            _musicSource.Play();
        }

        private void PlayAmbience()
        {
            if (_ambienceSource == null || _gameplayAmbience == null)
                return;

            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.clip = _gameplayAmbience;
            _ambienceSource.loop = true;
            ApplyVolumes();
            _ambienceSource.Play();
        }

        private void StopAmbience()
        {
            if (_ambienceSource != null)
                _ambienceSource.Stop();
        }

        private void ApplyVolumes()
        {
            float musicSetting = GameSettings.IsLoaded ? GameSettings.MusicVolume : 1f;
            if (_musicSource != null)
                _musicSource.volume = _musicBaseVolume * musicSetting;
            if (_ambienceSource != null)
                _ambienceSource.volume = _ambienceVolume * musicSetting;
        }

        private void StopVictoryRoutine()
        {
            if (_victoryRoutine == null)
                return;
            StopCoroutine(_victoryRoutine);
            _victoryRoutine = null;
        }
    }
}
