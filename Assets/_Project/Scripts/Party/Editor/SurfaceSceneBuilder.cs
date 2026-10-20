using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PartyGroup = TpsDungeon.Party.Party;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上（城の前の広場）のシーンを作る。何度実行しても同じ形に作り直す。
    /// - 地形は ProBuilder の箱で組む（あとからシーンで ProBuilder の道具で手直しできる）。広場の北が城（ダンジョンの塔。<see cref="SurfaceTower"/>）で、上へ行くほど太い。
    /// - 歩けるのは地面の範囲だけ。塀は置かず、地面のまわりを見えない壁（[Bounds]）で囲む。
    /// - 城の正面に 1F へ入る大門（<see cref="SceneGate"/>）を 1 つ付ける。ほかに、解放した層のボス階へ登れるショートカット（空から垂れたロープ）を村のあちこちに置く（<see cref="SurfaceShortcuts"/>）。
    /// - 村（武器屋・銀行・民家・小物・木）とまわりの景色は <see cref="SurfaceScenery"/> が RPG Poly Pack Lite で置く。強化は死んだときに行く別の場所でするので、ここには置かない。
    /// - パーティー一式（操作台・仲間 4 人・カメラ）を [Party Rig] にまとめ、<see cref="GameFlow"/> で城の中へ持ち越す。
    /// - 床と壁から NavMesh を焼いてシーンと一緒に保存する（地上は毎回同じ形なので実行中には焼かない）。
    /// Build Settings では先頭（起動したときのシーン）にする。
    /// </summary>
    public static class SurfaceSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Surface.unity";
        private const string NavMeshPath = "Assets/_Project/Scenes/Surface/NavMesh-Surface.asset";
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";
        private const string FollowCameraPrefabPath = "Assets/ThirdParty/3D Model/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab";
        private const string FirstFloorScene = "L1_Random";
        private const int Followers = 4;

        // 地面の範囲（メートル）。+Z が城。歩けるのはこの範囲だけで、まわりを見えない壁で囲む。
        private const float GroundMinX = -50f;
        private const float GroundMaxX = 50f;
        private const float GroundMinZ = -31f;
        private const float GroundMaxZ = 69f;
        private const float BoundsHeight = 30f;
        private const float BoundsThickness = 2f;
        private const float CastleFront = 30f;
        public const string GateSpawnName = "CastleGateSpawn";
        // シーンを移るとき（門・階段）に鳴らす音。
        public const string TravelSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/アスファルトの上を走る1 (cut).mp3";
        // 門の前のどれだけ手前に立つか（メートル）。
        private const float GateSpawnDistance = 3f;

        [MenuItem("Tools/TPS Dungeon/地上シーンを作る")]
        public static void GenerateFromMenu() => Debug.Log(Generate());

        /// <summary>地上のシーンを作り直して保存し、何をしたかを返す。</summary>
        public static string Generate()
        {
            var partyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartySetup.PartyPrefabPath);
            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartySetup.CharacterPrefabPath);
            var followCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefabPath);
            if (partyPrefab == null || characterPrefab == null) return "先に Tools/TPS Dungeon/Party/パーティーを組み込む を実行すること。";
            if (followCameraPrefab == null) return $"追従カメラのプレハブが無い: {FollowCameraPrefabPath}";

            var materials = LoadMaterials();
            if (materials == null) return $"{MaterialsFolder} のプレースホルダの材質が足りない。先に「プレースホルダのマップ一式を生成」を実行すること。";

            // 追加ロードで作って保存後に閉じる。いま開いているシーンの未保存の変更を巻き込まないため。
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                CreateLighting();
                Transform world = new GameObject("[Surface]").transform;
                BuildPlaza(world, materials);
                BuildBounds();
                BuildCastle(world);
                Transform spawn = CreateSpawn(world);
                CreateGateSpawn(world);

                PlaceParty(spawn, partyPrefab, characterPrefab, followCameraPrefab);
                SurfaceScenery.Build(scene, world);
                SurfaceShortcuts.Build(world);

                // 保存してからでないと NavMesh のアセットの置き場（シーン名のフォルダ）を決められないので、先に 1 度保存する。
                EditorSceneManager.SaveScene(scene, ScenePath);
                BakeNavMesh(world.gameObject);
                EditorSceneManager.SaveScene(scene, ScenePath);
                PutFirstInBuildSettings(ScenePath);
            }
            finally
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            return $"{ScenePath} を作った（地面 {GroundMaxX - GroundMinX}×{GroundMaxZ - GroundMinZ}m を見えない壁で囲む、1F の門 → {FirstFloorScene}、拠点 2 つは閉じている、パーティー {Followers + 1} 人）。";
        }

        private static Dictionary<string, Material> LoadMaterials()
        {
            var result = new Dictionary<string, Material>();
            foreach (string key in new[] { "Floor", "Wall", "Door", "DoorLeaf" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Placeholder_{key}.mat");
                if (material == null) return null;
                result[key] = material;
            }
            return result;
        }

        private static void CreateLighting()
        {
            var light = new GameObject("Directional Light");
            var component = light.AddComponent<Light>();
            component.type = LightType.Directional;
            component.intensity = 1.3f;
            component.color = new Color(1f, 0.96f, 0.88f);
            component.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

            // 屋外なので空・地平・地面で色を変えた環境光にする。
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.2f);
        }

        // --- 地形 ---

        private static void BuildPlaza(Transform world, Dictionary<string, Material> m)
        {
            Transform group = Group(world, "Plaza");

            // 床の上面を y = 0 にする（階のシーンと同じ）。
            var center = new Vector3((GroundMinX + GroundMaxX) * 0.5f, -0.25f, (GroundMinZ + GroundMaxZ) * 0.5f);
            Box(group, "Ground", m["Floor"], center, new Vector3(GroundMaxX - GroundMinX, 0.5f, GroundMaxZ - GroundMinZ));
        }

        /// <summary>
        /// 地面の外へ出られないよう、地面の 4 辺のすぐ外に見えない壁（当たりだけの箱）を立てる。
        /// - [Surface] の外に置く。NavMesh は [Surface] の下だけを焼くので、壁の上面に NavMesh ができない。
        /// - レイヤーは Ignore Raycast。キャラは止めるが、照準などのレイ（既定のマスク）やカメラの壁よけ（Default だけを見る）には掛からない。
        /// </summary>
        private static void BuildBounds()
        {
            var root = new GameObject("[Bounds]").transform;
            float width = GroundMaxX - GroundMinX + BoundsThickness * 2f;
            float depth = GroundMaxZ - GroundMinZ;
            float centerX = (GroundMinX + GroundMaxX) * 0.5f;
            float centerZ = (GroundMinZ + GroundMaxZ) * 0.5f;
            float y = BoundsHeight * 0.5f;
            float half = BoundsThickness * 0.5f;

            InvisibleWall(root, "Bound_S", new Vector3(centerX, y, GroundMinZ - half), new Vector3(width, BoundsHeight, BoundsThickness));
            InvisibleWall(root, "Bound_N", new Vector3(centerX, y, GroundMaxZ + half), new Vector3(width, BoundsHeight, BoundsThickness));
            InvisibleWall(root, "Bound_W", new Vector3(GroundMinX - half, y, centerZ), new Vector3(BoundsThickness, BoundsHeight, depth));
            InvisibleWall(root, "Bound_E", new Vector3(GroundMaxX + half, y, centerZ), new Vector3(BoundsThickness, BoundsHeight, depth));
        }

        private static void InvisibleWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name) { layer = 2 };
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = center;
            wall.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>城（ダンジョンの塔）。土台・大門・上ほど太い塔は <see cref="SurfaceTower"/> が建てる。</summary>
        private static void BuildCastle(Transform world) => SurfaceTower.Build(world, out _);

        private static Transform CreateSpawn(Transform world)
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(world, false);
            // 南の塀の手前で、城を向いて立つ。
            spawn.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, GroundMinZ + 7f), Quaternion.identity);
            spawn.AddComponent<PlayerSpawnPoint>();
            return spawn.transform;
        }

        /// <summary>城の大門の前。ダンジョンから地上へ降りてきたパーティーが、門を背にして立つ（PlayerSpawnPoint.CastleGate）。</summary>
        public static Transform CreateGateSpawn(Transform world)
        {
            var spawn = new GameObject(GateSpawnName);
            spawn.transform.SetParent(world, false);
            spawn.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, CastleFront - GateSpawnDistance), Quaternion.Euler(0f, 180f, 0f));
            spawn.AddComponent<PlayerSpawnPoint>().PointId = PlayerSpawnPoint.CastleGate;
            return spawn.transform;
        }

        internal static void AddGate(GameObject target, string sceneName, bool open, string openLabel, string closedLabel, string title, string body,
            MapIcon icon = MapIcon.None, string mapLabel = null)
        {
            var gate = target.AddComponent<SceneGate>();
            var serialized = new SerializedObject(gate);
            serialized.FindProperty("sceneName").stringValue = sceneName;
            serialized.FindProperty("open").boolValue = open;
            serialized.FindProperty("openLabel").stringValue = openLabel;
            serialized.FindProperty("closedLabel").stringValue = closedLabel;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = body;
            serialized.FindProperty("mapIcon").enumValueIndex = (int)icon;
            serialized.FindProperty("mapLabel").stringValue = mapLabel ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>中心 center・大きさ size の ProBuilder の箱。当たりは MeshCollider。</summary>
        internal static GameObject Box(Transform parent, string name, Material material, Vector3 center, Vector3 size)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = center;

            mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();

            mesh.gameObject.AddComponent<MeshCollider>();
            GameObjectUtility.SetStaticEditorFlags(mesh.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return mesh.gameObject;
        }

        // --- パーティー ---

        /// <summary>
        /// [Party Rig] の下に、カメラ・操作台・先頭・仲間を置く。GameFlow がこの親ごと次のシーンへ持ち越す。
        /// </summary>
        private static void PlaceParty(Transform spawn, GameObject partyPrefab, GameObject characterPrefab, GameObject followCameraPrefab)
        {
            var rig = new GameObject("[Party Rig]");
            var flow = rig.AddComponent<GameFlow>();
            var flowSerialized = new SerializedObject(flow);
            flowSerialized.FindProperty("travelSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(TravelSoundPath);
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();

            var mainCamera = new GameObject("Main Camera") { tag = "MainCamera" };
            mainCamera.transform.SetParent(rig.transform, false);
            var camera = mainCamera.AddComponent<Camera>();
            // 壁際までカメラが寄るので、既定の 0.3 だと壁が透ける。
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            mainCamera.AddComponent<CinemachineBrain>();
            mainCamera.AddComponent<AudioListener>();

            var followObject = (GameObject)PrefabUtility.InstantiatePrefab(followCameraPrefab, rig.transform);
            followObject.name = "PlayerFollowCamera";
            var follow = followObject.GetComponent<CinemachineCamera>();

            var partyObject = (GameObject)PrefabUtility.InstantiatePrefab(partyPrefab, rig.transform);
            partyObject.name = "Party";

            var members = new List<PartyMember>();
            for (int i = 0; i <= Followers; i++)
            {
                var character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, rig.transform);
                character.name = "Character";
                character.transform.SetPositionAndRotation(spawn.position - spawn.forward * (1.6f * i), spawn.rotation);
                members.Add(PartySceneSetup.Configure(character, i));
            }

            var party = partyObject.GetComponent<PartyGroup>();
            var serializedParty = new SerializedObject(party);
            SerializedProperty list = serializedParty.FindProperty("members");
            list.arraySize = members.Count;
            for (int i = 0; i < members.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = members[i];
            serializedParty.FindProperty("followCamera").objectReferenceValue = follow;
            serializedParty.ApplyModifiedPropertiesWithoutUndo();

            if (follow != null && members[0].CameraTarget != null) follow.Target.TrackingTarget = members[0].CameraTarget;
            followObject.transform.position = spawn.position + new Vector3(0f, 1.4f, -4f);

            var serializedFlow = new SerializedObject(flow);
            serializedFlow.FindProperty("party").objectReferenceValue = party;
            serializedFlow.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- NavMesh・Build Settings ---

        /// <summary>地形（[Surface] の下）の当たりから NavMesh を焼き、アセットに書き出してシーンから参照させる。</summary>
        private static void BakeNavMesh(GameObject world)
        {
            var surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = BreakableLayer.Exclude(AllyLayer.Exclude(~0));
            surface.buildHeightMesh = true;
            // 歩ける範囲（村とショートカットのまわり）の見えない壁・森の当たり・焼く範囲を揃える。
            SurfaceWalkArea.Apply(world);

            Physics.SyncTransforms();
            surface.BuildNavMesh();

            string folder = System.IO.Path.GetDirectoryName(NavMeshPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/'), System.IO.Path.GetFileName(folder));

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>Build Settings の先頭（起動したときのシーン）に有効で置く。</summary>
        private static void PutFirstInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
