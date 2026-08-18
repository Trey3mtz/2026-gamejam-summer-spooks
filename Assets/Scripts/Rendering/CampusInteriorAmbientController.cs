using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyGame.Rendering
{
    /// <summary>
    /// Restores the existing building-only blackout behavior: the outdoor scene
    /// keeps its authored night lighting, while building interiors switch to
    /// zero ambient light and preserve the player's held flashlight.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusInteriorAmbientController : MonoBehaviour
    {
        private static readonly string[] BuildingNames =
        {
            "B10_Learning_Center",
            "B11_B12_Science_Planetarium_Complex",
            "B13_Fieldhouse",
            "B14_Engineering_Building",
            "B15_Health_Physical_Education_II",
            "B16_Academic_Services",
            "B27_Health_PE_Complex"
        };

        [SerializeField, Min(0f)] private float _edgeInset = 0.3f;

        private readonly List<Bounds> _buildingBounds = new List<Bounds>();
        private readonly Dictionary<Light, bool> _temporarilyDisabledLights =
            new Dictionary<Light, bool>();
        private Transform _player;
        private AmbientMode _outdoorAmbientMode;
        private Color _outdoorAmbientLight;
        private Color _outdoorAmbientSkyColor;
        private Color _outdoorAmbientEquatorColor;
        private Color _outdoorAmbientGroundColor;
        private float _outdoorAmbientIntensity;
        private float _outdoorReflectionIntensity;
        private bool _isInside;
        private float _nextLightAuditTime;

        private void Awake()
        {
            _outdoorAmbientMode = RenderSettings.ambientMode;
            _outdoorAmbientLight = RenderSettings.ambientLight;
            _outdoorAmbientSkyColor = RenderSettings.ambientSkyColor;
            _outdoorAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            _outdoorAmbientGroundColor = RenderSettings.ambientGroundColor;
            _outdoorAmbientIntensity = RenderSettings.ambientIntensity;
            _outdoorReflectionIntensity = RenderSettings.reflectionIntensity;
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            _player = playerObject != null ? playerObject.transform : null;
            CacheBuildingBounds();
            SuppressInstalledInteriorLights();
        }

        private void Update()
        {
            if (_player == null)
                return;

            bool inside = IsInsideBuilding(_player.position, true);

            if (inside != _isInside)
            {
                _isInside = inside;
                if (inside)
                    ApplyZeroLightInterior();
                else
                    RestoreOutdoorLighting();
            }

            if (Time.unscaledTime >= _nextLightAuditTime)
            {
                _nextLightAuditTime = Time.unscaledTime + 0.5f;
                SuppressInstalledInteriorLights();
                if (_isInside)
                    SuppressAllNonPlayerLights();
            }
        }

        private void ApplyZeroLightInterior()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientSkyColor = Color.black;
            RenderSettings.ambientEquatorColor = Color.black;
            RenderSettings.ambientGroundColor = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;
            SuppressAllNonPlayerLights();
            DynamicGI.UpdateEnvironment();
        }

        private void RestoreOutdoorLighting()
        {
            RenderSettings.ambientMode = _outdoorAmbientMode;
            RenderSettings.ambientLight = _outdoorAmbientLight;
            RenderSettings.ambientSkyColor = _outdoorAmbientSkyColor;
            RenderSettings.ambientEquatorColor = _outdoorAmbientEquatorColor;
            RenderSettings.ambientGroundColor = _outdoorAmbientGroundColor;
            RenderSettings.ambientIntensity = _outdoorAmbientIntensity;
            RenderSettings.reflectionIntensity = _outdoorReflectionIntensity;

            foreach (KeyValuePair<Light, bool> entry in _temporarilyDisabledLights)
            {
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            }
            _temporarilyDisabledLights.Clear();
            DynamicGI.UpdateEnvironment();
        }

        private void SuppressInstalledInteriorLights()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light lightComponent in lights)
            {
                if (lightComponent == null || IsPlayerLight(lightComponent) ||
                    lightComponent.type == LightType.Directional)
                    continue;

                if (IsInsideBuilding(lightComponent.transform.position, false))
                    lightComponent.enabled = false;
            }
        }

        private void SuppressAllNonPlayerLights()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light lightComponent in lights)
            {
                if (lightComponent == null || IsPlayerLight(lightComponent))
                    continue;

                if (!_temporarilyDisabledLights.ContainsKey(lightComponent))
                    _temporarilyDisabledLights.Add(lightComponent, lightComponent.enabled);
                lightComponent.enabled = false;
            }
        }

        private bool IsPlayerLight(Light lightComponent)
        {
            return _player != null && lightComponent.transform.IsChildOf(_player);
        }

        private bool IsInsideBuilding(Vector3 position, bool applyInset)
        {
            foreach (Bounds building in _buildingBounds)
            {
                Bounds interior = building;
                if (applyInset)
                    interior.Expand(new Vector3(-_edgeInset * 2f, 0f, -_edgeInset * 2f));

                if (position.x >= interior.min.x && position.x <= interior.max.x &&
                    position.z >= interior.min.z && position.z <= interior.max.z &&
                    position.y >= interior.min.y - 0.5f && position.y <= interior.max.y + 0.5f)
                    return true;
            }
            return false;
        }

        private void CacheBuildingBounds()
        {
            _buildingBounds.Clear();
            GameObject campus = GameObject.Find("UTRGV_Campus_Blockout");
            Transform buildings = campus != null ? campus.transform.Find("Buildings") : null;
            if (buildings == null)
                return;

            foreach (string buildingName in BuildingNames)
            {
                Transform building = buildings.Find(buildingName);
                if (building == null)
                    continue;

                Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    continue;

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i].name.Contains("Minimap") ||
                        renderers[i].GetComponentInParent<Canvas>() != null)
                        continue;
                    bounds.Encapsulate(renderers[i].bounds);
                }
                _buildingBounds.Add(bounds);
            }

            Debug.Log($"[CampusInteriorAmbientController] Zero-light interior blackout active for " +
                      $"{_buildingBounds.Count}/{BuildingNames.Length} buildings.", this);
        }

        private void OnDisable()
        {
            RestoreOutdoorLighting();
        }
    }
}
