using System;
using System.Collections;
using System.Collections.Generic;
using SpookyGame.Enemies;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SpookyGame.Gameplay
{
    /// <summary>
    /// Chooses a different playable room in every regular campus building, places
    /// one school-supply objective there, and populates selected interiors with
    /// territory-bound possums.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusObjectiveSpawner : MonoBehaviour
    {
        private sealed class BuildingSpec
        {
            public readonly string ObjectName;
            public readonly string DisplayName;

            public BuildingSpec(string objectName, string displayName)
            {
                ObjectName = objectName;
                DisplayName = displayName;
            }
        }

        private static readonly BuildingSpec[] Buildings =
        {
            new BuildingSpec("B10_Learning_Center", "Learning Center"),
            new BuildingSpec("B13_Fieldhouse", "Fieldhouse"),
            new BuildingSpec("B14_Engineering_Building", "Engineering Building"),
            new BuildingSpec("B15_Health_Physical_Education_II", "Health & Physical Education II"),
            new BuildingSpec("B16_Academic_Services", "Academic Services"),
            new BuildingSpec("B27_Health_PE_Complex", "Health & PE Complex")
        };

        private static readonly BuildingSpec BossBuilding = new BuildingSpec(
            "B11_B12_Science_Planetarium_Complex",
            "Science & Planetarium Complex");

        private static readonly string[] AllBuildingObjectNames =
        {
            "B10_Learning_Center",
            "B11_B12_Science_Planetarium_Complex",
            "B13_Fieldhouse",
            "B14_Engineering_Building",
            "B15_Health_Physical_Education_II",
            "B16_Academic_Services",
            "B27_Health_PE_Complex"
        };

        private static readonly string[] ItemNames =
        {
            "Spiral Notebook",
            "Graphing Calculator",
            "Student ID Card",
            "Laboratory Goggles",
            "Pencil Case",
            "Campus Textbook"
        };

        private static readonly CollegeSupplyType[] ItemTypes =
        {
            CollegeSupplyType.Notebook,
            CollegeSupplyType.Calculator,
            CollegeSupplyType.StudentId,
            CollegeSupplyType.LabGoggles,
            CollegeSupplyType.PencilCase,
            CollegeSupplyType.Textbook
        };

        [SerializeField, Range(1, 7)] private int _buildingsWithIndoorPossums = 7;
        [SerializeField, Range(1, 4)] private int _possumsPerSelectedBuilding = 4;
        [SerializeField, Range(5, 100)] private int _placementAttempts = 40;

        private readonly List<int> _buildingOrder = new List<int>();
        private int _groundWallMask;

        private IEnumerator Start()
        {
            yield return null;

            GameRunDirector director = GameRunDirector.Instance;
            PossumSpawnManager outdoorSpawnManager = GetComponent<PossumSpawnManager>();
            if (director == null || outdoorSpawnManager == null || outdoorSpawnManager.PossumPrefab == null)
            {
                Debug.LogError("[CampusObjectiveSpawner] Required run director or possum prefab is missing.", this);
                yield break;
            }

            GameObject campus = GameObject.Find("UTRGV_Campus_Blockout");
            Transform buildingsRoot = campus != null ? campus.transform.Find("Buildings") : null;
            if (buildingsRoot == null)
            {
                Debug.LogError("[CampusObjectiveSpawner] UTRGV_Campus_Blockout/Buildings was not found.", this);
                yield break;
            }

            _groundWallMask = LayerMask.GetMask("GroundWall");
            ShuffleItemOrder();

            Transform objectiveContainer = new GameObject("Spawned_Campus_Objectives").transform;
            objectiveContainer.SetParent(transform, false);
            var validBuildings = new List<(BuildingSpec spec, Transform root, Bounds bounds, List<Transform> rooms)>();

            for (int index = 0; index < Buildings.Length; index++)
            {
                BuildingSpec spec = Buildings[index];
                Transform building = buildingsRoot.Find(spec.ObjectName);
                if (building == null || !TryGetInteriorLayout(building, out Bounds buildingBounds, out List<Transform> rooms))
                {
                    Debug.LogWarning($"[CampusObjectiveSpawner] Playable rooms were not found in {spec.ObjectName}.", this);
                    continue;
                }

                // A building remains valid for indoor enemies even if an objective
                // placement needs a fallback. Previously, one failed item roll also
                // removed that entire building (including HPC) from possum spawning.
                validBuildings.Add((spec, building, buildingBounds, rooms));

                if (!TryFindPlacementInAnyRoom(rooms, buildingBounds, 0.4f,
                        out Transform room, out Vector3 position))
                {
                    Debug.LogWarning($"[CampusObjectiveSpawner] No clear objective position in {spec.ObjectName}.", this);
                    continue;
                }

                int itemIndex = _buildingOrder[index];
                string objectiveId = $"campus-supply-{index + 1:00}";
                director.RegisterObjective(objectiveId, ItemNames[itemIndex], spec.DisplayName);
                GameObject itemObject = new GameObject($"Objective_{spec.ObjectName}_{room.name}");
                itemObject.transform.SetParent(objectiveContainer, true);
                itemObject.transform.position = position + Vector3.up * 0.9f;
                CollegeCollectible collectible = itemObject.AddComponent<CollegeCollectible>();
                collectible.Configure(objectiveId, ItemNames[itemIndex], spec.DisplayName,
                    ItemTypes[itemIndex], false);
            }

            AddBossBuildingForPossums(buildingsRoot, validBuildings);

            director.RegisterObjective("llorona-research-drive", "Stolen Research Drive",
                "Science & Planetarium Complex — Second Floor");
            SpawnTerritoryMarkingEasterEgg(validBuildings, objectiveContainer);
            SpawnInteriorPossums(validBuildings, outdoorSpawnManager.PossumPrefab);
        }

        private void AddBossBuildingForPossums(
            Transform buildingsRoot,
            List<(BuildingSpec spec, Transform root, Bounds bounds, List<Transform> rooms)> buildings)
        {
            Transform building = buildingsRoot.Find(BossBuilding.ObjectName);
            if (building == null ||
                !TryGetInteriorLayout(building, out Bounds buildingBounds, out List<Transform> rooms))
            {
                Debug.LogWarning("[CampusObjectiveSpawner] Playable first-floor rooms were not found in " +
                                 $"{BossBuilding.ObjectName}; no indoor possums were added there.", this);
                return;
            }

            buildings.Add((BossBuilding, building, buildingBounds, rooms));
        }

        private void SpawnTerritoryMarkingEasterEgg(
            List<(BuildingSpec spec, Transform root, Bounds bounds, List<Transform> rooms)> buildings,
            Transform parent)
        {
            if (buildings.Count == 0)
                return;

            int buildingIndex = buildings.FindIndex(entry =>
                entry.spec.ObjectName == "B14_Engineering_Building");
            var building = buildings[buildingIndex >= 0 ? buildingIndex : 0];
            if (TryFindExteriorPlacement(building.root, building.bounds, out Vector3 position))
            {
                CreateTerritoryMarkingEasterEgg(position, parent);
                return;
            }

            Debug.LogWarning("[CampusObjectiveSpawner] No exterior location was found for the territory-marking Easter egg.", this);
        }

        private bool TryFindExteriorPlacement(Transform buildingRoot, Bounds buildingBounds, out Vector3 position)
        {
            position = default;
            var entrances = new List<Transform>();
            foreach (Transform child in buildingRoot.GetComponentsInChildren<Transform>(true))
            {
                string objectName = child.name;
                if (objectName.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf("entrance", StringComparison.OrdinalIgnoreCase) >= 0)
                    entrances.Add(child);
            }

            Vector3[] directions =
            {
                Vector3.forward, Vector3.right, Vector3.back, Vector3.left,
                new Vector3(1f, 0f, 1f).normalized, new Vector3(1f, 0f, -1f).normalized,
                new Vector3(-1f, 0f, -1f).normalized, new Vector3(-1f, 0f, 1f).normalized
            };

            float bestDoorClearance = -1f;
            foreach (Vector3 direction in directions)
            {
                Vector3 candidate = buildingBounds.center;
                candidate.x += direction.x * (buildingBounds.extents.x + 2.4f);
                candidate.z += direction.z * (buildingBounds.extents.z + 2.4f);
                candidate.y = buildingBounds.min.y + 0.25f;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.25f, NavMesh.AllAreas) ||
                    ContainsPlanar(buildingBounds, hit.position, 0.6f))
                    continue;

                float nearestEntrance = float.MaxValue;
                foreach (Transform entrance in entrances)
                {
                    Vector3 difference = entrance.position - hit.position;
                    difference.y = 0f;
                    nearestEntrance = Mathf.Min(nearestEntrance, difference.magnitude);
                }

                // Pick the valid exterior wall point farthest from every entrance.
                // With no named entrances, prefer a back corner instead of a doorway-facing side.
                if (entrances.Count == 0)
                    nearestEntrance = direction == Vector3.back ? 1f : 0f;
                if (nearestEntrance <= bestDoorClearance)
                    continue;

                bestDoorClearance = nearestEntrance;
                position = hit.position;
            }
            return bestDoorClearance >= 0f;
        }

        private void CreateTerritoryMarkingEasterEgg(Vector3 position, Transform parent)
        {
            GameObject easterEgg = new GameObject("EasterEgg_Territory_Marking");
            easterEgg.transform.SetParent(parent, true);
            easterEgg.transform.position = position + Vector3.up * 0.01f;
            easterEgg.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            easterEgg.AddComponent<TerritoryMarkingEasterEgg>().Build(_groundWallMask);
        }

        private void SpawnInteriorPossums(
            List<(BuildingSpec spec, Transform root, Bounds bounds, List<Transform> rooms)> buildings,
            GameObject possumPrefab)
        {
            if (buildings.Count == 0)
                return;

            for (int i = buildings.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (buildings[i], buildings[swap]) = (buildings[swap], buildings[i]);
            }

            Transform container = new GameObject("Interior_Possums").transform;
            container.SetParent(transform, false);
            int buildingCount = Mathf.Min(_buildingsWithIndoorPossums, buildings.Count);
            int totalSpawned = 0;
            for (int buildingIndex = 0; buildingIndex < buildingCount; buildingIndex++)
            {
                var entry = buildings[buildingIndex];
                int spawned = 0;
                for (int attempt = 0; attempt < _placementAttempts && spawned < _possumsPerSelectedBuilding; attempt++)
                {
                    Transform room = entry.rooms[Random.Range(0, entry.rooms.Count)];
                    if (!TryFindPlacement(room, entry.bounds, 0.52f, out Vector3 position))
                        continue;

                    GameObject possum = Instantiate(possumPrefab, position, Quaternion.identity, container);
                    possum.name = $"Interior_Possum_{entry.spec.ObjectName}_{spawned + 1:00}";
                    PossumTerritory territory = possum.GetComponent<PossumTerritory>();
                    if (territory == null)
                        territory = possum.AddComponent<PossumTerritory>();
                    territory.ConfigureInterior(entry.bounds);
                    spawned++;
                    totalSpawned++;
                }

                // Deterministic room-center fallback: every audited building gets
                // its indoor enemies once the interior NavMesh is baked.
                for (int roomIndex = 0;
                     roomIndex < entry.rooms.Count && spawned < _possumsPerSelectedBuilding;
                     roomIndex++)
                {
                    if (!TryFindPlacementNearRoomCenter(entry.rooms[roomIndex], entry.bounds,
                            out Vector3 fallbackPosition))
                        continue;

                    GameObject possum = Instantiate(possumPrefab, fallbackPosition,
                        Quaternion.identity, container);
                    possum.name = $"Interior_Possum_{entry.spec.ObjectName}_{spawned + 1:00}";
                    PossumTerritory territory = possum.GetComponent<PossumTerritory>();
                    if (territory == null)
                        territory = possum.AddComponent<PossumTerritory>();
                    territory.ConfigureInterior(entry.bounds);
                    spawned++;
                    totalSpawned++;
                }

                if (spawned < _possumsPerSelectedBuilding)
                {
                    Debug.LogWarning($"[CampusObjectiveSpawner] Spawned only {spawned}/" +
                                     $"{_possumsPerSelectedBuilding} indoor possums in " +
                                     $"{entry.spec.ObjectName}.", this);
                }
                else
                {
                    Debug.Log($"[CampusObjectiveSpawner] {entry.spec.ObjectName}: " +
                              $"{spawned} confined indoor possums ready.", this);
                }
            }

            Debug.Log($"[CampusObjectiveSpawner] Spawned {totalSpawned} indoor possums across " +
                      $"{buildingCount}/{buildings.Count} playable building interiors.", this);
        }

        private bool TryGetInteriorLayout(Transform building, out Bounds buildingBounds,
            out List<Transform> rooms)
        {
            rooms = new List<Transform>();
            buildingBounds = default;
            Transform playable = building.Find("Playable_Interior");
            Transform architecture = playable != null ? playable.Find("01_Architecture") : null;
            Transform roomRoot = playable != null ? playable.Find("02_Rooms_And_Furniture") : null;

            // The hand-authored Science/Planetarium complex uses numbered roots
            // directly under the building. Its room root contains only the first
            // floor, keeping possums out of La Llorona's upstairs boss arena.
            if (architecture == null || roomRoot == null)
            {
                architecture = building.Find("02_Interior_Architecture");
                roomRoot = building.Find("03_Rooms_And_Furniture");
            }

            if (architecture == null || roomRoot == null ||
                !PossumTerritory.TryCalculateBounds(architecture, out buildingBounds))
                return false;

            foreach (Transform room in roomRoot)
            {
                if (PossumTerritory.TryCalculateBounds(room, out _))
                    rooms.Add(room);
            }
            return rooms.Count > 0;
        }

        private bool TryFindPlacement(Transform room, Bounds buildingBounds, float clearance,
            out Vector3 position)
        {
            position = default;
            if (!PossumTerritory.TryCalculateBounds(room, out Bounds roomBounds))
                return false;

            float searchRadius = Mathf.Clamp(Mathf.Max(roomBounds.extents.x, roomBounds.extents.z) + 1.8f,
                2.5f, 6f);
            for (int attempt = 0; attempt < _placementAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * searchRadius;
                Vector3 candidate = new Vector3(roomBounds.center.x + offset.x,
                    buildingBounds.min.y + 0.25f, roomBounds.center.z + offset.y);
                if (!ContainsPlanar(buildingBounds, candidate, -0.8f))
                    continue;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.75f, NavMesh.AllAreas) ||
                    !ContainsPlanar(buildingBounds, hit.position, -0.75f))
                    continue;

                Vector3 clearanceCenter = hit.position + Vector3.up * 0.72f;
                if (_groundWallMask != 0 && Physics.CheckSphere(clearanceCenter, clearance,
                        _groundWallMask, QueryTriggerInteraction.Ignore))
                    continue;

                position = hit.position;
                return true;
            }

            return false;
        }

        private bool TryFindPlacementInAnyRoom(List<Transform> rooms, Bounds buildingBounds,
            float clearance, out Transform selectedRoom, out Vector3 position)
        {
            selectedRoom = null;
            position = default;
            if (rooms == null || rooms.Count == 0)
                return false;

            int start = Random.Range(0, rooms.Count);
            for (int offset = 0; offset < rooms.Count; offset++)
            {
                Transform room = rooms[(start + offset) % rooms.Count];
                if (!TryFindPlacement(room, buildingBounds, clearance, out position))
                    continue;

                selectedRoom = room;
                return true;
            }
            return false;
        }

        private bool TryFindPlacementNearRoomCenter(Transform room, Bounds buildingBounds,
            out Vector3 position)
        {
            position = default;
            if (!PossumTerritory.TryCalculateBounds(room, out Bounds roomBounds))
                return false;

            Vector3 center = roomBounds.center;
            center.y = buildingBounds.min.y + 0.25f;
            if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 4f, NavMesh.AllAreas) ||
                !ContainsPlanar(buildingBounds, hit.position, -0.6f))
                return false;

            position = hit.position;
            return true;
        }

        private void ShuffleItemOrder()
        {
            _buildingOrder.Clear();
            for (int i = 0; i < ItemNames.Length; i++)
                _buildingOrder.Add(i);
            for (int i = _buildingOrder.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (_buildingOrder[i], _buildingOrder[swap]) = (_buildingOrder[swap], _buildingOrder[i]);
            }
        }

        private static bool ContainsPlanar(Bounds bounds, Vector3 point, float padding)
        {
            Vector3 extents = bounds.extents + new Vector3(padding, 0f, padding);
            return Mathf.Abs(point.x - bounds.center.x) <= Mathf.Max(0.25f, extents.x) &&
                   Mathf.Abs(point.z - bounds.center.z) <= Mathf.Max(0.25f, extents.z);
        }
    }
}
