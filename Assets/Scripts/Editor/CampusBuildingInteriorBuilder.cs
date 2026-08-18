#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SpookyGame.Core;
using SpookyGame.Core.Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyGame.EditorTools
{
    /// <summary>
    /// Converts the remaining solid campus blockout cubes into playable,
    /// single-floor interiors while keeping every original footprint and
    /// minimap overlay in place.
    /// </summary>
    public static class CampusBuildingInteriorBuilder
    {
        private const string ScenePath = "Assets/Scenes/UTRGV_Blockout.unity";
        private const string SingleDoorPath = "Assets/Prefabs/Environment/Doors/Campus_Single_Door.prefab";
        private const string DoubleDoorPath = "Assets/Prefabs/Environment/Doors/Campus_Double_Door.prefab";

        private const float ExteriorWallThickness = 0.35f;
        private const float InteriorWallThickness = 0.22f;
        private const float MainHallWidth = 4f;
        private const float RoomDoorWidth = 1.4f;
        private const float ExteriorDoorWidth = 3.2f;
        private const float DoorHeight = 3f;

        private sealed class BuildingDefinition
        {
            public readonly string Name;
            public readonly float Width;
            public readonly float Depth;
            public readonly float WallHeight;
            public readonly int Bays;
            public readonly string ExteriorMaterialPath;
            public readonly string[] NorthRooms;
            public readonly string[] SouthRooms;

            public BuildingDefinition(
                string name,
                float width,
                float depth,
                float wallHeight,
                int bays,
                string exteriorMaterialPath,
                string[] northRooms,
                string[] southRooms)
            {
                Name = name;
                Width = width;
                Depth = depth;
                WallHeight = wallHeight;
                Bays = bays;
                ExteriorMaterialPath = exteriorMaterialPath;
                NorthRooms = northRooms;
                SouthRooms = southRooms;
            }
        }

        private static readonly BuildingDefinition[] Definitions =
        {
            new BuildingDefinition(
                "B10_Learning_Center", 30f, 26f, 4.8f, 5,
                "Assets/Materials/Campus/M_Campus_BrickWarm.mat",
                new[] { "Study_Room_A", "Tutoring_Lab_A", "Computer_Lab", "Study_Room_B" },
                new[] { "Quiet_Study_A", "Staff_Office", "Tutoring_Lab_B", "Quiet_Study_B" }),

            new BuildingDefinition(
                "B13_Fieldhouse", 42f, 42f, 7.5f, 3,
                "Assets/Materials/Campus/M_Campus_BrickWarm.mat",
                new[] { "Main_Training_Court", "Equipment_Storage" },
                new[] { "Home_Locker_Room", "Away_Locker_Room" }),

            new BuildingDefinition(
                "B14_Engineering_Building", 60f, 32f, 5f, 7,
                "Assets/Materials/Campus/M_Campus_Stucco.mat",
                new[] { "Robotics_Lab", "Electronics_Lab", "Fabrication_Lab", "Project_Lab", "Lecture_Hall_A", "Faculty_Office" },
                new[] { "Computer_Engineering_Lab", "Circuits_Lab", "Materials_Lab", "Machine_Shop", "Lecture_Hall_B", "Utility_Room" }),

            new BuildingDefinition(
                "B15_Health_Physical_Education_II", 48f, 36f, 5f, 5,
                "Assets/Materials/Campus/M_Campus_BrickLight.mat",
                new[] { "Exercise_Lab", "Kinesiology_Lab", "Rehab_Lab", "Equipment_Room" },
                new[] { "Classroom_A", "Classroom_B", "Locker_Room_A", "Locker_Room_B" }),

            new BuildingDefinition(
                "B16_Academic_Services", 46f, 34f, 4.8f, 5,
                "Assets/Materials/Campus/M_Campus_BrickWarm.mat",
                new[] { "Advising_Office_A", "Advising_Office_B", "Records_Office", "Financial_Aid" },
                new[] { "Reception", "Counseling_Office", "Testing_Room", "Staff_Office" }),

            new BuildingDefinition(
                "B27_Health_PE_Complex", 50f, 35f, 6f, 5,
                "Assets/Materials/Campus/M_Campus_Stucco.mat",
                new[] { "Training_Room", "Dance_Studio", "Fitness_Lab", "Storage" },
                new[] { "Locker_Room_A", "Locker_Room_B", "Classroom", "Athletic_Office" })
        };

        private static int _groundWallLayer;
        private static int _interactableLayer;
        private static Material _interiorWallMaterial;
        private static Material _floorMaterial;
        private static Material _roofMaterial;
        private static Material _metalMaterial;
        private static Material _orangeMaterial;
        private static GameObject _singleDoorPrefab;
        private static GameObject _doubleDoorPrefab;

        [MenuItem("Tools/Spooky Game/Build Remaining Campus Interiors")]
        public static void BuildAllRemainingBuildings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding campus interiors.");

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            LoadSharedAssets();
            BuildDoorPrefabs();

            GameObject campus = GameObject.Find("UTRGV_Campus_Blockout");
            Transform buildings = campus != null ? campus.transform.Find("Buildings") : null;
            if (buildings == null)
                throw new InvalidOperationException("UTRGV_Campus_Blockout/Buildings was not found.");

            foreach (BuildingDefinition definition in Definitions)
            {
                Transform building = buildings.Find(definition.Name);
                if (building == null)
                    throw new InvalidOperationException($"Building root is missing: {definition.Name}");

                BuildBuilding(building, definition);
            }

            EditorSceneManager.MarkSceneDirty(buildings.gameObject.scene);
            EditorSceneManager.SaveScene(buildings.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CampusBuildingInteriorBuilder] Built {Definitions.Length} single-floor campus interiors.");
        }

        private static void LoadSharedAssets()
        {
            _groundWallLayer = LayerMask.NameToLayer("GroundWall");
            _interactableLayer = LayerMask.NameToLayer("Interactable");
            if (_groundWallLayer < 0 || _interactableLayer < 0)
                throw new InvalidOperationException("GroundWall and Interactable layers are required.");

            _interiorWallMaterial = LoadMaterial("Assets/Materials/Campus/M_Campus_White.mat");
            _floorMaterial = LoadMaterial("Assets/Materials/Campus/M_Campus_Concrete.mat");
            _roofMaterial = LoadMaterial("Assets/Materials/Campus/M_Campus_Roof.mat");
            _metalMaterial = LoadMaterial("Assets/Materials/Campus/M_Campus_Metal.mat");
            _orangeMaterial = LoadMaterial("Assets/Materials/Campus/M_Campus_Orange.mat");
        }

        private static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new InvalidOperationException("Required material is missing: " + path);
            return material;
        }

        private static void BuildDoorPrefabs()
        {
            EnsureFolder("Assets/Prefabs/Environment/Doors");
            _singleDoorPrefab = CreateDoorPrefab(SingleDoorPath, RoomDoorWidth, false);
            _doubleDoorPrefab = CreateDoorPrefab(DoubleDoorPath, ExteriorDoorWidth, true);
        }

        private static GameObject CreateDoorPrefab(string path, float width, bool doubleDoor)
        {
            string prefabName = doubleDoor ? "Campus_Double_Door" : "Campus_Single_Door";
            var root = new GameObject(prefabName);

            CreateBox(root.transform, "Frame_Left",
                new Vector3(-width * 0.5f - 0.08f, DoorHeight * 0.5f, 0f),
                new Vector3(0.16f, DoorHeight, 0.22f), _orangeMaterial, _groundWallLayer, true);
            CreateBox(root.transform, "Frame_Right",
                new Vector3(width * 0.5f + 0.08f, DoorHeight * 0.5f, 0f),
                new Vector3(0.16f, DoorHeight, 0.22f), _orangeMaterial, _groundWallLayer, true);
            CreateBox(root.transform, "Frame_Header",
                new Vector3(0f, DoorHeight + 0.08f, 0f),
                new Vector3(width + 0.32f, 0.16f, 0.22f), _orangeMaterial, _groundWallLayer, true);

            var target = new GameObject("Interaction_Target");
            target.layer = _interactableLayer;
            target.transform.SetParent(root.transform, false);
            target.transform.localPosition = new Vector3(0f, DoorHeight * 0.5f, 0f);
            var targetCollider = target.AddComponent<BoxCollider>();
            targetCollider.isTrigger = true;
            targetCollider.size = new Vector3(width + 0.2f, DoorHeight, 0.55f);

            int leafCount = doubleDoor ? 2 : 1;
            var pivots = new Transform[leafCount];
            var multipliers = new float[leafCount];
            float leafWidth = doubleDoor ? width * 0.5f - 0.06f : width - 0.06f;

            if (doubleDoor)
            {
                pivots[0] = CreateDoorLeaf(target.transform, "Left", -width * 0.5f, leafWidth, 1f);
                pivots[1] = CreateDoorLeaf(target.transform, "Right", width * 0.5f, leafWidth, -1f);
                multipliers[0] = 1f;
                multipliers[1] = -1f;
            }
            else
            {
                pivots[0] = CreateDoorLeaf(target.transform, "Door", -width * 0.5f, leafWidth, 1f);
                multipliers[0] = 1f;
            }

            GameObject promptAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interact Prompt.prefab");
            if (promptAsset == null)
                throw new InvalidOperationException("Assets/Prefabs/Interact Prompt.prefab is missing.");

            GameObject promptObject = UnityEngine.Object.Instantiate(promptAsset);
            promptObject.name = "Interact Prompt";
            promptObject.transform.SetParent(target.transform, false);
            promptObject.transform.localPosition = Vector3.zero;
            promptObject.transform.localRotation = Quaternion.identity;
            promptObject.transform.localScale = Vector3.one;
            InteractionPrompt prompt = promptObject.GetComponent<InteractionPrompt>();

            SwingDoor swingDoor = target.AddComponent<SwingDoor>();
            var serializedDoor = new SerializedObject(swingDoor);
            serializedDoor.FindProperty("interactionPrompt").objectReferenceValue = prompt;
            serializedDoor.FindProperty("promptText").stringValue = "Press [E] to Open / Close";

            SerializedProperty pivotProperty = serializedDoor.FindProperty("_doorPivots");
            pivotProperty.arraySize = pivots.Length;
            for (int i = 0; i < pivots.Length; i++)
                pivotProperty.GetArrayElementAtIndex(i).objectReferenceValue = pivots[i];

            SerializedProperty multiplierProperty = serializedDoor.FindProperty("_directionMultipliers");
            multiplierProperty.arraySize = multipliers.Length;
            for (int i = 0; i < multipliers.Length; i++)
                multiplierProperty.GetArrayElementAtIndex(i).floatValue = multipliers[i];

            serializedDoor.FindProperty("_openAngle").floatValue = 100f;
            serializedDoor.FindProperty("_openDuration").floatValue = 0.55f;
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform CreateDoorLeaf(
            Transform target,
            string side,
            float hingeX,
            float leafWidth,
            float direction)
        {
            var hinge = new GameObject(side + "_Hinge").transform;
            hinge.SetParent(target, false);
            hinge.localPosition = new Vector3(hingeX, -DoorHeight * 0.5f, 0f);

            float leafCenterX = direction * leafWidth * 0.5f;
            GameObject leaf = CreateBox(hinge, side + "_Door_Leaf",
                new Vector3(leafCenterX, DoorHeight * 0.5f, 0f),
                new Vector3(leafWidth, DoorHeight, 0.14f), _metalMaterial, _groundWallLayer, true);

            float handleX = direction * leafWidth * 0.34f;
            CreateBox(leaf.transform, side + "_Handle",
                new Vector3(handleX / leafWidth, 0f, 0.62f),
                new Vector3(0.08f / leafWidth, 0.06f / DoorHeight, 0.16f / 0.14f),
                _orangeMaterial, 0, false);
            return hinge;
        }

        private static void BuildBuilding(Transform building, BuildingDefinition definition)
        {
            if (definition.Bays < 3 || definition.Bays % 2 == 0)
                throw new InvalidOperationException(definition.Name + " must use an odd number of room bays.");
            if (definition.NorthRooms.Length != definition.Bays - 1 ||
                definition.SouthRooms.Length != definition.Bays - 1)
                throw new InvalidOperationException(definition.Name + " room-name count does not match its layout.");

            PreserveMinimapOverlayWhileNormalizing(building);
            RemoveOriginalSolidCube(building);

            Transform previous = building.Find("Playable_Interior");
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var generated = new GameObject("Playable_Interior").transform;
            generated.SetParent(building, false);

            Transform architecture = CreateContainer(generated, "01_Architecture");
            Transform rooms = CreateContainer(generated, "02_Rooms_And_Furniture");
            Transform doors = CreateContainer(generated, "03_Functional_Doors");
            Material exterior = LoadMaterial(definition.ExteriorMaterialPath);
            BuildShell(architecture, definition, exterior);
            BuildCrossHallAndRooms(architecture, rooms, doors, definition);
        }

        private static void PreserveMinimapOverlayWhileNormalizing(Transform building)
        {
            var overlays = new List<Transform>();
            foreach (Transform child in building)
            {
                if (child.name.IndexOf("Minimap", StringComparison.OrdinalIgnoreCase) >= 0)
                    overlays.Add(child);
            }

            foreach (Transform overlay in overlays)
                overlay.SetParent(null, true);

            Vector3 worldPosition = building.position;
            building.localScale = Vector3.one;
            building.position = new Vector3(worldPosition.x, 0f, worldPosition.z);

            foreach (Transform overlay in overlays)
                overlay.SetParent(building, true);
        }

        private static void RemoveOriginalSolidCube(Transform building)
        {
            foreach (BoxCollider collider in building.GetComponents<BoxCollider>())
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (MeshRenderer renderer in building.GetComponents<MeshRenderer>())
                UnityEngine.Object.DestroyImmediate(renderer);
            foreach (MeshFilter filter in building.GetComponents<MeshFilter>())
                UnityEngine.Object.DestroyImmediate(filter);
        }

        private static void BuildShell(
            Transform architecture,
            BuildingDefinition definition,
            Material exterior)
        {
            float width = definition.Width;
            float depth = definition.Depth;
            float height = definition.WallHeight;

            CreateBox(architecture, "Continuous_Floor",
                new Vector3(0f, 0.04f, 0f),
                new Vector3(width - 0.05f, 0.08f, depth - 0.05f),
                _floorMaterial, _groundWallLayer, true);

            CreateBox(architecture, "Roof",
                new Vector3(0f, height + 0.15f, 0f),
                new Vector3(width + 0.05f, 0.3f, depth + 0.05f),
                _roofMaterial, _groundWallLayer, true);

            CreateWallSpanWithOpening(architecture, "South_Exterior_Wall", -depth * 0.5f,
                -width * 0.5f, width * 0.5f, 0f, ExteriorDoorWidth + 0.12f,
                DoorHeight, ExteriorWallThickness, height, exterior);
            CreateWallSpanWithOpening(architecture, "North_Exterior_Wall", depth * 0.5f,
                -width * 0.5f, width * 0.5f, 0f, ExteriorDoorWidth + 0.12f,
                DoorHeight, ExteriorWallThickness, height, exterior);

            CreateBox(architecture, "West_Exterior_Wall",
                new Vector3(-width * 0.5f, height * 0.5f, 0f),
                new Vector3(ExteriorWallThickness, height, depth), exterior, _groundWallLayer, true);
            CreateBox(architecture, "East_Exterior_Wall",
                new Vector3(width * 0.5f, height * 0.5f, 0f),
                new Vector3(ExteriorWallThickness, height, depth), exterior, _groundWallLayer, true);

            CreateBox(architecture, "South_Entrance_Canopy",
                new Vector3(0f, DoorHeight + 0.45f, -depth * 0.5f - 0.52f),
                new Vector3(4.4f, 0.22f, 1.4f), _orangeMaterial, _groundWallLayer, true);
            CreateBox(architecture, "South_Entrance_Accent",
                new Vector3(0f, DoorHeight + 0.82f, -depth * 0.5f - 0.2f),
                new Vector3(4.1f, 0.48f, 0.16f), _orangeMaterial, _groundWallLayer, false);
        }

        private static void BuildCrossHallAndRooms(
            Transform architecture,
            Transform rooms,
            Transform doors,
            BuildingDefinition definition)
        {
            float usableWidth = definition.Width - ExteriorWallThickness * 2f;
            float bayWidth = usableWidth / definition.Bays;
            float leftEdge = -usableWidth * 0.5f;
            int lobbyBay = definition.Bays / 2;

            float northRoomMinZ = MainHallWidth * 0.5f;
            float northRoomMaxZ = definition.Depth * 0.5f - ExteriorWallThickness;
            float southRoomMinZ = -definition.Depth * 0.5f + ExteriorWallThickness;
            float southRoomMaxZ = -MainHallWidth * 0.5f;

            for (int boundary = 1; boundary < definition.Bays; boundary++)
            {
                float x = leftEdge + bayWidth * boundary;
                CreateBox(architecture, $"North_Room_Partition_{boundary:00}",
                    new Vector3(x, definition.WallHeight * 0.5f,
                        (northRoomMinZ + northRoomMaxZ) * 0.5f),
                    new Vector3(InteriorWallThickness, definition.WallHeight,
                        northRoomMaxZ - northRoomMinZ),
                    _interiorWallMaterial, _groundWallLayer, true);
                CreateBox(architecture, $"South_Room_Partition_{boundary:00}",
                    new Vector3(x, definition.WallHeight * 0.5f,
                        (southRoomMinZ + southRoomMaxZ) * 0.5f),
                    new Vector3(InteriorWallThickness, definition.WallHeight,
                        southRoomMaxZ - southRoomMinZ),
                    _interiorWallMaterial, _groundWallLayer, true);
            }

            int northNameIndex = 0;
            int southNameIndex = 0;
            for (int bay = 0; bay < definition.Bays; bay++)
            {
                float bayMin = leftEdge + bayWidth * bay;
                float bayMax = bayMin + bayWidth;
                float centerX = (bayMin + bayMax) * 0.5f;

                if (bay == lobbyBay)
                {
                    CreateWallSpanWithOpening(architecture, "North_Lobby_Portal", MainHallWidth * 0.5f,
                        bayMin, bayMax, 0f, MainHallWidth, DoorHeight,
                        InteriorWallThickness, definition.WallHeight, _interiorWallMaterial);
                    CreateWallSpanWithOpening(architecture, "South_Lobby_Portal", -MainHallWidth * 0.5f,
                        bayMin, bayMax, 0f, MainHallWidth, DoorHeight,
                        InteriorWallThickness, definition.WallHeight, _interiorWallMaterial);
                    continue;
                }

                string northName = definition.NorthRooms[northNameIndex++];
                string southName = definition.SouthRooms[southNameIndex++];

                CreateWallSpanWithOpening(architecture, northName + "_Hall_Wall", MainHallWidth * 0.5f,
                    bayMin, bayMax, centerX, RoomDoorWidth + 0.08f, DoorHeight,
                    InteriorWallThickness, definition.WallHeight, _interiorWallMaterial);
                CreateWallSpanWithOpening(architecture, southName + "_Hall_Wall", -MainHallWidth * 0.5f,
                    bayMin, bayMax, centerX, RoomDoorWidth + 0.08f, DoorHeight,
                    InteriorWallThickness, definition.WallHeight, _interiorWallMaterial);

                InstantiateDoor(_singleDoorPrefab, doors, northName + "_Door",
                    new Vector3(centerX, 0f, MainHallWidth * 0.5f), 0f);
                InstantiateDoor(_singleDoorPrefab, doors, southName + "_Door",
                    new Vector3(centerX, 0f, -MainHallWidth * 0.5f), 0f);

                float roomWidth = bayWidth - InteriorWallThickness;
                float northDepth = northRoomMaxZ - northRoomMinZ;
                float southDepth = southRoomMaxZ - southRoomMinZ;
                Vector3 northCenter = new Vector3(centerX, 0f, (northRoomMinZ + northRoomMaxZ) * 0.5f);
                Vector3 southCenter = new Vector3(centerX, 0f, (southRoomMinZ + southRoomMaxZ) * 0.5f);

                Transform northRoom = CreateContainer(rooms, northName);
                Transform southRoom = CreateContainer(rooms, southName);
                AddRoomFurniture(northRoom, northName, northCenter, roomWidth, northDepth, 1f);
                AddRoomFurniture(southRoom, southName, southCenter, roomWidth, southDepth, -1f);
            }

            InstantiateDoor(_doubleDoorPrefab, doors, "South_Main_Entrance",
                new Vector3(0f, 0f, -definition.Depth * 0.5f), 0f);
            InstantiateDoor(_doubleDoorPrefab, doors, "North_Rear_Exit",
                new Vector3(0f, 0f, definition.Depth * 0.5f), 0f);
        }

        private static void AddRoomFurniture(
            Transform room,
            string roomName,
            Vector3 center,
            float roomWidth,
            float roomDepth,
            float side)
        {
            string lower = roomName.ToLowerInvariant();
            float safeWidth = Mathf.Max(1.8f, roomWidth - 1.2f);

            if (lower.Contains("court") || lower.Contains("studio") ||
                lower.Contains("training") || lower.Contains("exercise") || lower.Contains("fitness"))
            {
                CreateSimpleBench(room, "Wall_Bench", center + new Vector3(0f, 0f, side * roomDepth * 0.28f),
                    Mathf.Min(4f, safeWidth));
                return;
            }

            if (lower.Contains("locker"))
            {
                CreateSimpleBench(room, "Center_Bench", center, Mathf.Min(3.5f, safeWidth));
                CreateBox(room, "Locker_Block",
                    center + new Vector3(0f, 1f, side * roomDepth * 0.35f),
                    new Vector3(Mathf.Min(4f, safeWidth), 2f, 0.55f),
                    _metalMaterial, _groundWallLayer, true);
                return;
            }

            if (lower.Contains("storage") || lower.Contains("equipment") || lower.Contains("utility"))
            {
                CreateBox(room, "Storage_Shelf",
                    center + new Vector3(0f, 1.1f, side * roomDepth * 0.3f),
                    new Vector3(Mathf.Min(4f, safeWidth), 2.2f, 0.65f),
                    _metalMaterial, _groundWallLayer, true);
                return;
            }

            if (lower.Contains("office") || lower.Contains("advising") ||
                lower.Contains("records") || lower.Contains("financial") ||
                lower.Contains("reception") || lower.Contains("counseling"))
            {
                CreateSimpleTable(room, "Office_Desk", center + new Vector3(0f, 0f, side * roomDepth * 0.12f),
                    Mathf.Min(2.4f, safeWidth));
                CreateBox(room, "File_Cabinet",
                    center + new Vector3(safeWidth * 0.3f, 0.65f, side * roomDepth * 0.34f),
                    new Vector3(0.7f, 1.3f, 0.55f), _metalMaterial, _groundWallLayer, true);
                return;
            }

            CreateSimpleTable(room, "Work_Table_A",
                center + new Vector3(0f, 0f, -roomDepth * 0.18f), Mathf.Min(3f, safeWidth));
            CreateSimpleTable(room, "Work_Table_B",
                center + new Vector3(0f, 0f, roomDepth * 0.18f), Mathf.Min(3f, safeWidth));
        }

        private static void CreateSimpleTable(Transform parent, string name, Vector3 center, float width)
        {
            Transform table = CreateContainer(parent, name);
            CreateBox(table, "Top", center + Vector3.up * 0.78f,
                new Vector3(width, 0.12f, 1.1f), _metalMaterial, _groundWallLayer, true);
            CreateBox(table, "Left_Support", center + new Vector3(-width * 0.38f, 0.38f, 0f),
                new Vector3(0.12f, 0.7f, 0.9f), _metalMaterial, _groundWallLayer, true);
            CreateBox(table, "Right_Support", center + new Vector3(width * 0.38f, 0.38f, 0f),
                new Vector3(0.12f, 0.7f, 0.9f), _metalMaterial, _groundWallLayer, true);
        }

        private static void CreateSimpleBench(Transform parent, string name, Vector3 center, float width)
        {
            Transform bench = CreateContainer(parent, name);
            CreateBox(bench, "Seat", center + Vector3.up * 0.48f,
                new Vector3(width, 0.14f, 0.55f), _metalMaterial, _groundWallLayer, true);
            CreateBox(bench, "Left_Support", center + new Vector3(-width * 0.38f, 0.23f, 0f),
                new Vector3(0.12f, 0.45f, 0.42f), _metalMaterial, _groundWallLayer, true);
            CreateBox(bench, "Right_Support", center + new Vector3(width * 0.38f, 0.23f, 0f),
                new Vector3(0.12f, 0.45f, 0.42f), _metalMaterial, _groundWallLayer, true);
        }

        private static void CreateWallSpanWithOpening(
            Transform parent,
            string name,
            float z,
            float spanMin,
            float spanMax,
            float openingCenter,
            float openingWidth,
            float openingHeight,
            float thickness,
            float wallHeight,
            Material material)
        {
            float openingMin = Mathf.Max(spanMin, openingCenter - openingWidth * 0.5f);
            float openingMax = Mathf.Min(spanMax, openingCenter + openingWidth * 0.5f);
            float leftWidth = openingMin - spanMin;
            float rightWidth = spanMax - openingMax;

            if (leftWidth > 0.02f)
            {
                CreateBox(parent, name + "_Left",
                    new Vector3(spanMin + leftWidth * 0.5f, wallHeight * 0.5f, z),
                    new Vector3(leftWidth, wallHeight, thickness), material, _groundWallLayer, true);
            }

            if (rightWidth > 0.02f)
            {
                CreateBox(parent, name + "_Right",
                    new Vector3(openingMax + rightWidth * 0.5f, wallHeight * 0.5f, z),
                    new Vector3(rightWidth, wallHeight, thickness), material, _groundWallLayer, true);
            }

            float headerHeight = wallHeight - openingHeight;
            if (headerHeight > 0.02f && openingMax > openingMin)
            {
                CreateBox(parent, name + "_Header",
                    new Vector3((openingMin + openingMax) * 0.5f,
                        openingHeight + headerHeight * 0.5f, z),
                    new Vector3(openingMax - openingMin, headerHeight, thickness),
                    material, _groundWallLayer, true);
            }
        }

        private static void InstantiateDoor(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 localPosition,
            float yaw)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            door.name = name;
            door.transform.SetParent(parent, false);
            door.transform.localPosition = localPosition;
            door.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static Transform CreateContainer(Transform parent, string name)
        {
            var container = new GameObject(name).transform;
            container.SetParent(parent, false);
            return container;
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            int layer,
            bool keepCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = layer;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = localScale;

            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (!keepCollider)
                UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            return box;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
#endif
