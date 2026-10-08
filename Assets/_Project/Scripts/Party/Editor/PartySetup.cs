using System.Collections.Generic;
using TpsDungeon.Combat;
using TpsDungeon.Hud.Editor;
using TpsDungeon.Interaction.Editor;
using TpsDungeon.Menu;
using TpsDungeon.Menu.Editor;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using TpsDungeon.Progression.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using PartyGroup = TpsDungeon.Party.Party;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// パーティー（プレイヤーと仲間に違いの無い仕組み）を組み込む。何度実行しても同じ結果になる。
    /// 1. レイヤー「Ally」を用意し、Ally どうしは物理で当たらないようにする。
    /// 2. 角・耳・しっぽの置き方のアセット（CharacterAccessoryCatalog）を用意し、GanzSe の小物を差し込む。
    /// 3. パーティーの操作台のプレハブ（Party.prefab）を作る。PlayerInput・移動の入力（CharacterInput）・Party・
    ///    PartyProgression・操作の設定・地図の開け閉め・デバッグ入力を持たせ、HUD・ポーズ・インベントリ・照準の案内を子に組み込む。
    /// 4. キャラのプレハブ（Character Variant）から、操作台へ移した物（入力・UI・パーティーの経験値）と Starter Assets の操作を外し、
    ///    CharacterMotor（Starter Assets の操作の写し）・NavMeshAgent・PartyMember・仲間の歩きと戦い・見た目を付ける。レイヤーは Ally にする。
    /// </summary>
    public static class PartySetup
    {
        public const string CharacterPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        public const string PartyPrefabPath = "Assets/_Project/Prefabs/Character/Party.prefab";
        public const string CatalogPath = "Assets/_Project/Settings/Party/CharacterAccessoryCatalog.asset";

        private const string AccessoryFolder = "Assets/ThirdParty/3D Model/URP GanzSe Free Character Accessories/Prefabs/";
        private const string PlayerInputActionsPath = "Assets/_Project/Settings/Input/PlayerControls.inputactions";

        private static readonly string[] UiChildren = { "Interaction HUD", "Game HUD", "Pause Menu", "Inventory Screen" };

        [MenuItem("Tools/TPS Dungeon/Party/パーティーを組み込む")]
        public static void Setup()
        {
            int ally = EnsureAllyLayer();
            CharacterAccessoryCatalog catalog = EnsureCatalog();

            // 操作台に移す設定を、外す前のキャラのプレハブから読んでおく。
            var source = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
            try
            {
                CreatePartyPrefab(source);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }

            // 画面は操作台へ（各画面の組み込みは操作台を相手にする）。
            InventorySetup.Setup();
            PauseMenuSetup.Setup();
            PlayerHudSetup.Setup();
            PlayerInteractionSetup.Setup();
            ProgressionSetup.Setup();

            ConvertCharacter(ally, catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"パーティーを組み込んだ: {PartyPrefabPath} / {CharacterPrefabPath}");
        }

        // ---- レイヤー --------------------------------------------------------

        /// <summary>レイヤー「Ally」を用意し（主人公が元から使っていた 8 番を優先）、Ally どうしの衝突を切る。</summary>
        public static int EnsureAllyLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            int index = -1;
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == AllyLayer.Name) index = i;
            }

            if (index < 0)
            {
                int[] candidates = { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 };
                foreach (int i in candidates)
                {
                    if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                    index = i;
                    break;
                }

                if (index < 0) throw new System.InvalidOperationException("空いているレイヤーが無い");
                layers.GetArrayElementAtIndex(index).stringValue = AllyLayer.Name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }

            Physics.IgnoreLayerCollision(index, index, true);
            AssetDatabase.SaveAssets();
            return index;
        }

        // ---- 見た目の小物 ----------------------------------------------------

        public static CharacterAccessoryCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterAccessoryCatalog>(CatalogPath);
            if (catalog == null)
            {
                EnsureFolder("Assets/_Project/Settings/Party");
                catalog = ScriptableObject.CreateInstance<CharacterAccessoryCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetPrefabs(
                Load("FCA_Horn_Type1_L_Color{0}", CharacterLook.HornColors),
                Load("FCA_Horn_Type1_R_Color{0}", CharacterLook.HornColors),
                Load("FCA_Ear_Type1_L_Color{0}", CharacterLook.EarColors),
                Load("FCA_Ear_Type1_R_Color{0}", CharacterLook.EarColors),
                Load("FCA_Tail_Static_Type1_Color{0}", CharacterLook.TailColors));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static GameObject[] Load(string pattern, int count)
        {
            var result = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                string path = AccessoryFolder + string.Format(pattern, i + 1) + ".prefab";
                result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (result[i] == null) Debug.LogWarning($"小物が見つからない: {path}");
            }

            return result;
        }

        // ---- 操作台 ----------------------------------------------------------

        private static void CreatePartyPrefab(GameObject character)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PartyPrefabPath) : new GameObject("Party");
            try
            {
                var playerInput = GetOrAdd<PlayerInput>(root);
                var sourceInput = character.GetComponent<PlayerInput>();
                if (sourceInput != null) EditorUtility.CopySerialized(sourceInput, playerInput);
                else
                {
                    playerInput.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerInputActionsPath);
                    playerInput.defaultActionMap = "Player";
                    playerInput.notificationBehavior = PlayerNotifications.SendMessages;
                }

                var input = GetOrAdd<CharacterInput>(root);
                Component starterInputs = FindByTypeName(character, "StarterAssetsInputs");
                if (starterInputs != null)
                {
                    var from = new SerializedObject(starterInputs);
                    var to = new SerializedObject(input);
                    foreach (string name in new[] { "analogMovement", "cursorLocked", "cursorInputForLook", "lookSensitivity" })
                    {
                        SerializedProperty p = from.FindProperty(name);
                        if (p != null) to.CopyFromSerializedProperty(p);
                    }

                    to.ApplyModifiedPropertiesWithoutUndo();
                }

                var progression = GetOrAdd<PartyProgression>(root);
                var sourceProgression = character.GetComponent<PartyProgression>();
                if (sourceProgression != null) EditorUtility.CopySerialized(sourceProgression, progression);

                var debugInput = GetOrAdd<ProgressionDebugInput>(root);
                var sourceDebug = character.GetComponent<ProgressionDebugInput>();
                if (sourceDebug != null) EditorUtility.CopySerialized(sourceDebug, debugInput);

                var controls = GetOrAdd<PlayerControlSettings>(root);
                SetReference(controls, "playerInput", playerInput);
                SetReference(controls, "hotbar", null);

                var mapToggle = GetOrAdd<PlayerMapToggle>(root);
                SetReference(mapToggle, "playerInput", playerInput);

                var party = GetOrAdd<PartyGroup>(root);
                SetReference(party, "input", input);

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        // ---- キャラ ----------------------------------------------------------

        private static void ConvertCharacter(int ally, CharacterAccessoryCatalog catalog)
        {
            var root = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
            try
            {
                var motor = GetOrAdd<CharacterMotor>(root);
                Component thirdPerson = FindByTypeName(root, "ThirdPersonController");
                if (thirdPerson != null) CopyMotorSettings(thirdPerson, motor);
                if (motor.CinemachineCameraTarget == null)
                {
                    Transform cameraRoot = root.transform.Find("PlayerCameraRoot");
                    if (cameraRoot != null) motor.CinemachineCameraTarget = cameraRoot.gameObject;
                }

                // 操作台へ移した物と、Starter Assets の操作を外す（ThirdPersonController が PlayerInput を要求するので先に外す）。
                foreach (string typeName in new[] { "ThirdPersonController", "StarterAssetsInputs", "BasicRigidBodyPush" })
                {
                    Component c = FindByTypeName(root, typeName);
                    if (c != null) Object.DestroyImmediate(c, true);
                }

                RemoveComponent<PlayerControlSettings>(root);
                RemoveComponent<PlayerMapToggle>(root);
                RemoveComponent<ProgressionDebugInput>(root);
                RemoveComponent<PartyProgression>(root);
                RemoveComponent<PlayerInput>(root);

                foreach (string name in UiChildren)
                {
                    Transform child = root.transform.Find(name);
                    if (child != null) Object.DestroyImmediate(child.gameObject, true);
                }

                ClearReference(root.GetComponent<PlayerHotbar>(), "playerInput");
                ClearReference(root.GetComponent<PlayerMeleeInput>(), "playerInput");

                var agent = GetOrAdd<NavMeshAgent>(root);
                var controller = root.GetComponent<CharacterController>();
                agent.radius = controller != null ? Mathf.Max(0.25f, controller.radius) : 0.3f;
                agent.height = controller != null ? controller.height : 1.8f;
                agent.baseOffset = 0f;
                agent.speed = 2f;
                agent.angularSpeed = 720f;
                agent.acceleration = 24f;
                agent.stoppingDistance = 0.35f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                agent.enabled = false;

                GetOrAdd<PartyMember>(root);
                GetOrAdd<FollowerLocomotion>(root).enabled = false;
                GetOrAdd<FollowerBrain>(root).enabled = false;
                GetOrAdd<CharacterAppearance>(root).Catalog = catalog;

                root.layer = ally;
                PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CopyMotorSettings(Component thirdPerson, CharacterMotor motor)
        {
            var from = new SerializedObject(thirdPerson);
            var to = new SerializedObject(motor);
            string[] names =
            {
                "MoveSpeed", "SprintSpeed", "RotationSmoothTime", "SpeedChangeRate", "LandingAudioClip", "FootstepAudioClips",
                "FootstepAudioVolume", "JumpHeight", "Gravity", "JumpTimeout", "FallTimeout", "GroundedOffset", "GroundedRadius",
                "GroundLayers", "CinemachineCameraTarget", "TopClamp", "BottomClamp", "CameraAngleOverride", "LockCameraPosition",
            };
            foreach (string name in names)
            {
                SerializedProperty p = from.FindProperty(name);
                if (p != null) to.CopyFromSerializedProperty(p);
            }

            to.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 道具 ------------------------------------------------------------

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c, true);
        }

        private static Component FindByTypeName(GameObject go, string typeName)
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName) return c;
            }

            return null;
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty p = serialized.FindProperty(propertyName);
            if (p == null) return;
            p.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearReference(Object target, string propertyName)
        {
            if (target != null) SetReference(target, propertyName, null);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
