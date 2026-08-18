using SpookyGame.Core;
using UnityEngine;

namespace SpookyGame.Gameplay
{
    /// <summary>Registers and drops La Llorona's required final campus objective.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class BossObjectiveDrop : MonoBehaviour
    {
        [SerializeField] private string _objectiveId = "llorona-research-drive";
        [SerializeField] private string _displayName = "Stolen Research Drive";
        [SerializeField] private string _location = "Science & Planetarium Complex — Second Floor";
        [SerializeField, Min(0f)] private float _dropHeight = 1f;

        private ActorHealth _health;
        private bool _dropped;

        private void Awake()
        {
            _health = GetComponent<ActorHealth>();
        }

        private void Start()
        {
            GameRunDirector.Instance?.RegisterObjective(_objectiveId, _displayName, _location);
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.Died += DropObjective;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Died -= DropObjective;
        }

        private void DropObjective()
        {
            if (_dropped)
                return;
            _dropped = true;

            GameObject drop = new GameObject("Objective_LaLlorona_Research_Drive");
            drop.transform.position = transform.position + Vector3.up * _dropHeight;
            CollegeCollectible collectible = drop.AddComponent<CollegeCollectible>();
            collectible.Configure(_objectiveId, _displayName, _location,
                CollegeSupplyType.ResearchDrive, false);
        }
    }
}
