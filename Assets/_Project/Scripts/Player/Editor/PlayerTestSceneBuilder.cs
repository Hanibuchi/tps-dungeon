using System.Collections.Generic;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.DebugTools;
using TpsDungeon.Map.Generation;
using TpsDungeon.Map.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Player.Editor
{
    /// <summary>
    /// TPS のキャラクター操作とカメラを確かめるためのシーンを組み立てる。
    /// マップは既存のマップ生成にそのまま任せ、ここではプレイヤーとカメラの器だけを置く。
    /// 生成物なので手で編集せず、構成を変えたくなったらこのファイルを直して作り直すこと。
    /// </summary>
    public static class PlayerTestSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Player_Debug.unity";

        internal const string ConfigPath = "Assets/_Project/Settings/Map/Floor_L1_Config.asset";
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";
        private const string PlayerMaterialPath = MaterialsFolder + "/Placeholder_Player.mat";

        /// <summary>
        /// シードを 0 にすると FloorBootstrap が毎回引き直すので、固定配置のプレイヤーが壁の中や場外に出る。
        /// 固定しておけば、同じシードのレイアウトを生成器側で先に計算してスポーン地点を焼き込める。
        /// スポーンが気に入らなければこの値を変えて作り直すこと。
        /// </summary>
        private const int FloorSeed = 20260922;

        private const float PlayerHeight = 2f;
        private const float PlayerRadius = 0.35f;

        /// <summary>肩越しの注視点の高さ。目線より少し下に置くと足元も画に入る。</summary>
        private const float CameraTargetHeight = 1.4f;

        /// <summary>床の上面がちょうど y=0 なので、初期状態でめり込まないように少し浮かせる。</summary>
        private const float SpawnLift = 0.05f;

        [MenuItem("Tools/TPS Dungeon/Player/Generate Player Test Scene")]
        public static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        /// <summary>確認用シーンを作り直して、何をしたかのログを返す。</summary>
        public static string Generate()
        {
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(ConfigPath);
            if (config == null)
            {
                return "先に Tools/TPS Dungeon/プレースホルダのマップ一式を生成 を実行すること。設定が無い: "
                       + ConfigPath;
            }

            if (!TryFindSpawn(config, out Vector3 spawn, out string spawnNote))
            {
                return "スポーン地点を決められなかったのでシーンは作っていない。" + spawnNote;
            }

            Material playerMaterial = EnsurePlayerMaterial();

            // 追加ロードで作って保存後に閉じる。いま開いているシーンの未保存の変更を巻き込まないため。
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                CreateLighting();
                FloorBootstrap bootstrap = CreateFloorRoot(config);
                Transform cameraTarget = CreatePlayer(spawn, playerMaterial);
                CreateCameraRig(cameraTarget);
                CreateTopDownDebugCamera(bootstrap);

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
            }
            finally
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            return "プレイヤー確認用シーンを作り直した: " + ScenePath
                   + $"\n  シード {FloorSeed} / スポーン {spawn}（{spawnNote}）"
                   + "\n  移動とカメラ操作のスクリプトはまだ無いので、Play してもカプセルは動かないのが正常。"
                   + "\n  俯瞰で確かめたいときは Main Camera を無効にして [Debug] TopDownCamera を有効にすること。";
        }

        /// <summary>
        /// 上り階段の部屋にスポーン地点を取る。
        /// FeatureCell の中心には Stair_Up のマーカー（1.4 角のコライダー）が立つ。
        /// マーカー半幅 0.7 + カプセル半径 0.35 より外、セル半幅より内に収まる位置へ横にずらす。
        /// </summary>
        private static bool TryFindSpawn(FloorConfig config, out Vector3 spawn, out string note)
        {
            spawn = Vector3.zero;
            if (!TryFindEntrance(config, out Vector3 center, out note)) return false;

            spawn = center + new Vector3(config.cellSize * 0.3f, SpawnLift, 0f);
            return true;
        }

        /// <summary>
        /// ランタイムと同じシードでレイアウトを先に計算し、階の入口（上り階段の部屋の FeatureCell）の中心を返す。
        /// マップの Data/Generation 層は UnityEngine に依存しないので、エディタ側でそのまま回せる。
        /// [Floor] を原点に置くので、返す座標はワールド座標と一致する。
        /// </summary>
        internal static bool TryFindEntrance(FloorConfig config, out Vector3 center, out string note)
        {
            center = Vector3.zero;

            FloorLayout layout = FloorLayoutGenerator.Generate(config.BuildParams(), FloorSeed);
            if (layout == null || layout.Rooms.Count == 0)
            {
                note = $"シード {FloorSeed} では部屋が 1 つも置けなかった。FloorSeed を変えること。";
                return false;
            }

            // 上り階段の部屋が階の入口。役割が割り当たらなかったときだけ先頭の部屋で妥協する。
            int index = layout.StairUpRoom >= 0 ? layout.StairUpRoom : 0;
            RoomInstance room = layout.RoomAt(index);
            if (room == null)
            {
                note = $"シード {FloorSeed} の部屋 {index} を引けなかった。FloorSeed を変えること。";
                return false;
            }

            GridPos cell = room.FeatureCell;

            // FloorBuilder.CellCenter と同じ式。[Floor] を原点に置くのでローカルとワールドが一致する。
            center = new Vector3(
                (cell.X + 0.5f) * config.cellSize, 0f, (cell.Y + 0.5f) * config.cellSize);

            note = layout.StairUpRoom >= 0
                ? $"上り階段の部屋 {index} / セル ({cell.X}, {cell.Y})"
                : $"上り階段が割り当たらなかったので部屋 {index} / セル ({cell.X}, {cell.Y})";
            return true;
        }

        private static void CreateLighting()
        {
            var light = new GameObject("Directional Light");
            var component = light.AddComponent<Light>();
            component.type = LightType.Directional;
            component.intensity = 1.1f;
            component.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // プレースホルダが黒く潰れると位置関係が読めないので、環境光は明るめにしておく。
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        private static FloorBootstrap CreateFloorRoot(FloorConfig config)
        {
            var root = new GameObject("[Floor]");
            root.AddComponent<FloorBuilder>();
            var bootstrap = root.AddComponent<FloorBootstrap>();

            // private [SerializeField] なので SerializedObject 経由で差し込む。
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("seed").intValue = FloorSeed;
            serialized.FindProperty("generateOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return bootstrap;
        }

        /// <summary>カメラが追う注視点の Transform を返す。</summary>
        private static Transform CreatePlayer(Vector3 spawn, Material material)
        {
            var player = new GameObject("[Player]") { tag = "Player" };
            player.transform.position = spawn;

            var controller = player.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);

            // 見た目だけのカプセル。当たり判定は CharacterController が持つので Collider は外す。
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = "Mesh";
            mesh.transform.SetParent(player.transform, false);
            mesh.transform.localPosition = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            mesh.transform.localScale =
                new Vector3(PlayerRadius * 2f, PlayerHeight * 0.5f, PlayerRadius * 2f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;

            var target = new GameObject("CameraTarget");
            target.transform.SetParent(player.transform, false);
            target.transform.localPosition = new Vector3(0f, CameraTargetHeight, 0f);
            return target.transform;
        }

        /// <summary>
        /// Cinemachine 3 系なので CinemachineCamera（2 系の CinemachineVirtualCamera ではない）を使う。
        /// 向きを回すのはキャラクター側の役目なので、ここでは入力を読むコンポーネントは付けない。
        /// </summary>
        private static void CreateCameraRig(Transform cameraTarget)
        {
            var mainCameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = mainCameraObject.AddComponent<Camera>();

            // 壁際までカメラが寄るので、既定の 0.3 だと壁が透ける。
            camera.nearClipPlane = 0.1f;
            mainCameraObject.AddComponent<CinemachineBrain>();
            mainCameraObject.AddComponent<AudioListener>();

            var rigObject = new GameObject("CM ThirdPerson");
            var rig = rigObject.AddComponent<CinemachineCamera>();

            // ThirdPersonFollow は LookAt を見ないので、追従先だけ差せばよい。
            rig.Target.TrackingTarget = cameraTarget;

            var body = rigObject.AddComponent<CinemachineThirdPersonFollow>();
            body.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
            body.VerticalArmLength = 0.4f;
            body.CameraSide = 1f;
            body.CameraDistance = 4f;
            body.Damping = new Vector3(0.1f, 0.5f, 0.3f);

            // 既定は off だが、ダンジョンは壁だらけでカメラが壁に埋まるので入れておく。
            body.AvoidObstacles.Enabled = true;
        }

        /// <summary>
        /// レイアウト全体を俯瞰で確かめるための予備カメラ。普段は TPS カメラで見るので無効にしておく。
        /// MainCamera タグと AudioListener は Main Camera 側に付けたので、こちらには付けない。
        /// </summary>
        private static void CreateTopDownDebugCamera(FloorBootstrap bootstrap)
        {
            var cameraObject = new GameObject("[Debug] TopDownCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var view = cameraObject.AddComponent<FloorDebugView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("topDownCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            cameraObject.SetActive(false);
        }

        private static Material EnsurePlayerMaterial()
        {
            EnsureFolder(MaterialsFolder);

            var material = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
            if (material == null)
            {
                material = new Material(DefaultShader());
                AssetDatabase.CreateAsset(material, PlayerMaterialPath);
            }

            // 床・壁・階段のプレースホルダと見分けがつく緑にする。
            var color = new Color(0.2f, 0.75f, 0.55f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Shader DefaultShader()
        {
            // このプロジェクトは URP なので Lit を使う。見つからないときだけ組み込みにフォールバックする。
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? shader : Shader.Find("Standard");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Build Settings に足す。ただし無効で登録する。
        /// 確認用のシーンを製品のビルドに含めたくないが、一覧からは見えるようにしておきたい。
        /// Project ウィンドウから開く分には Build Settings と関係ないので手動確認には支障がない。
        /// </summary>
        private static void AddToBuildSettings(string scenePath)
        {
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == scenePath) return;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(scenePath, false),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
