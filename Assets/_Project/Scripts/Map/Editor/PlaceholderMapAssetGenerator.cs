using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 手作りの部屋テンプレートが揃うまでの間、生成器を動かして確認できるようにするための
    /// プレースホルダ一式（部屋 Prefab・共通パーツ・カタログ・FloorConfig）をコードから作る。
    /// 本物のテンプレートができたらカタログの中身を差し替えればよく、このスクリプトは不要になる。
    /// 何度実行しても同じ結果になる（既存アセットは上書きされる）。
    /// </summary>
    public static class PlaceholderMapAssetGenerator
    {
        private const string RoomsFolder = "Assets/_Project/Prefabs/Rooms/L1";
        private const string PartsFolder = "Assets/_Project/Prefabs/Parts";
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";
        private const string SettingsFolder = "Assets/_Project/Settings/Map";

        private const float CellSize = 5f;
        private const float WallHeight = 5f;
        private const float WallThickness = 0.15f;
        private const float CeilingThickness = 0.2f;

        // 窓の開口。壁の幅と高さに対する比。腰の高さ（下端）と上端を比で持つので、壁の高さを変えても形が保たれる。
        private const float WindowOpeningWidth = 0.5f;
        private const float WindowSill = 0.3f;
        private const float WindowTop = 0.75f;
        private const float FloorThickness = 0.1f;

        // ドアの開口。セル幅と壁高に対する比で持つので、CellSize を変えても通れる幅のまま追従する。
        private const float DoorOpeningWidth = 0.4f;
        private const float DoorOpeningHeight = 0.75f;

        // 扉板の見た目。厚み（メートル）は Door の既定値と揃えて枠より薄くしてある。
        private const float DoorLeafThickness = 0.08f;

        // 開口に張る照準用トリガーの奥行き（メートル。ルートの z スケールは 1 のまま）。
        private const float DoorInteractVolumeDepth = 0.3f;

        private readonly struct RoomSpec
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly RoomTag Tags;
            public readonly bool AllowRotation;
            public readonly float Weight;
            public readonly string MaterialKey;

            public RoomSpec(string name, int width, int height, RoomTag tags, bool allowRotation, float weight, string materialKey)
            {
                Name = name;
                Width = width;
                Height = height;
                Tags = tags;
                AllowRotation = allowRotation;
                Weight = weight;
                MaterialKey = materialKey;
            }
        }

        /// <summary>
        /// 第1層のプレースホルダ部屋。1 セル 5m を 1 部屋の単位として、小さい部屋を組み合わせて
        /// フロアを作る構成にしている。大きい部屋ばかりだと隣に貼り付ける余地が減るので、
        /// 1x1 を厚めにして 2x2 までに抑えてある。
        /// ここの並び順が RoomTemplateCatalog の登録順になる。
        /// </summary>
        private static readonly RoomSpec[] Rooms =
        {
            new RoomSpec("Room_1x1", 1, 1, RoomTag.Normal, true, 1.0f, "Floor"),
            new RoomSpec("Room_1x2", 1, 2, RoomTag.Normal, true, 0.8f, "Floor"),
            new RoomSpec("Room_2x2", 2, 2, RoomTag.Normal, true, 0.5f, "Floor"),
            new RoomSpec("Room_Shop", 1, 1, RoomTag.Shop, true, 0.2f, "Shop"),
            new RoomSpec("Room_Stair", 1, 1, RoomTag.Stair, true, 0.2f, "Stair"),
        };

        [MenuItem("Tools/TPS Dungeon/プレースホルダのマップ一式を生成")]
        public static void Generate()
        {
            EnsureFolder(RoomsFolder);
            EnsureFolder(PartsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(SettingsFolder);

            var materials = new Dictionary<string, Material>
            {
                ["Floor"] = Material("Placeholder_Floor", new Color(0.55f, 0.53f, 0.50f)),
                ["Wall"] = Material("Placeholder_Wall", new Color(0.35f, 0.34f, 0.33f)),
                ["Door"] = Material("Placeholder_Door", new Color(0.65f, 0.35f, 0.15f)),
                ["DoorLeaf"] = Material("Placeholder_DoorLeaf", new Color(0.42f, 0.24f, 0.12f)),
                ["Shop"] = Material("Placeholder_Shop", new Color(0.85f, 0.72f, 0.18f)),
                ["Stair"] = Material("Placeholder_Stair", new Color(0.35f, 0.55f, 0.75f)),
                ["StairUp"] = Material("Placeholder_StairUp", new Color(0.25f, 0.85f, 0.35f)),
                ["StairDown"] = Material("Placeholder_StairDown", new Color(0.95f, 0.45f, 0.15f)),
                ["Ceiling"] = Material("Placeholder_Ceiling", new Color(0.28f, 0.27f, 0.26f)),
            };

            var parts = BuildParts(materials);
            var roomPrefabs = new List<GameObject>();
            foreach (var spec in Rooms) roomPrefabs.Add(BuildRoomPrefab(spec, materials[spec.MaterialKey]));

            var catalog = BuildCatalog(roomPrefabs);
            BuildFloorConfig(catalog, parts);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"プレースホルダのマップ一式を生成した: 部屋 {roomPrefabs.Count} 種類 + 共通パーツ {parts.Count} 種類");
        }

        private static Dictionary<string, GameObject> BuildParts(Dictionary<string, Material> materials)
        {
            var parts = new Dictionary<string, GameObject>
            {
                // 壁は FloorBuilder が localScale の x と y を (cellSize, wallHeight) に上書きするので、
                // ルートは 1x1 の単位で作り、厚みだけ子の z スケールで持たせる。
                // ドアは扉板を回すのでルートはスケールせず、Door.Fit が枠（Frame）だけを同じ大きさにする。
                ["Wall"] = BuildPanelPrefab("Wall_Basic", materials["Wall"]),
                ["Door"] = BuildDoorPrefab("Door_Basic", materials["Door"], materials["DoorLeaf"]),
                ["StairUp"] = BuildMarkerPrefab("Stair_Up", materials["StairUp"], PrimitiveType.Cube),
                ["StairDown"] = BuildMarkerPrefab("Stair_Down", materials["StairDown"], PrimitiveType.Cube),
                ["ShopMarker"] = BuildMarkerPrefab("Shop_Marker", materials["Shop"], PrimitiveType.Cylinder),
                ["Window"] = BuildWindowPrefab("Window_Basic", materials["Wall"]),
                ["Ceiling"] = BuildCeilingPrefab("Ceiling_Basic", materials["Ceiling"]),
            };
            return parts;
        }

        /// <summary>
        /// 窓付きの壁と天井だけを作り直して、今の FloorConfig に差す。
        /// 一式の生成はドア（Animator を後から付けてある）も作り直すので、それを避けたいときに使う。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/天井と窓のパーツを生成")]
        public static void GenerateCeilingAndWindow()
        {
            string path = $"{SettingsFolder}/Floor_L1_Config.asset";
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(path);
            if (config == null)
            {
                Debug.LogError($"{path} が無い。先に「プレースホルダのマップ一式を生成」を実行すること。");
                return;
            }

            EnsureFolder(PartsFolder);
            EnsureFolder(MaterialsFolder);
            var wall = Material("Placeholder_Wall", new Color(0.35f, 0.34f, 0.33f));
            var ceiling = Material("Placeholder_Ceiling", new Color(0.28f, 0.27f, 0.26f));

            config.wallHeight = WallHeight;
            config.windowWallPrefab = BuildWindowPrefab("Window_Basic", wall);
            config.ceilingPrefab = BuildCeilingPrefab("Ceiling_Basic", ceiling);
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            Debug.Log($"天井と窓のパーツを生成して {config.name} に差した（壁の高さ {config.wallHeight} m）");
        }

        /// <summary>
        /// 窓付きの壁。壁と同じくルートが (cellSize, wallHeight, 1) にスケールされる前提で、1x1 単位で
        /// 左右の柱・腰壁・上の壁を組んで真ん中を開ける。開口には見えない当たりを張り、外へ出られないようにする。
        /// </summary>
        private static GameObject BuildWindowPrefab(string name, Material material)
        {
            var root = new GameObject(name);

            float side = (1f - WindowOpeningWidth) * 0.5f;
            float sideOffset = (WindowOpeningWidth + side) * 0.5f;
            AddDoorPiece(root, "PillarLeft", material,
                new Vector3(-sideOffset, 0.5f, 0f), new Vector3(side, 1f, WallThickness));
            AddDoorPiece(root, "PillarRight", material,
                new Vector3(sideOffset, 0.5f, 0f), new Vector3(side, 1f, WallThickness));
            AddDoorPiece(root, "Sill", material,
                new Vector3(0f, WindowSill * 0.5f, 0f), new Vector3(WindowOpeningWidth, WindowSill, WallThickness));
            AddDoorPiece(root, "Lintel", material,
                new Vector3(0f, (1f + WindowTop) * 0.5f, 0f), new Vector3(WindowOpeningWidth, 1f - WindowTop, WallThickness));

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.localPosition = new Vector3(0f, (WindowSill + WindowTop) * 0.5f, 0f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(WindowOpeningWidth, WindowTop - WindowSill, WallThickness);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 天井 1 セル分。ルートの原点が下面の中心で、FloorBuilder が x と z を cellSize にスケールする。
        /// 影は落とさない（部屋の上を全部覆うので、落とすと室内が真っ暗になる）。
        /// </summary>
        private static GameObject BuildCeilingPrefab(string name, Material material)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, CeilingThickness * 0.5f, 0f);
            mesh.transform.localScale = new Vector3(1f, CeilingThickness, 1f);
            Paint(mesh, material);
            mesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>壁・ドア用。ルートのスケールが (cellSize, wallHeight, 1) に上書きされる前提。</summary>
        private static GameObject BuildPanelPrefab(string name, Material material)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            // ルートの原点を床面に置きたいので、板は上方向に半分ずらす。
            mesh.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            mesh.transform.localScale = new Vector3(1f, 1f, WallThickness);
            Paint(mesh, material);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// ドア用。ルートはスケールせず、枠は子の Frame に 1x1 単位で建て、FloorBuilder が Door.Fit で
        /// Frame だけを (cellSize, wallHeight, 1) にスケールする。左右の柱と鴨居で真ん中を開け、
        /// その開口に Door が開け閉めする扉板（Hinge の子）を吊る。開閉の動きはルートの Animator が
        /// DoorAnimatorBuilder の作るクリップで Hinge を回す。
        /// </summary>
        private static GameObject BuildDoorPrefab(string name, Material material, Material leafMaterial)
        {
            var root = new GameObject(name);

            var frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);

            float pillarWidth = (1f - DoorOpeningWidth) * 0.5f;
            float pillarOffsetX = (DoorOpeningWidth + pillarWidth) * 0.5f;
            float lintelHeight = 1f - DoorOpeningHeight;

            AddDoorPiece(frame, "Pillar_L", material,
                new Vector3(-pillarOffsetX, 0.5f, 0f), new Vector3(pillarWidth, 1f, WallThickness));
            AddDoorPiece(frame, "Pillar_R", material,
                new Vector3(pillarOffsetX, 0.5f, 0f), new Vector3(pillarWidth, 1f, WallThickness));
            AddDoorPiece(frame, "Lintel", material,
                new Vector3(0f, DoorOpeningHeight + lintelHeight * 0.5f, 0f),
                new Vector3(DoorOpeningWidth, lintelHeight, WallThickness));

            // 開けた後に開口を覗いても「閉める」が出るよう、開口いっぱいに照準用のトリガーを張る。
            // 当たり判定は持たないので通行の邪魔はしない。Frame の z スケールは 1 なので奥行きはメートル。
            var volume = new GameObject("InteractVolume");
            volume.transform.SetParent(frame.transform, false);
            volume.transform.localPosition = new Vector3(0f, DoorOpeningHeight * 0.5f, 0f);
            var trigger = volume.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(DoorOpeningWidth, DoorOpeningHeight, DoorInteractVolumeDepth);

            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(hinge.transform, false);
            Paint(leaf, leafMaterial);

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = DoorAnimatorBuilder.Build();

            var door = root.AddComponent<Door>();
            var serialized = new SerializedObject(door);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("frame").objectReferenceValue = frame.transform;
            serialized.FindProperty("hinge").objectReferenceValue = hinge.transform;
            serialized.FindProperty("leaf").objectReferenceValue = leaf.transform;
            serialized.FindProperty("openingWidth").floatValue = DoorOpeningWidth;
            serialized.FindProperty("openingHeight").floatValue = DoorOpeningHeight;
            serialized.FindProperty("leafThickness").floatValue = DoorLeafThickness;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // プレハブ単体で見ても実寸のドアになるよう、既定のセルと壁の大きさに合わせておく。
            door.Fit(CellSize, WallHeight);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        private static void AddDoorPiece(
            GameObject root, string name, Material material, Vector3 localPosition, Vector3 localScale)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            Paint(piece, material);
        }

        private static GameObject BuildMarkerPrefab(string name, Material material, PrimitiveType primitive)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(primitive);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            mesh.transform.localScale = new Vector3(1.4f, primitive == PrimitiveType.Cylinder ? 0.75f : 1.5f, 1.4f);
            Paint(mesh, material);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 部屋 Prefab。床とドア候補だけを持ち、外周壁は持たない（FloorBuilder が建てる）。
        /// ローカル原点はセル (0,0) の角。
        /// </summary>
        private static GameObject BuildRoomPrefab(RoomSpec spec, Material material)
        {
            var root = new GameObject(spec.Name);
            var template = root.AddComponent<RoomTemplate>();
            template.Configure(new Vector2Int(spec.Width, spec.Height), CellSize, spec.Tags, spec.AllowRotation, spec.Weight);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(
                spec.Width * CellSize * 0.5f, -FloorThickness * 0.5f, spec.Height * CellSize * 0.5f);
            floor.transform.localScale = new Vector3(spec.Width * CellSize, FloorThickness, spec.Height * CellSize);
            Paint(floor, material);

            var socketsRoot = new GameObject("DoorSockets");
            socketsRoot.transform.SetParent(root.transform, false);

            foreach (var data in RoomTemplateFactory.CreateDefaultSockets(spec.Width, spec.Height))
            {
                var socketObject = new GameObject($"Socket_{data.Facing}_{data.LocalCell.X}_{data.LocalCell.Y}");
                socketObject.transform.SetParent(socketsRoot.transform, false);

                var socket = socketObject.AddComponent<DoorSocket>();
                socket.Configure(new Vector2Int(data.LocalCell.X, data.LocalCell.Y), data.Facing);
                socket.SnapToCell(CellSize);
            }

            return SavePrefab(root, $"{RoomsFolder}/{spec.Name}.prefab");
        }

        private static RoomTemplateCatalog BuildCatalog(List<GameObject> roomPrefabs)
        {
            string path = $"{SettingsFolder}/L1_RoomCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<RoomTemplateCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RoomTemplateCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            var entries = new List<RoomTemplateCatalog.Entry>();
            foreach (var prefab in roomPrefabs) entries.Add(new RoomTemplateCatalog.Entry { prefab = prefab });
            catalog.SetEntries(entries);

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static FloorConfig BuildFloorConfig(RoomTemplateCatalog catalog, Dictionary<string, GameObject> parts)
        {
            string path = $"{SettingsFolder}/Floor_L1_Config.asset";
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FloorConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            config.widthInCells = 64;
            config.heightInCells = 64;
            config.cellSize = CellSize;
            config.wallHeight = WallHeight;
            config.minRoomCount = 10;
            config.maxRoomCount = 20;
            config.maxPackAttempts = 6;
            config.startJitter = 4;
            config.extraDoorCount = 2;
            config.includeShop = true;
            config.roomCatalog = catalog;
            config.wallPrefab = parts["Wall"];
            config.doorPrefab = parts["Door"];
            config.stairUpPrefab = parts["StairUp"];
            config.stairDownPrefab = parts["StairDown"];
            config.shopMarkerPrefab = parts["ShopMarker"];
            config.windowWallPrefab = parts["Window"];
            config.ceilingPrefab = parts["Ceiling"];

            EditorUtility.SetDirty(config);
            return config;
        }

        private static Material Material(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(DefaultShader());
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader DefaultShader()
        {
            // このプロジェクトは URP なので Lit を使う。見つからないときだけ組み込みにフォールバックする。
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? shader : Shader.Find("Standard");
        }

        private static void Paint(GameObject target, Material material)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
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
    }
}
