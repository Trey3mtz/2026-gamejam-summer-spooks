using TMPro;
using UnityEngine;
using DG.Tweening;

namespace SpookyGame.Core
{
    ///<Summary>
    /// This is a member of NetworkInteractable. It's simply a class managing the fade in-out of text.   
    ///</Summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class InteractionPrompt : MonoBehaviour
    {
        [Header("Interaction Prompt")]
        [SerializeField] private TextMeshPro text;
        [SerializeField] private Vector3 offset = new Vector3(0, 1f, 0);
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private float moveDuration = 0.35f;
        
        
        private Transform _followTarget;
        private Tween _moveTween;
        private Tween _fadeTween;
        private bool _isArriving;
        
        private void Awake()
        {
            if (text == null)
                text = GetComponent<TextMeshPro >();
            text.fontSize = 3;
            text.alignment = TextAlignmentOptions.CenterGeoAligned;
            text.alpha = 0;

            text.enabled = false;
        }
        
        public void Show(string message, Transform target)
        {
            KillTweens();

            _followTarget = target;
            text.text = message;
            text.alpha = 0f;
            
            transform.position = target.position; // start at target
            //gameObject.SetActive(true);
            text.enabled = true;
            
            Vector3 targetPos = target.position + offset;

            _isArriving = true;

            _moveTween = transform.DOMove(targetPos, moveDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => _isArriving = false);

            _fadeTween = text.DOFade(1f, fadeDuration)
                .SetEase(Ease.OutCubic);
        }

        public void Hide()
        {
            KillTweens();

            _fadeTween = text.DOFade(0f, fadeDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    _followTarget = null;
                    text.enabled = false;
                });
        }
        
        private void LateUpdate()
        {
            if (!_followTarget || _isArriving)
                return;

            UpdatePosition();
        }
        
        
        private Vector3 _velocity;
        private void UpdatePosition()
        {
            Vector3 targetPos = _followTarget.position + offset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _velocity,
                0.08f
            );
        }
        
        
        private void KillTweens()
        {
            _moveTween?.Kill();
            _fadeTween?.Kill();
        }
    }
}