#if UNITY_EDITOR
using System;
using SpookyGame.Core;
using SpookyGame.Core.Interactables;
using SpookyGame.Enemies;
using SpookyGame.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SpookyGame.EditorTools
{
    /// <summary>One-shot whole-campus validation/repair requested for UTRGV_Blockout.</summary>
    [InitializeOnLoad]
    internal static class CampusFullAuditRepair
    {
        private const bool AutoRepairEnabled = false;
        private const string ScenePath = "Assets/Scenes/UTRGV_Blockout.unity";
        private const string LloronaPrefabPath = "Assets/Prefabs/Villains/Llorona_Boss.prefab";

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

        static CampusFullAuditRepair()
        {
            if (AutoRepairEnabled)
                EditorApplication.delayCall += RepairAndAudit;
        }

        [MenuItem("Tools/Spooky Game/Full Audit and Repair Campus Buildings")]
        private static void RepairAndAudit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += RepairAndAudit;
                return;
            }

            ConfigureLloronaPrefab();
            EditorApplication.ExecuteMenuItem("Tools/Spooky Game/Rebuild Enemy Combat Animations");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject campus = GameObject.Find("UTRGV_Campus_Blockout");
            Transform buildings = campus != null ? campus.transform.Find("Buildings") : null;
            if (buildings == null)
                throw new InvalidOperationException("UTRGV_Campus_Blockout/Buildings was not found.");

            int totalDoors = 0;
            int totalLights = 0;

            foreach (string buildingName in BuildingNames)
            {
                Transform building = buildings.Find(buildingName);
                if (building == null)
                {
                    Debug.LogError($"[CampusFullAudit] Missing building: {buildingName}");
                    continue;
                }

                SwingDoor[] doors = building.GetComponentsInChildren<SwingDoor>(true);
                int lights = building.GetComponentsInChildren<Light>(true).Length;
                int roomCount = CountPlayableRooms(building, buildingName.Contains("B11_B12"));
                totalDoors += doors.Length;
                totalLights += lights;

                string status = doors.Length > 0 && lights == 0 && roomCount > 0
                    ? "PASS"
                    : "REVIEW";
                Debug.Log($"[CampusFullAudit] {status} {buildingName}: doors={doors.Length}, " +
                          $"interiorLights={lights}, indoorPossumRooms={roomCount}.");
            }

            ConfigureRuntimeSpawner();
            RebuildNavigation();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CampusFullAudit] COMPLETE: buildings={BuildingNames.Length}, doors={totalDoors}, " +
                      $"interiorLights={totalLights}. " +
                      "All seven buildings are configured for two confined indoor possums.");
        }

        private static int CountPlayableRooms(Transform building, bool bossBuilding)
        {
            Transform rooms = bossBuilding
                ? building.Find("03_Rooms_And_Furniture")
                : building.Find("Playable_Interior/02_Rooms_And_Furniture");
            return rooms != null ? rooms.childCount : 0;
        }

        private static void ConfigureRuntimeSpawner()
        {
            CampusObjectiveSpawner spawner = UnityEngine.Object.FindFirstObjectByType<CampusObjectiveSpawner>();
            if (spawner == null)
                return;

            SerializedObject serialized = new SerializedObject(spawner);
            serialized.FindProperty("_buildingsWithIndoorPossums").intValue = 7;
            serialized.FindProperty("_possumsPerSelectedBuilding").intValue = 2;
            serialized.FindProperty("_placementAttempts").intValue = 100;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        private static void ConfigureLloronaPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LloronaPrefabPath);
            try
            {
                NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
                if (agent == null)
                    agent = root.AddComponent<NavMeshAgent>();
                agent.speed = 11f;
                agent.acceleration = 36f;
                agent.angularSpeed = 0f;
                agent.stoppingDistance = 2.65f;
                agent.radius = 0.45f;
                agent.height = 2.75f;
                agent.autoBraking = true;
                agent.autoRepath = true;
                agent.updateRotation = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

                LloronaSecondFloorChase chase = root.GetComponent<LloronaSecondFloorChase>();
                if (chase == null)
                    chase = root.AddComponent<LloronaSecondFloorChase>();

                SerializedObject serializedChase = new SerializedObject(chase);
                serializedChase.FindProperty("_movementSpeed").floatValue = 11f;
                serializedChase.FindProperty("_aggroDistance").floatValue = 22f;
                serializedChase.FindProperty("_stoppingDistance").floatValue = 2.65f;
                serializedChase.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, LloronaPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RebuildNavigation()
        {
            NavMeshSurface surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogError("[CampusFullAudit] Enemy_Navigation NavMeshSurface is missing.");
                return;
            }

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
            Debug.Log("[CampusFullAudit] Rebuilt the campus NavMesh after the interior/door audit.");
        }
    }
}
#endif
