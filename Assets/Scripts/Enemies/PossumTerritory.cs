using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SpookyGame.Enemies
{
    /// <summary>
    /// Restricts a possum to either one building or the outdoor campus. Paths are
    /// checked as well as current position so agents cannot route through doorways.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PossumTerritory : MonoBehaviour
    {
        private enum TerritoryKind { Exterior, Interior }

        private static readonly string[] CampusBuildingNames =
        {
            "B10_Learning_Center",
            "B11_B12_Science_Planetarium_Complex",
            "B13_Fieldhouse",
            "B14_Engineering_Building",
            "B15_Health_Physical_Education_II",
            "B16_Academic_Services",
            "B27_Health_PE_Complex"
        };

        private TerritoryKind _kind;
        private readonly List<Bounds> _buildingZones = new List<Bounds>();
        private Bounds _interiorZone;
        private Vector3 _lastValidPosition;
        private bool _configured;

        public void ConfigureExterior(IEnumerable<Bounds> buildingZones)
        {
            _kind = TerritoryKind.Exterior;
            _buildingZones.Clear();
            _buildingZones.AddRange(buildingZones);
            _lastValidPosition = transform.position;
            _configured = true;
        }

        public void ConfigureInterior(Bounds buildingZone)
        {
            _kind = TerritoryKind.Interior;
            _interiorZone = buildingZone;
            _lastValidPosition = transform.position;
            _configured = true;
        }

        public bool IsPositionAllowed(Vector3 position)
        {
            if (!_configured)
                return true;

            if (_kind == TerritoryKind.Interior)
                return ContainsPlanar(_interiorZone, position, -0.65f);

            foreach (Bounds zone in _buildingZones)
            {
                if (ContainsPlanar(zone, position, 0.55f))
                    return false;
            }
            return true;
        }

        public bool ValidateAgentPosition(NavMeshAgent agent)
        {
            if (!_configured || agent == null || !agent.isOnNavMesh)
                return true;

            if (IsPositionAllowed(transform.position))
            {
                _lastValidPosition = transform.position;
                return true;
            }

            agent.ResetPath();
            agent.Warp(_lastValidPosition);
            return false;
        }

        public bool IsPathAllowed(NavMeshPath path)
        {
            if (!_configured || path == null || path.corners == null || path.corners.Length == 0)
                return true;

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length; i++)
            {
                if (!IsPositionAllowed(corners[i]))
                    return false;

                if (i == 0)
                    continue;

                Vector3 from = corners[i - 1];
                Vector3 to = corners[i];
                float distance = Vector3.Distance(from, to);
                int steps = Mathf.CeilToInt(distance / 1.25f);
                for (int step = 1; step < steps; step++)
                {
                    if (!IsPositionAllowed(Vector3.Lerp(from, to, step / (float)steps)))
                        return false;
                }
            }

            return true;
        }

        public static List<Bounds> FindCampusBuildingZones()
        {
            var result = new List<Bounds>();
            GameObject campus = GameObject.Find("UTRGV_Campus_Blockout");
            Transform buildings = campus != null ? campus.transform.Find("Buildings") : null;
            if (buildings == null)
                return result;

            foreach (string buildingName in CampusBuildingNames)
            {
                Transform building = buildings.Find(buildingName);
                if (building != null && TryCalculateBounds(building, out Bounds bounds))
                    result.Add(bounds);
            }
            return result;
        }

        public static bool TryCalculateBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || renderer.name.IndexOf("Minimap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private static bool ContainsPlanar(Bounds bounds, Vector3 point, float padding)
        {
            Vector3 extents = bounds.extents + new Vector3(padding, 0f, padding);
            extents.x = Mathf.Max(0.25f, extents.x);
            extents.z = Mathf.Max(0.25f, extents.z);
            Vector3 delta = point - bounds.center;
            return Mathf.Abs(delta.x) <= extents.x && Mathf.Abs(delta.z) <= extents.z;
        }
    }
}
