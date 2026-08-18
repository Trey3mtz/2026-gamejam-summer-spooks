using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyGame.Gameplay
{
    /// <summary>
    /// Builds a harmless campus-prank Easter egg: glossy water marks and an
    /// unmistakably labeled water bottle. All geometry is decorative and has
    /// no colliders, so it cannot obstruct the player or navigation agents.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerritoryMarkingEasterEgg : MonoBehaviour
    {
        private Material _wetMaterial;
        private Material _bottleMaterial;
        private Material _capMaterial;
        private Material _labelMaterial;

        public void Build(int groundWallMask)
        {
            BuildMaterials();
            if (TryFindNearbyWall(groundWallMask, out RaycastHit wallHit))
            {
                Vector3 towardWall = wallHit.point - transform.position;
                towardWall.y = 0f;
                if (towardWall.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(
                        Vector3.Cross(Vector3.up, towardWall.normalized), Vector3.up);
                BuildFloorMarks();
                BuildBottle();
                BuildWallMarks(wallHit);
                return;
            }

            BuildFloorMarks();
            BuildBottle();
        }

        private void BuildMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            _wetMaterial = CreateMaterial(shader, "Suspiciously_Glossy_Water",
                new Color(0.12f, 0.24f, 0.28f, 0.42f), 0.96f, 0f,
                Color.black, true);
            _bottleMaterial = CreateMaterial(shader, "Clear_Blue_Water_Bottle",
                new Color(0.10f, 0.46f, 0.62f, 1f), 0.82f, 0.05f,
                new Color(0.01f, 0.075f, 0.10f), false);
            _capMaterial = CreateMaterial(shader, "Water_Bottle_Cap",
                new Color(0.035f, 0.12f, 0.18f, 1f), 0.45f, 0f, Color.black, false);
            _labelMaterial = CreateMaterial(shader, "Water_Bottle_Label",
                new Color(0.86f, 0.91f, 0.88f, 1f), 0.15f, 0f, Color.black, false);
        }

        private static Material CreateMaterial(Shader shader, string materialName, Color color,
            float smoothness, float metallic, Color emission, bool transparent)
        {
            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_Surface"))
                    material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend"))
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend"))
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite"))
                    material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            if (emission.maxColorComponent > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        private void BuildFloorMarks()
        {
            Transform puddles = new GameObject("Wet_Marks_On_Floor").transform;
            puddles.SetParent(transform, false);

            CreateMark("Main_Wet_Patch", puddles, new Vector3(-0.12f, 0.018f, 0.04f),
                new Vector3(0.72f, 0.012f, 0.44f), 14f);
            CreateMark("Wet_Patch_02", puddles, new Vector3(0.38f, 0.017f, 0.17f),
                new Vector3(0.38f, 0.01f, 0.25f), -20f);
            CreateMark("Wet_Patch_03", puddles, new Vector3(-0.46f, 0.016f, -0.18f),
                new Vector3(0.29f, 0.009f, 0.18f), 31f);

            for (int index = 0; index < 5; index++)
            {
                float distance = 0.55f + index * 0.20f;
                CreateMark($"Water_Trail_{index + 1:00}", puddles,
                    new Vector3(-distance, 0.015f, -0.15f + Mathf.Sin(index * 1.8f) * 0.10f),
                    new Vector3(0.12f + index * 0.012f, 0.008f, 0.075f), index * 23f);
            }
        }

        private void BuildBottle()
        {
            Transform bottle = new GameObject("Definitely_Just_A_Water_Bottle").transform;
            bottle.SetParent(transform, false);
            bottle.localPosition = new Vector3(0.58f, 0.14f, -0.37f);
            bottle.localRotation = Quaternion.Euler(0f, 24f, 0f);

            CreatePrimitive("Bottle_Body", PrimitiveType.Cylinder, bottle,
                Vector3.zero, new Vector3(0.115f, 0.34f, 0.115f),
                Quaternion.Euler(0f, 0f, 90f), _bottleMaterial);
            CreatePrimitive("Bottle_Neck", PrimitiveType.Cylinder, bottle,
                new Vector3(0.38f, 0f, 0f), new Vector3(0.075f, 0.095f, 0.075f),
                Quaternion.Euler(0f, 0f, 90f), _bottleMaterial);
            CreatePrimitive("Bottle_Cap", PrimitiveType.Cylinder, bottle,
                new Vector3(0.49f, 0f, 0f), new Vector3(0.086f, 0.035f, 0.086f),
                Quaternion.Euler(0f, 0f, 90f), _capMaterial);
            CreatePrimitive("Bottle_Label", PrimitiveType.Cube, bottle,
                new Vector3(-0.02f, 0.119f, 0f), new Vector3(0.27f, 0.012f, 0.17f),
                Quaternion.identity, _labelMaterial);

            var label = new GameObject("100_PERCENT_WATER_Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(bottle, false);
            label.transform.localPosition = new Vector3(-0.02f, 0.133f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.transform.localScale = Vector3.one * 0.035f;
            label.text = "100% WATER";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 2.6f;
            label.color = new Color(0.02f, 0.12f, 0.16f, 1f);
            label.enableWordWrapping = false;
            label.rectTransform.sizeDelta = new Vector2(8.5f, 1.5f);
        }

        private void BuildWallMarks(RaycastHit wallHit)
        {
            Transform marks = new GameObject("Wet_Territory_Marks_On_Wall").transform;
            marks.SetParent(transform, true);
            marks.position = wallHit.point + wallHit.normal * 0.025f - Vector3.up * 0.58f;
            marks.rotation = Quaternion.LookRotation(wallHit.normal, Vector3.up);

            CreatePrimitive("Wall_Splash", PrimitiveType.Sphere, marks,
                new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 0.34f, 0.012f),
                Quaternion.Euler(0f, 0f, 8f), _wetMaterial);
            CreatePrimitive("Wall_Streak_01", PrimitiveType.Sphere, marks,
                new Vector3(-0.11f, 0.16f, 0f), new Vector3(0.055f, 0.32f, 0.009f),
                Quaternion.Euler(0f, 0f, -6f), _wetMaterial);
            CreatePrimitive("Wall_Streak_02", PrimitiveType.Sphere, marks,
                new Vector3(0.09f, 0.11f, 0f), new Vector3(0.045f, 0.27f, 0.009f),
                Quaternion.Euler(0f, 0f, 5f), _wetMaterial);
            CreatePrimitive("Wall_Drip", PrimitiveType.Sphere, marks,
                new Vector3(0.17f, -0.06f, 0f), new Vector3(0.035f, 0.12f, 0.008f),
                Quaternion.identity, _wetMaterial);
        }

        private bool TryFindNearbyWall(int mask, out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance = float.MaxValue;
            Vector3 origin = transform.position + Vector3.up * 0.72f;
            Vector3[] directions =
            {
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, 1f).normalized,
                new Vector3(1f, 0f, -1f).normalized, new Vector3(-1f, 0f, -1f).normalized
            };

            foreach (Vector3 direction in directions)
            {
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, 4f, mask,
                        QueryTriggerInteraction.Ignore) || hit.distance >= nearestDistance ||
                    Mathf.Abs(hit.normal.y) > 0.25f)
                    continue;

                nearestDistance = hit.distance;
                nearestHit = hit;
            }
            return nearestDistance < float.MaxValue;
        }

        private void CreateMark(string objectName, Transform parent, Vector3 localPosition,
            Vector3 localScale, float yaw)
        {
            CreatePrimitive(objectName, PrimitiveType.Sphere, parent, localPosition, localScale,
                Quaternion.Euler(0f, yaw, 0f), _wetMaterial);
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType primitive,
            Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation,
            Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(primitive);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;

            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return instance;
        }
    }
}
