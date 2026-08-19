using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Core.Interactables
{
    /// <summary>Distributes reachable UV-cell pickups across the outdoor campus NavMesh.</summary>
    [DisallowMultipleComponent]
    public sealed class AmmoPickupSpawner : MonoBehaviour
    {
        [SerializeField, Range(1, 24)] private int _pickupCount = 10;
        [SerializeField, Min(1)] private int _ammoPerPickup = 12;
        [SerializeField, Min(0)] private int _batteryPerPickup = 35;
        [SerializeField] private AudioClip _pickupSound;
        [SerializeField, Min(0f)] private float _minimumPlayerDistance = 18f;
        [SerializeField, Min(0f)] private float _minimumPickupSpacing = 22f;
        [SerializeField, Range(10, 1000)] private int _attemptsPerPickup = 250;
        [SerializeField] private Vector2 _allowedNavMeshHeight = new Vector2(-0.5f, 1.5f);
        [SerializeField] private int _placementSeed = 20260816;

        private readonly List<int> _triangleStarts = new List<int>();
        private readonly List<float> _cumulativeAreas = new List<float>();
        private readonly List<Vector3> _positions = new List<Vector3>();
        private NavMeshTriangulation _triangulation;
        private System.Random _random;
        private float _totalArea;
        private Transform _player;

        public int SpawnedCount { get; private set; }

        private IEnumerator Start()
        {
            yield return null;

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogError("[AmmoPickupSpawner] No Player-tagged object was found.", this);
                yield break;
            }

            _player = playerObject.transform;
            _random = new System.Random(_placementSeed);
            BuildGroundTriangleTable();
            if (_triangleStarts.Count == 0)
            {
                Debug.LogError("[AmmoPickupSpawner] No ground-level NavMesh triangles were available.", this);
                yield break;
            }

            if (!NavMesh.SamplePosition(_player.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
            {
                Debug.LogError("[AmmoPickupSpawner] Player is not near the baked NavMesh.", this);
                yield break;
            }

            Transform container = new GameObject("Spawned_UV_Ammo").transform;
            container.SetParent(transform, false);

            for (int index = 0; index < _pickupCount; index++)
            {
                if (!TryChoosePosition(playerHit.position, out Vector3 position))
                    continue;

                GameObject pickupObject = new GameObject($"UV_Ammo_{SpawnedCount + 1:00}");
                pickupObject.transform.SetParent(container, true);
                pickupObject.transform.position = position + Vector3.up * 1.15f;
                FlashlightAmmoPickup pickup = pickupObject.AddComponent<FlashlightAmmoPickup>();
                pickup.Configure(_ammoPerPickup, _batteryPerPickup, _pickupSound);
                _positions.Add(position);
                SpawnedCount++;
            }

            if (SpawnedCount < _pickupCount)
                Debug.LogWarning($"[AmmoPickupSpawner] Placed {SpawnedCount}/{_pickupCount} reachable pickups.", this);
        }

        private void BuildGroundTriangleTable()
        {
            _triangulation = NavMesh.CalculateTriangulation();
            _triangleStarts.Clear();
            _cumulativeAreas.Clear();
            _totalArea = 0f;

            float minimumY = Mathf.Min(_allowedNavMeshHeight.x, _allowedNavMeshHeight.y);
            float maximumY = Mathf.Max(_allowedNavMeshHeight.x, _allowedNavMeshHeight.y);
            for (int index = 0; index <= _triangulation.indices.Length - 3; index += 3)
            {
                Vector3 a = _triangulation.vertices[_triangulation.indices[index]];
                Vector3 b = _triangulation.vertices[_triangulation.indices[index + 1]];
                Vector3 c = _triangulation.vertices[_triangulation.indices[index + 2]];
                if (a.y < minimumY || a.y > maximumY || b.y < minimumY || b.y > maximumY ||
                    c.y < minimumY || c.y > maximumY)
                    continue;

                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (area <= 0.001f)
                    continue;

                _totalArea += area;
                _triangleStarts.Add(index);
                _cumulativeAreas.Add(_totalArea);
            }
        }

        private bool TryChoosePosition(Vector3 playerNavMeshPosition, out Vector3 position)
        {
            position = default;
            for (int attempt = 0; attempt < _attemptsPerPickup; attempt++)
            {
                Vector3 candidate = RandomPointOnGroundNavMesh();
                if (PlanarDistance(candidate, _player.position) < _minimumPlayerDistance)
                    continue;

                bool tooClose = false;
                foreach (Vector3 existing in _positions)
                {
                    if (PlanarDistance(candidate, existing) >= _minimumPickupSpacing)
                        continue;
                    tooClose = true;
                    break;
                }

                if (tooClose)
                    continue;

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(candidate, playerNavMeshPosition, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    continue;

                position = candidate;
                return true;
            }

            return false;
        }

        private Vector3 RandomPointOnGroundNavMesh()
        {
            float selection = (float)_random.NextDouble() * _totalArea;
            int tableIndex = _cumulativeAreas.BinarySearch(selection);
            if (tableIndex < 0)
                tableIndex = ~tableIndex;
            tableIndex = Mathf.Clamp(tableIndex, 0, _triangleStarts.Count - 1);

            int triangleStart = _triangleStarts[tableIndex];
            Vector3 a = _triangulation.vertices[_triangulation.indices[triangleStart]];
            Vector3 b = _triangulation.vertices[_triangulation.indices[triangleStart + 1]];
            Vector3 c = _triangulation.vertices[_triangulation.indices[triangleStart + 2]];
            float root = Mathf.Sqrt((float)_random.NextDouble());
            float second = (float)_random.NextDouble();
            return (1f - root) * a + root * (1f - second) * b + root * second * c;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
