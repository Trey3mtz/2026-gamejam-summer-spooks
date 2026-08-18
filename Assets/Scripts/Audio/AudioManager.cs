using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SpookyGame.Utilities;
using UnityEngine;

namespace SpookyGame.Audio
{
    /// <summary>
    /// Pooled one-shot / looping sound player. Drop the Audio_Manager prefab into the first
    /// scene and call it from anywhere:
    ///     AudioManager.Instance.PlaySoundFX(jumpSFX, transform);
    /// Sounds play in 3D at (and following) the given transform. No AudioMixer needed —
    /// volumes come from GameSettings: Master drives AudioListener.volume, Music/SFX are
    /// applied per source, so the existing settings menu keeps working.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Wired in the Audio_Manager prefab")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private Transform _gameSFXSource;
        [SerializeField] private Transform _gameSFX_Reversed;

        [Header("3D settings applied to pooled sources")]
        [SerializeField] private float _minDistance = 1f;
        [SerializeField] private float _maxDistance = 25f;

        [Header("Pool sizes (grow on demand)")]
        [SerializeField] private int _amountToPool = 20;
        [SerializeField] private int _amountToPoolReverse = 5;

        private readonly List<GameObject> pooledSoundFX = new List<GameObject>(24);
        private readonly List<GameObject> pooledReverseSFX = new List<GameObject>(8);
        private float _musicBaseVolume = 1f;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // The prefab can be dropped into a scene with no GameManager and still work.
            if (!GameSettings.IsLoaded)
                GameSettings.Load();

            for (int i = 0; i < _amountToPool; i++)
                pooledSoundFX.Add(CreateNewSFXobj(reversed: false));
            for (int i = 0; i < _amountToPoolReverse; i++)
                pooledReverseSFX.Add(CreateNewSFXobj(reversed: true));

            GameSettings.AudioChanged += ApplyVolumes;
            ApplyVolumes();
        }

        private void Start()
        {
            if (GameManager.Instance)
                GameManager.Instance.PauseChanged += OnPauseChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            GameSettings.AudioChanged -= ApplyVolumes;
            if (GameManager.Instance)
                GameManager.Instance.PauseChanged -= OnPauseChanged;
        }

        private void OnPauseChanged(bool paused)
        {
            if (paused) PauseAudioSFX();
            else UnpauseAudioSFX();
        }

// *********************************************************************************************  play methods

        public void PlaySoundFX(AudioClip clip, Transform objSource) => PlaySoundFX(clip, objSource, 1f, 1f);
        public void PlaySoundFX(AudioClip clip, Transform objSource, float volumeAdjust) => PlaySoundFX(clip, objSource, volumeAdjust, 1f);
        public void PlaySoundFX(AudioClip clip, float pitchAdjust, Transform objSource) => PlaySoundFX(clip, objSource, 1f, pitchAdjust);

        public void PlaySoundFX(AudioClip clip, Transform objSource, float volumeAdjust, float pitchAdjust)
        {
            if (!clip) return;
            AudioSource source = SetupSource(pooledSoundFX, clip, objSource, volumeAdjust, pitchAdjust, spatial: true);
            source.Play();
            TimedSetActive(clip.length, source.gameObject, objSource, unscaled: false);
        }

        /// <summary>
        /// Plays a looping sfx and returns its AudioSource — the caller owns stopping it
        /// (disable the returned source's GameObject, or call KillGameObjectsCurrentAudio).
        /// The sound follows objSource and returns to the pool when objSource is destroyed.
        /// </summary>
        public AudioSource PlayLoopingSFX(AudioClip clip, Transform objSource, float volumeAdjust = 1f, float pitchAdjust = 1f)
        {
            if (!clip) return null;
            AudioSource source = SetupSource(pooledSoundFX, clip, objSource, volumeAdjust, pitchAdjust, spatial: true);
            source.loop = true;
            source.Play();
            LoopingSFXActive(source.gameObject, objSource);
            return source;
        }

        /// <summary> 3D one-shot that keeps timing while Time.timeScale is 0. </summary>
        public void PlaySoundFXUnscaled(AudioClip clip, Transform objSource)
        {
            if (!clip) return;
            AudioSource source = SetupSource(pooledSoundFX, clip, objSource, 1f, 1f, spatial: true);
            source.Play();
            TimedSetActive(clip.length, source.gameObject, objSource, unscaled: true);
        }

        // Old call shape from the previous project — the transform is irrelevant for 2D sounds.
        public void PlayUISoundFX(AudioClip clip, Transform objSource) => PlayUISoundFX(clip);
        public void PlayUISoundFX(AudioClip clip, Transform objSource, float volumeAdjust, float pitchAdjust) => PlayUISoundFX(clip, volumeAdjust, pitchAdjust);

        /// <summary> 2D one-shot, unaffected by pause/timeScale — menus, clicks, notifications. </summary>
        public void PlayUISoundFX(AudioClip clip, float volumeAdjust = 1f, float pitchAdjust = 1f)
        {
            if (!clip) return;
            AudioSource source = SetupSource(pooledSoundFX, clip, transform, volumeAdjust, pitchAdjust, spatial: false);
            source.Play();
            TimedSetActive(clip.length, source.gameObject, null, unscaled: true);
        }

        public void PlayReversedSoundFX(AudioClip clip, Transform objSource, float volumeAdjust = 1f, float pitchAdjust = 1f)
        {
            if (!clip) return;
            if (pitchAdjust > 0) pitchAdjust *= -1;
            AudioSource source = SetupSource(pooledReverseSFX, clip, objSource, volumeAdjust, pitchAdjust, spatial: true);
            source.timeSamples = clip.samples - 1;
            source.Play();
            TimedSetActive(clip.length, source.gameObject, objSource, unscaled: false);
        }

