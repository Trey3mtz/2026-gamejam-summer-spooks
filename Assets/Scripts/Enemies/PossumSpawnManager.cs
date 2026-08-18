using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Enemies
{
    /// <summary>
    /// Places a fresh group of possums on reachable, ground-level NavMesh
    /// triangles each time the map starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PossumSpawnManager : MonoBehaviour
    {
        [SerializeField] private GameObject _possumPrefab;
        [SerializeField, Range(1, 30)] private int _spawnCount = 28;
        [SerializeField, Min(0f)] private float _minimumPlayerDistance = 60f;
        [SerializeField, Min(0f)] private float _minimumPossumSpacing = 6f;
        [SerializeField, Range(1, 1000)] private int _attemptsPerPossum = 200;
        [Tooltip("Keeps map mobs outdoors/on the campus ground rather than on upper floors.")]
        [SerializeField] private Vector2 _allowedNavMeshHeight = new Vector2(-0.5f, 1.5f);

        private readonly List<int> _triangleStarts = new List<int>();
        private readonly List<float> _cumulativeAreas = new List<float>();
        private readonly List<Vector3> _spawnedPositions = new List<Vector3>();
        private readonly List<Bounds> _buildingZones = new List<Bounds>();
        private NavMeshTriangulation _triangulation;
        private float _totalArea;
        private Transform _player;

        public int RequestedSpawnCount => _spawnCount;
        public GameObject PossumPrefab => _possumPrefab;
        public int SpawnedCount { get; private set; }
        public bool FinishedSpawning { get; private set; }

        private IEnumerator Start()
        {
            // Let the baked navigation data and player finish registering.
            yield return null;

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogError("[PossumSpawnManager] No GameObject tagged Player was found.", this);
                FinishedSpawning = true;
                yield break;
            }

            if (_possumPrefab == null)
            {
                Debug.LogError("[PossumSpawnManager] Possum prefab is not assigned.", this);
                FinishedSpawning = true;
                yield break;
            }

            _player = playerObject.transform;
            BuildGroundTriangleTable();
            if (_triangleStarts.Count == 0)
            {
                Debug.LogError("[PossumSpawnManager] No ground-level NavMesh triangles were found.", this);
                FinishedSpawning = true;
                yield break;
            }

            var container = new GameObject("Spawned_Possums").transform;
            container.SetParent(transform, false);
            _buildingZones.Clear();
            _buildingZones.AddRange(PossumTerritory.FindCampusBuildingZones());

            if (!NavMesh.SamplePosition(_player.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
            {
                Debug.LogError("[PossumSpawnManager] Player is not near the baked NavMesh.", this);
                FinishedSpawning = true;
                yield break;
            }

            for (int i = 0; i < _spawnCount; i++)
            {
                if (!TryChooseSpawnPosition(playerHit.position, out Vector3 spawnPosition))
                    continue;

                GameObject possum = Instantiate(_possumPrefab, spawnPosition, Quaternion.identity, container);
                possum.name = $"Possum_{SpawnedCount + 1:00}";
                PossumTerritory territory = possum.GetComponent<PossumTerritory>();
                if (territory == null)
                    territory = possum.AddComponent<PossumTerritory>();
                territory.ConfigureExterior(_buildingZones);
                _spawnedPositions.Add(spawnPosition);
                SpawnedCount++;
            }

            FinishedSpawning = true;
            if (SpawnedCount < _spawnCount)
            {
                Debug.LogWarning($"[PossumSpawnManager] Spawned {SpawnedCount}/{_spawnCount} possums. " +
                                 "The map did not provide enough separated reachable positions.", this);
            }
        }

        private void BuildGroundTriangleTable()
        {
            _triangulation = NavMesh.CalculateTriangulation();
            _triangleStarts.Clear();
            _cumulativeAreas.Clear();
            _totalArea = 0f;

            float minimumY = Mathf.Min(_allowedNavMeshHeight.x, _allowedNavMeshHeight.y);
            float maximumY = Mathf.Max(_allowedNavMeshHeight.x, _allowedNavMeshHeight.y);

            for (int i = 0; i <= _triangulation.indices.Length - 3; i += 3)
            {
                Vector3 a = _triangulation.vertices[_triangulation.indices[i]];
                Vector3 b = _triangulation.vertices[_triangulation.indices[i + 1]];
                Vector3 c = _triangulation.vertices[_triangulation.indices[i + 2]];
                if (a.y < minimumY || a.y > maximumY ||
                    b.y < minimumY || b.y > maximumY ||
                    c.y < minimumY || c.y > maximumY)
                    continue;

                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (area <= 0.001f)
                    continue;

                _totalArea += area;
                _triangleStarts.Add(i);
                _cumulativeAreas.Add(_totalArea);
            }
        }

        private bool TryChooseSpawnPosition(Vector3 playerNavMeshPosition, out Vector3 position)
        {
            position = default;

            for (int attempt = 0; attempt < _attemptsPerPossum; attempt++)
            {
                Vector3 candidate = RandomPointOnGroundNavMesh();
                if (PlanarDistance(candidate, _player.position) < _minimumPlayerDistance)
                    continue;

                bool insideBuilding = false;
                foreach (Bounds zone in _buildingZones)
                {
                    Vector3 extents = zone.extents + new Vector3(0.55f, 0f, 0.55f);
                    if (Mathf.Abs(candidate.x - zone.center.x) <= extents.x &&
                        Mathf.Abs(candidate.z - zone.center.z) <= extents.z)
                    {
                        insideBuilding = true;
                        break;
                    }
                }
                if (insideBuilding)
                    continue;

                bool tooCloseToAnother = false;
                foreach (Vector3 existing in _spawnedPositions)
                {
                    if (PlanarDistance(candidate, existing) >= _minimumPossumSpacing)
                        continue;

                    tooCloseToAnother = true;
                    break;
                }

                if (tooCloseToAnother)
                    continue;

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(candidate, playerNavMeshPosition,
                        NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;

                position = candidate;
                return true;
            }

            return false;
        }

        private Vector3 RandomPointOnGroundNavMesh()
        {
            float areaChoice = Random.value * _totalArea;
            int tableIndex = _cumulativeAreas.BinarySearch(areaChoice);
            if (tableIndex < 0)
                tableIndex = ~tableIndex;
            tableIndex = Mathf.Clamp(tableIndex, 0, _triangleStarts.Count - 1);

            int triangleStart = _triangleStarts[tableIndex];
            Vector3 a = _triangulation.vertices[_triangulation.indices[triangleStart]];
            Vector3 b = _triangulation.vertices[_triangulation.indices[triangleStart + 1]];
            Vector3 c = _triangulation.vertices[_triangulation.indices[triangleStart + 2]];

            float root = Mathf.Sqrt(Random.value);
            float second = Random.value;
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
