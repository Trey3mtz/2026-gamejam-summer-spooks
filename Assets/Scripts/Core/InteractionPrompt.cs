using TMPro;
using UnityEngine;
using DG.Tweening;

namespace SpookyGame.Core
{
    ///<Summary>
    /// This is a member of Interactable. It's simply a class managing the fade in-out of text.   
    ///</Summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class InteractionPrompt : MonoBehaviour
    {
        [Header("Interaction Prompt")]
        [SerializeField] private TextMeshPro text;
        [SerializeField] private Vector3 offset = new Vector3(0, 1f, 0);
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private float moveDuration = 0.35f;
        [SerializeField] private float minTextSize = 0.35f;
        [SerializeField] private float maxTextSize = 0.22f;
        
        [Header("Distance Scaling")]
        [Tooltip("X-axis: Distance to camera. Y-axis: Resulting local scale multiplier.")]
        [SerializeField] private AnimationCurve scaleDistanceCurve = AnimationCurve.Linear(0.5f, 0.35f, 2f, 2f); // (lowest distance, smallest textsize, longest distance, largest textsize)
       
        private Transform _cameraTransform;
        private Transform _followTarget;
        
        private Tween _moveTween;
        private Tween _fadeTween;
        private bool _isArriving;
        
        private void Awake()
        {
            if (text == null)
                text = GetComponent<TextMeshPro >();
            text.fontSize = maxTextSize;
            text.alignment = TextAlignmentOptions.CenterGeoAligned;
            text.alpha = 0;

            text.enabled = false;
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
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

            UpdateScale();
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

            // Only update position if the initial arrival tween is finished
            if (!_isArriving)
            {
                UpdatePosition();
            }
            
            UpdateScale();
        }
        
        private void UpdateScale()
        {
            if (!_cameraTransform) return;

            // Measure straight-line distance from the prompt to the camera
            float distance = Vector3.Distance(transform.position, _cameraTransform.position);
            
            // Map the distance to a scale factor via the serialized curve
            float scaleValue = Mathf.Clamp01(scaleDistanceCurve.Evaluate(distance));
            
            // Apply uniform scaling
            text.fontSize = Mathf.Lerp(minTextSize, maxTextSize, scaleValue);
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