// *********************************************************************************************  music

        public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
        {
            if (!_musicSource || !clip) return;
            _musicBaseVolume = volume;
            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = volume * GameSettings.MusicVolume;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource) _musicSource.Stop();
        }

// *********************************************************************************************  volume / global control

        private void ApplyVolumes()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (_musicSource)
                _musicSource.volume = _musicBaseVolume * GameSettings.MusicVolume;
            RefreshPoolVolumes(pooledSoundFX);
            RefreshPoolVolumes(pooledReverseSFX);
        }

        private static void RefreshPoolVolumes(List<GameObject> pool)
        {
            foreach (GameObject obj in pool)
            {
                if (!obj || !obj.activeInHierarchy) continue;
                obj.GetComponent<AudioSource>().volume =
                    obj.GetComponent<AudioReference>().baseVolume * GameSettings.SfxVolume;
            }
        }

        public void PauseAudioSFX()
        {
            foreach (GameObject obj in pooledSoundFX)
                if (obj.activeInHierarchy)
                    obj.GetComponent<AudioSource>().Pause();
        }

        public void UnpauseAudioSFX()
        {
            foreach (GameObject obj in pooledSoundFX)
                if (obj.activeInHierarchy)
                    obj.GetComponent<AudioSource>().UnPause();
        }

        public void KillAllCurrentActiveAudioSFX()
        {
            foreach (GameObject obj in pooledSoundFX)
                if (obj.activeInHierarchy)
                    ReturnToPool(obj);
        }

        /// <summary> Stops every pooled sound that was started by killedObj (matched by root transform). </summary>
        public void KillGameObjectsCurrentAudio(Transform killedObj)
        {
            foreach (GameObject sound in pooledSoundFX)
                if (sound.activeInHierarchy && sound.GetComponent<AudioReference>().myReference == killedObj.root)
                    ReturnToPool(sound);
        }

// *********************************************************************************************  pool internals

        private AudioSource SetupSource(List<GameObject> pool, AudioClip clip, Transform objSource, float volumeAdjust, float pitchAdjust, bool spatial)
        {
            GameObject obj = GetPooledObject(pool);
            if (!obj)
            {
                obj = CreateNewSFXobj(pool == pooledReverseSFX);
                pool.Add(obj);
            }

            obj.transform.position = objSource ? objSource.position : transform.position;

            AudioReference reference = obj.GetComponent<AudioReference>();
            reference.myReference = objSource ? objSource.root : null;
            reference.baseVolume = volumeAdjust;

            AudioSource source = obj.GetComponent<AudioSource>();
            source.clip = clip;
            source.volume = volumeAdjust * GameSettings.SfxVolume;
            source.pitch = pitchAdjust;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;

            obj.SetActive(true);
            return source;
        }

        private GameObject CreateNewSFXobj(bool reversed)
        {
            GameObject soundObject = new GameObject(reversed ? "Pooled Reverse Sound" : "Pooled Sound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();
            soundObject.AddComponent<AudioReference>();

            audioSource.playOnAwake = false;
            audioSource.minDistance = _minDistance;
            audioSource.maxDistance = _maxDistance;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.dopplerLevel = 0f;
            audioSource.spread = 150f;

            Transform parent = reversed && _gameSFX_Reversed ? _gameSFX_Reversed
                             : _gameSFXSource ? _gameSFXSource : transform;
            soundObject.transform.SetParent(parent);
            soundObject.SetActive(false);
            return soundObject;
        }

        private static GameObject GetPooledObject(List<GameObject> pool)
        {
            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].activeInHierarchy)
                    return pool[i];
            return null;
        }

        public void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            obj.GetComponent<AudioSource>().Stop();
        }

        // Follows objSource for the clip's duration, then returns the object to the pool.
        private async void TimedSetActive(float timer, GameObject sfxObj, Transform objSource, bool unscaled)
        {
            try
            {
                AudioSource sfxAudioSource = sfxObj.GetComponent<AudioSource>();
                while (timer > 0)
                {
                    if (objSource)
                        sfxObj.transform.position = objSource.position;

                    if (!sfxObj.activeInHierarchy)
                        return; // someone else already returned it to the pool

                    timer -= unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                    await UniTask.Yield();
                }

                // Ensure the audio has actually finished (pitch < 1 plays longer than clip.length).
                while (sfxObj && sfxAudioSource.isPlaying)
                    await UniTask.Yield();

                if (sfxObj)
                    sfxObj.SetActive(false);
            }
            catch { } // object or manager destroyed mid-flight — nothing to clean up
        }

        private async void LoopingSFXActive(GameObject sfxObj, Transform objSource)
        {
            try
            {
                while (objSource)
                {
                    if (!sfxObj.activeInHierarchy)
                        return;

                    sfxObj.transform.position = objSource.position;
                    await UniTask.Yield();
                }
                ReturnToPool(sfxObj);
            }
            catch { }
        }
    }

    /// <summary> Tags a pooled sound with who started it, so their audio can be killed with them. </summary>
    public class AudioReference : MonoBehaviour
    {
        public Transform myReference;
        public float baseVolume = 1f;
    }
}
