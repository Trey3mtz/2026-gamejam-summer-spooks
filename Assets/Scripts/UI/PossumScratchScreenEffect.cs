using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyGame.UI
{
    /// <summary>
    /// Plays the possum scratch artwork as a screen-space hit effect. The Image and
    /// its sprites are authored in the scene; this component only controls playback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PossumScratchScreenEffect : MonoBehaviour
    {
        [SerializeField] private Image _scratchImage;
        [SerializeField] private Sprite[] _frames;
        [SerializeField, Min(0.01f)] private float _secondsPerFrame = 0.055f;
        [SerializeField, Min(0f)] private float _holdLastFrameSeconds = 0.08f;

        private Coroutine _playback;
        private Sprite[] _activeFrames;
        private float _activeSecondsPerFrame;
        private float _activeHoldLastFrameSeconds;

        private void Awake()
        {
            Hide();
        }

        private void OnDisable()
        {
            if (_playback != null)
                StopCoroutine(_playback);

            _playback = null;
            Hide();
        }

        public void PlayScratch()
        {
            Play(_frames, _secondsPerFrame, _holdLastFrameSeconds);
        }

        public void Play(Sprite[] frames, float secondsPerFrame, float holdLastFrameSeconds)
        {
            if (_scratchImage == null || frames == null || frames.Length == 0)
                return;

            if (_playback != null)
                StopCoroutine(_playback);

            _activeFrames = frames;
            _activeSecondsPerFrame = Mathf.Max(0.01f, secondsPerFrame);
            _activeHoldLastFrameSeconds = Mathf.Max(0f, holdLastFrameSeconds);
            _playback = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            _scratchImage.enabled = true;

            foreach (Sprite frame in _activeFrames)
            {
                if (frame == null)
                    continue;

                _scratchImage.sprite = frame;
                yield return new WaitForSecondsRealtime(_activeSecondsPerFrame);
            }

            if (_activeHoldLastFrameSeconds > 0f)
                yield return new WaitForSecondsRealtime(_activeHoldLastFrameSeconds);

            Hide();
            _playback = null;
        }

        private void Hide()
        {
            if (_scratchImage != null)
            {
                _scratchImage.enabled = false;
                _scratchImage.sprite = null;
            }
        }
    }
}
