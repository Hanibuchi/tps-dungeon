using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
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
        // 高い建物の上の階なので、小さめの窓を上の方に開ける（5 m の壁なら幅 1.25 m、高さ 3.0〜4.0 m）。
        private const float WindowOpeningWidth = 0.25f;
        private const float WindowSill = 0.6f;
        private const float WindowTop = 0.8f;
        private const float Elevation = 40f;
        private const float FloorThickness = 0.1f;

        // ドアの開口。セル幅と壁高に対する比で持つので、CellSize を変えても通れる幅のまま追従する。
        // 5 m のセル・5 m の壁なら幅 1.5 m、高さ 2.8 m ほど。
        private const float DoorOpeningWidth = 0.3f;
        private const float DoorOpeningHeight = 0.5625f;

        // 扉板の厚みと、枠との隙間（メートル）。枠より薄くして、閉めたときに枠と重ならないようにする。
        private const float DoorLeafThickness = 0.08f;
        private const float DoorLeafGap = 0.02f;

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
                // 壁・窓・天井・土台は ProBuilder で実寸に作り、FloorBuilder はスケールしない（素材の模様の密度を揃えるため）。
                ["Wall"] = BuildWallPrefab("Wall_Basic", materials["Wall"], CellSize, WallHeight),
                ["Door"] = BuildDoorPrefab("Door_Basic", materials["Door"], materials["DoorLeaf"], CellSize, WallHeight),
                ["StairUp"] = BuildMarkerPrefab("Stair_Up", materials["StairUp"], PrimitiveType.Cube),
                ["StairDown"] = BuildMarkerPrefab("Stair_Down", materials["StairDown"], PrimitiveType.Cube),
                ["ShopMarker"] = BuildMarkerPrefab("Shop_Marker", materials["Shop"], PrimitiveType.Cylinder),
                ["Window"] = BuildWindowPrefab("Window_Basic", materials["Wall"], CellSize, WallHeight),
                ["Ceiling"] = BuildCeilingPrefab("Ceiling_Basic", materials["Ceiling"], CellSize),
                ["Foundation"] = BuildFoundationPrefab("Foundation_Basic", materials["Wall"], CellSize, Elevation),
            };
            return parts;
        }

        /// <summary>
        /// 壁・窓付きの壁・ドア・天井・土台と、部屋の床を、今の FloorConfig の寸法（cellSize・wallHeight・elevation）で
        /// ProBuilder の実寸の箱に作り直す。FloorConfig の高さを変えたらこれを回す。
        /// 一式の生成と違い、部屋の並びやカタログ・FloorConfig の他の値には触らない。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/壁・床・天井のパーツを生成")]
        public static void GenerateStructureParts()
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
            // 一式の生成で作ってある素材はそのまま使う（作り直すと色の値が丸めで書き換わる）。
            var wall = ExistingMaterial("Placeholder_Wall", new Color(0.35f, 0.34f, 0.33f));
            var ceiling = ExistingMaterial("Placeholder_Ceiling", new Color(0.28f, 0.27f, 0.26f));
            var doorFrame = ExistingMaterial("Placeholder_Door", new Color(0.65f, 0.35f, 0.15f));
            var doorLeaf = ExistingMaterial("Placeholder_DoorLeaf", new Color(0.42f, 0.24f, 0.12f));

            config.wallPrefab = BuildWallPrefab("Wall_Basic", wall, config.cellSize, config.wallHeight);
            config.windowWallPrefab = BuildWindowPrefab("Window_Basic", wall, config.cellSize, config.wallHeight);
            config.doorPrefab = BuildDoorPrefab("Door_Basic", doorFrame, doorLeaf, config.cellSize, config.wallHeight, reuseAnimator: true);
            config.ceilingPrefab = BuildCeilingPrefab("Ceiling_Basic", ceiling, config.cellSize);
            config.foundationPrefab = BuildFoundationPrefab("Foundation_Basic", wall, config.cellSize, config.elevation);
            EditorUtility.SetDirty(config);

            int floors = 0;
            if (config.roomCatalog != null)
            {
                foreach (var entry in config.roomCatalog.Entries)
                {
                    if (entry.prefab != null && RebuildRoomFloor(entry.prefab, config.cellSize)) floors++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"壁・ドア・床・天井のパーツを ProBuilder で作り直して {config.name} に差した（セル {config.cellSize} m、壁の高さ {config.wallHeight} m、土台 {config.elevation} m、作り直した部屋の床 {floors} 個）");
        }

        private static Material ExistingMaterial(string name, Color color) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat") ?? Material(name, color);

        /// <summary>壁 1 セル分。原点が壁の下端の中心で、幅 width・高さ height の実寸。</summary>
        private static GameObject BuildWallPrefab(string name, Material material, float width, float height)
        {
            var root = new GameObject(name);
            Box(root.transform, "Mesh", material, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, WallThickness), true);
            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 窓付きの壁。壁と同じ実寸で、左右の柱・腰壁・上の壁を組んで真ん中を開ける。
        /// 開口には見えない当たりを張り、外へ出られないようにする。
        /// </summary>
        private static GameObject BuildWindowPrefab(string name, Material material, float width, float height)
        {
            var root = new GameObject(name);

            float openW = WindowOpeningWidth * width;
            float sill = WindowSill * height;
            float top = WindowTop * height;
            float side = (width - openW) * 0.5f;
            float sideOffset = (openW + side) * 0.5f;
            Box(root.transform, "PillarLeft", material,
                new Vector3(-sideOffset, height * 0.5f, 0f), new Vector3(side, height, WallThickness), true);
            Box(root.transform, "PillarRight", material,
                new Vector3(sideOffset, height * 0.5f, 0f), new Vector3(side, height, WallThickness), true);
            Box(root.transform, "Sill", material,
                new Vector3(0f, sill * 0.5f, 0f), new Vector3(openW, sill, WallThickness), true);
            Box(root.transform, "Lintel", material,
                new Vector3(0f, (height + top) * 0.5f, 0f), new Vector3(openW, height - top, WallThickness), true);

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.localPosition = new Vector3(0f, (sill + top) * 0.5f, 0f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(openW, top - sill, WallThickness);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 土台 1 セル分。原点が上面の中心で、下へ depth + 1 m（地面の凹凸に隠れるよう少し深め）。
        /// 窓の外に見せるだけなので当たりは持たせない。
        /// </summary>
        private static GameObject BuildFoundationPrefab(string name, Material material, float width, float depth)
        {
            var root = new GameObject(name);
            float d = depth + 1f;
            Box(root.transform, "Mesh", material, new Vector3(0f, -d * 0.5f, 0f), new Vector3(width, d, width), false);
            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 天井 1 セル分。原点が下面の中心。
        /// 影は落とさない（部屋の上を全部覆うので、落とすと室内が真っ暗になる）。
        /// </summary>
        private static GameObject BuildCeilingPrefab(string name, Material material, float width)
        {
            var root = new GameObject(name);
            var mesh = Box(root.transform, "Mesh", material,
                new Vector3(0f, CeilingThickness * 0.5f, 0f), new Vector3(width, CeilingThickness, width), true);
            mesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>部屋 Prefab の床（子の Floor）を、今の大きさの ProBuilder の箱に差し替える。</summary>
        private static bool RebuildRoomFloor(GameObject prefab, float cellSize)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var template = root.GetComponent<RoomTemplate>();
                var old = root.transform.Find("Floor");
                if (template == null || old == null) return false;

                // もう同じ大きさの ProBuilder の床なら触らない（作り直すと中の ID が変わって差分だけが出る）。
                var size = new Vector3(template.SizeInCells.x * cellSize, FloorThickness, template.SizeInCells.y * cellSize);
                if (old.GetComponent<ProBuilderMesh>() != null && old.TryGetComponent(out BoxCollider current)
                    && Vector3.Distance(current.size, size) < 0.001f && old.localScale == Vector3.one) return false;

                var material = old.GetComponent<MeshRenderer>() != null ? old.GetComponent<MeshRenderer>().sharedMaterial : null;
                int index = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);

                var floor = AddRoomFloor(root, template.SizeInCells.x, template.SizeInCells.y, cellSize, material);
                floor.transform.SetSiblingIndex(index);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>部屋の床。原点（セル (0,0) の角）から幅 width × 奥行き height セル、上面が y = 0。</summary>
        private static GameObject AddRoomFloor(GameObject room, int width, int height, float cellSize, Material material)
        {
            return Box(room.transform, "Floor", material,
                new Vector3(width * cellSize * 0.5f, -FloorThickness * 0.5f, height * cellSize * 0.5f),
                new Vector3(width * cellSize, FloorThickness, height * cellSize), true);
        }

        /// <summary>
        /// 中心 center・大きさ size の ProBuilder の箱。スケールせず頂点で大きさを持つので、UV は 1 m = 1 の割合で
        /// どの面も同じ密度になる（素材を貼っても模様が伸びない）。当たりは同じ大きさの BoxCollider。
        /// </summary>
        private static GameObject Box(Transform parent, string name, Material material, Vector3 center, Vector3 size, bool collider)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = center;

            if (material != null) mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();

            if (collider) mesh.gameObject.AddComponent<BoxCollider>().size = size;
            return mesh.gameObject;
        }

        /// <summary>
        /// ドア 1 セル分。壁と同じく幅 width・高さ height の実寸で、どこもスケールしない。
        /// 左右の柱と鴨居（子の Frame）で真ん中を開け、その開口に扉板（Hinge の子の Leaf）を吊る。
        /// 開閉の動きはルートの Animator が DoorAnimatorBuilder の作るクリップで Hinge を回す。
        /// </summary>
        private static GameObject BuildDoorPrefab(string name, Material material, Material leafMaterial, float width, float height,
            bool reuseAnimator = false)
        {
            var root = new GameObject(name);

            var frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);

            float openW = DoorOpeningWidth * width;
            float openH = DoorOpeningHeight * height;
            float pillarWidth = (width - openW) * 0.5f;
            float pillarOffsetX = (openW + pillarWidth) * 0.5f;
            float lintelHeight = height - openH;

            Box(frame.transform, "Pillar_L", material,
                new Vector3(-pillarOffsetX, height * 0.5f, 0f), new Vector3(pillarWidth, height, WallThickness), true);
            Box(frame.transform, "Pillar_R", material,
                new Vector3(pillarOffsetX, height * 0.5f, 0f), new Vector3(pillarWidth, height, WallThickness), true);
            Box(frame.transform, "Lintel", material,
                new Vector3(0f, openH + lintelHeight * 0.5f, 0f), new Vector3(openW, lintelHeight, WallThickness), true);

            // 開けた後に開口を覗いても「閉める」が出るよう、開口いっぱいに照準用のトリガーを張る。
            // 当たり判定は持たないので通行の邪魔はしない。
            var volume = new GameObject("InteractVolume");
            volume.transform.SetParent(frame.transform, false);
            volume.transform.localPosition = new Vector3(0f, openH * 0.5f, 0f);
            var trigger = volume.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(openW, openH, DoorInteractVolumeDepth);

            // 回す軸は開口の左端。扉板は軸から右へ開口いっぱい（枠との隙間だけ小さく）。
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(-openW * 0.5f, 0f, 0f);

            var leafSize = new Vector3(openW - DoorLeafGap * 2f, openH - DoorLeafGap, DoorLeafThickness);
            var leaf = Box(hinge.transform, "Leaf", leafMaterial,
                new Vector3(openW * 0.5f, openH * 0.5f, 0f), leafSize, true);

            var animator = root.AddComponent<Animator>();
            // コントローラは組み直すと中の ID が全部変わるので、寸法だけ作り直すときは今のものを使う。
            var controller = reuseAnimator
                ? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DoorAnimatorBuilder.ControllerPath)
                : null;
            animator.runtimeAnimatorController = controller != null ? controller : DoorAnimatorBuilder.Build();

            var door = root.AddComponent<Door>();
            var serialized = new SerializedObject(door);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("hinge").objectReferenceValue = hinge.transform;
            serialized.FindProperty("leaf").objectReferenceValue = leaf.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
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

            AddRoomFloor(root, spec.Width, spec.Height, CellSize, material);

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
            config.elevation = Elevation;
            config.foundationPrefab = parts["Foundation"];

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
