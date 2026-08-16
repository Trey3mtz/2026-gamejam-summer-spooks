using System.Collections;
using UnityEngine;

namespace SpookyGame.Core.Interactables
{
    /// <summary>
    /// An interactable hinged door that plugs into the player's existing E-key sensor.
    /// The interaction target can control one leaf or a paired set of leaves.
    /// </summary>
    public sealed class SwingDoor : Interactable
    {
        [SerializeField] private Transform[] _doorPivots;
        [SerializeField] private float[] _directionMultipliers;
        [SerializeField, Range(45f, 125f)] private float _openAngle = 100f;
        [SerializeField, Min(0.05f)] private float _openDuration = 0.55f;

        private Quaternion[] _closedRotations;
        private Coroutine _animation;
        private bool _targetOpen;
        private float _swingDirection = 1f;

        public bool IsOpen => _targetOpen;

        private void Awake()
        {
            CacheClosedRotations();
        }

        public override bool CanInteract(GameObject interactor)
        {
            return enabled && _doorPivots != null && _doorPivots.Length > 0;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            _targetOpen = !_targetOpen;

            // Swing away from whichever side the player is standing on.
            if (_targetOpen && interactor != null)
            {
                float side = Vector3.Dot(transform.forward, interactor.transform.position - transform.position);
                _swingDirection = side >= 0f ? 1f : -1f;
            }

            if (_animation != null)
                StopCoroutine(_animation);

            _animation = StartCoroutine(AnimateToTarget());
        }

        private IEnumerator AnimateToTarget()
        {
            if (_closedRotations == null || _closedRotations.Length != _doorPivots.Length)
                CacheClosedRotations();

            var starts = new Quaternion[_doorPivots.Length];
            var targets = new Quaternion[_doorPivots.Length];

            for (int i = 0; i < _doorPivots.Length; i++)
            {
                if (_doorPivots[i] == null)
                    continue;

                starts[i] = _doorPivots[i].localRotation;
                float multiplier = _directionMultipliers != null && i < _directionMultipliers.Length
                    ? _directionMultipliers[i]
                    : 1f;

                targets[i] = _targetOpen
                    ? _closedRotations[i] * Quaternion.Euler(0f, _openAngle * _swingDirection * multiplier, 0f)
                    : _closedRotations[i];
            }

            float elapsed = 0f;
            while (elapsed < _openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _openDuration);
                t = t * t * (3f - 2f * t);

                for (int i = 0; i < _doorPivots.Length; i++)
                    if (_doorPivots[i] != null)
                        _doorPivots[i].localRotation = Quaternion.Slerp(starts[i], targets[i], t);

                yield return null;
            }

            for (int i = 0; i < _doorPivots.Length; i++)
                if (_doorPivots[i] != null)
                    _doorPivots[i].localRotation = targets[i];

            _animation = null;
        }

        private void CacheClosedRotations()
        {
            if (_doorPivots == null)
            {
                _closedRotations = System.Array.Empty<Quaternion>();
                return;
            }

            _closedRotations = new Quaternion[_doorPivots.Length];
            for (int i = 0; i < _doorPivots.Length; i++)
                _closedRotations[i] = _doorPivots[i] != null
                    ? _doorPivots[i].localRotation
                    : Quaternion.identity;
        }

        private void OnDisable()
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }
        }
    }
}
