using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

        // ドアノブ。床からの高さ、扉板の端（蝶番の反対）からの距離、首の長さと太さ、玉の直径（メートル）。
        private const float KnobHeight = 1.0f;
        private const float KnobInset = 0.14f;
        private const float KnobNeckLength = 0.05f;
        private const float KnobNeckWidth = 0.03f;
        private const float KnobSize = 0.08f;

        // 開け閉めの音（効果音ラボ）。ThirdParty は git に上げないので、無ければ空のまま（鳴らない）。
        private const string DoorOpenSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/ドアを開ける2.mp3";
        private const string DoorCloseSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/ドアを閉める2.mp3";

        // 開口に張る照準用トリガーの奥行き（メートル。ルートの z スケールは 1 のまま）。
        private const float DoorInteractVolumeDepth = 0.3f;

        // 階段。入口の下り・出口の上りとも、ドアから入って正面へ +Z に真っ直ぐ伸びる 1 本の階段で、段は飾り（登り降りはしない）。
        // 階段の部屋はドアを南（-Z）の 1 面にだけ開けるので、ドアの真正面（x = 0）から奥の北の壁際まで伸ばす。
        // 階段の幅（X）、下りの 1 段の奥行きと高さ、上りの 1 段の高さ（メートル）。上りの奥行きは天井まで届く段数で割り付けるので急になる（5 m の壁で 0.16 m）。
        private const float StairWidth = 1.4f;
        private const float StairDownRun = 0.3f;
        private const float StairDownRise = 0.2f;
        private const float StairUpRise = 0.25f;
        // 階段と奥の壁の内側との隙間（壁の厚みの半分＋少し）。
        private const float StairWallClearance = 0.2f;
        // 階段の始まる所の、ドアの壁からの距離。扉板（1.5 m）が部屋の内へ開いても段や手すりに当たらないよう、それより少し先から。
        private const float StairDoorClearance = 1.6f;
        // 上り階段の天井の穴の、ドア側の端（セルの中心からの Z）。
        private const float StairUpOpeningStart = 0.4f;
        // 下り階段の竪穴の深さ、上り階段の天井より上の竪穴の高さ、下り階段の穴の脇の手すりの高さ。
        private const float StairwellDepth = 3f;
        private const float StairUpShaft = 2f;
        private const float RailingHeight = 1f;
        // 手すりの柱・横木の太さと、柱の間隔の目安。
        private const float RailingBar = 0.08f;
        private const float RailingPostSpacing = 1f;
        // 入口に着いたパーティーの立ち位置（階段のローカル）。階段の脇（東）の、少しドア寄りで、ドアの方を向く。
        private static readonly Vector3 StairSpawnPosition = new Vector3(1.6f, 0.05f, -0.3f);

        private readonly struct RoomSpec
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly RoomTag Tags;
            public readonly bool AllowRotation;
            public readonly float Weight;
            public readonly string MaterialKey;
            /// <summary>ドア候補を打つ辺。null なら外周全部。</summary>
            public readonly Direction? DoorSide;

            public RoomSpec(string name, int width, int height, RoomTag tags, bool allowRotation, float weight, string materialKey,
                Direction? doorSide = null)
            {
                Name = name;
                Width = width;
                Height = height;
                Tags = tags;
                AllowRotation = allowRotation;
                Weight = weight;
                MaterialKey = materialKey;
                DoorSide = doorSide;
            }

            public List<DoorSocketData> Sockets() => DoorSide.HasValue
                ? RoomTemplateFactory.CreateSocketsOnSide(Width, Height, DoorSide.Value)
                : RoomTemplateFactory.CreateDefaultSockets(Width, Height);
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
            // 入口・出口になる部屋。ドアは南の 1 面だけにして入りやすくする（階段は北の壁沿いに置かれる）。
            new RoomSpec("Room_Stair", 1, 1, RoomTag.Stair, true, 0.2f, "Stair", Direction.South),
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
                ["DoorKnob"] = KnobMaterial(),
                ["Shop"] = Material("Placeholder_Shop", new Color(0.85f, 0.72f, 0.18f)),
                ["Stair"] = Material("Placeholder_Stair", new Color(0.35f, 0.55f, 0.75f)),
                ["Ceiling"] = Material("Placeholder_Ceiling", new Color(0.28f, 0.27f, 0.26f)),
                ["Void"] = Material("Placeholder_Void", new Color(0.03f, 0.03f, 0.035f)),
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
                ["Door"] = BuildDoorPrefab("Door_Basic", materials["Door"], materials["DoorLeaf"], materials["DoorKnob"], CellSize, WallHeight),
                ["StairUp"] = BuildStairUpPrefab("Stair_Up", StairMaterials.From(materials), CellSize, WallHeight),
                ["StairDown"] = BuildStairDownPrefab("Stair_Down", StairMaterials.From(materials), CellSize),
                ["ShopMarker"] = BuildMarkerPrefab("Shop_Marker", materials["Shop"], PrimitiveType.Cylinder),
                ["Window"] = BuildWindowPrefab("Window_Basic", materials["Wall"], CellSize, WallHeight),
                ["Ceiling"] = BuildCeilingPrefab("Ceiling_Basic", materials["Ceiling"], CellSize),
                ["Foundation"] = BuildFoundationPrefab("Foundation_Basic", materials["Wall"], CellSize, Elevation),
            };
            return parts;
        }

        /// <summary>
        /// 壁・窓付きの壁・ドア・天井・土台・階段と、部屋の床・ドア候補を、今の FloorConfig の寸法（cellSize・wallHeight・elevation）で
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
            var doorKnob = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Placeholder_DoorKnob.mat") ?? KnobMaterial();

            config.wallPrefab = BuildWallPrefab("Wall_Basic", wall, config.cellSize, config.wallHeight);
            config.windowWallPrefab = BuildWindowPrefab("Window_Basic", wall, config.cellSize, config.wallHeight);
            config.doorPrefab = BuildDoorPrefab("Door_Basic", doorFrame, doorLeaf, doorKnob, config.cellSize, config.wallHeight, reuseAnimator: true);
            config.ceilingPrefab = BuildCeilingPrefab("Ceiling_Basic", ceiling, config.cellSize);
            config.foundationPrefab = BuildFoundationPrefab("Foundation_Basic", wall, config.cellSize, config.elevation);
            var stairs = new StairMaterials
            {
                Room = ExistingMaterial("Placeholder_Stair", new Color(0.35f, 0.55f, 0.75f)),
                Steps = ExistingMaterial("Placeholder_Floor", new Color(0.55f, 0.53f, 0.50f)),
                Wall = wall,
                Ceiling = ceiling,
                Void = ExistingMaterial("Placeholder_Void", new Color(0.03f, 0.03f, 0.035f)),
            };
            config.stairUpPrefab = BuildStairUpPrefab("Stair_Up", stairs, config.cellSize, config.wallHeight);
            config.stairDownPrefab = BuildStairDownPrefab("Stair_Down", stairs, config.cellSize);
            if (config.volumeProfile == null) config.volumeProfile = LoadOrCreateVolumeProfile();
            EditorUtility.SetDirty(config);

            int floors = 0;
            int sockets = 0;
            if (config.roomCatalog != null)
            {
                foreach (var entry in config.roomCatalog.Entries)
                {
                    if (entry.prefab != null && RebuildRoomFloor(entry.prefab, config.cellSize)) floors++;
                    if (entry.prefab != null && SyncRoomSockets(entry.prefab, config.cellSize)) sockets++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"壁・ドア・床・天井・階段のパーツを ProBuilder で作り直して {config.name} に差した（セル {config.cellSize} m、壁の高さ {config.wallHeight} m、土台 {config.elevation} m、作り直した部屋の床 {floors} 個、ドア候補を揃えた部屋 {sockets} 個）");
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
        /// 日の光が室内へ抜けないよう影を落とす（裏表とも）。室内の暗さは階のシーンの Global Volume で持ち上げる。
        /// </summary>
        private static GameObject BuildCeilingPrefab(string name, Material material, float width)
        {
            var root = new GameObject(name);
            var mesh = Box(root.transform, "Mesh", material,
                new Vector3(0f, CeilingThickness * 0.5f, 0f), new Vector3(width, CeilingThickness, width), true);
            mesh.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>階段に使う素材。部屋の床（Room）は階段の部屋の床と同じ色にして、置き換えた床の継ぎ目を見せない。</summary>
        private sealed class StairMaterials
        {
            public Material Room;
            public Material Steps;
            public Material Wall;
            public Material Ceiling;
            public Material Void;

            public static StairMaterials From(Dictionary<string, Material> materials) => new StairMaterials
            {
                Room = materials["Stair"],
                Steps = materials["Floor"],
                Wall = materials["Wall"],
                Ceiling = materials["Ceiling"],
                Void = materials["Void"],
            };
        }

        /// <summary>
        /// 入口に置く、下の階へ続く下り階段。原点がセルの中心の床の高さで、ドアは -Z の側（階段の部屋の南）。
        /// 部屋の床の代わりに、ドアの正面から奥の壁まで細長い穴の空いた床を持ち（FloorFeature.replacesRoomFloor）、
        /// ドアから入ってそのまま +Z へ真っ直ぐ下りる階段を穴の下の竪穴に置く。
        /// 穴の両脇には手すり、降り口（ドア側）は開けたままにして、穴の上の見えない当たりで落ちないようにする。
        /// 竪穴の奥（北）の壁は暗くして、階段がその先へ続いて見えるようにする。
        /// 調べると下の階へ移る（子の Stairwell の SceneGate。行き先は FloorBootstrap が入れる）。子の SpawnPoint に入口に着いたパーティーが立つ。
        /// </summary>
        private static GameObject BuildStairDownPrefab(string name, StairMaterials m, float cellSize)
        {
            var root = new GameObject(name);
            var feature = root.AddComponent<FloorFeature>();
            feature.replacesRoomFloor = true;
            feature.depthBelowFloor = StairwellDepth + CeilingThickness;

            StairFootprint(cellSize, out var holeMin, out var holeMax);
            var holeCenter = (holeMin + holeMax) * 0.5f;
            var holeSize = holeMax - holeMin;

            HoledSlab(root.transform, "Floor", m.Room, cellSize, holeMin, holeMax, -FloorThickness, FloorThickness, true);
            var shaft = Shaft(root.transform, m.Wall, holeMin, holeMax, -StairwellDepth, -FloorThickness);
            shaft.transform.Find("North").GetComponent<MeshRenderer>().sharedMaterial = m.Void;
            Box(root.transform, "Bottom", m.Void,
                new Vector3(holeCenter.x, -StairwellDepth - CeilingThickness * 0.5f, holeCenter.y),
                new Vector3(holeSize.x + WallThickness * 2f, CeilingThickness, holeSize.y + WallThickness * 2f), false);

            // 調べられるのは穴まわり（段・手すり・当たり）だけ。床を見て「下の階へ」が出ないよう、床とは別の子にまとめる。
            var well = new GameObject("Stairwell");
            well.transform.SetParent(root.transform, false);

            int steps = Mathf.FloorToInt(holeSize.y / StairDownRun + 0.001f);
            Flight(well.transform, m.Steps, holeMin.y, holeMin.x, holeMax.x, steps, StairDownRun, -StairDownRise, -StairwellDepth);

            // 手すりは穴の両脇（東西）の長辺。北は部屋の壁、南は降り口。
            // 板にすると下りていく段が隠れるので、柱と横木だけにして透かす（落ちないのは見えない当たりで止める）。
            Railing(well.transform, "Railing_W", m.Wall, holeMin.x - RailingBar * 0.5f, holeMin.y, holeMax.y);
            Railing(well.transform, "Railing_E", m.Wall, holeMax.x + RailingBar * 0.5f, holeMin.y, holeMax.y);

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(well.transform, false);
            blocker.transform.localPosition = new Vector3(holeCenter.x, RailingHeight * 0.75f, holeCenter.y);
            blocker.AddComponent<BoxCollider>().size = new Vector3(holeSize.x, RailingHeight * 1.5f, holeSize.y);

            var gate = well.AddComponent<SceneGate>();
            var serialized = new SerializedObject(gate);
            serialized.FindProperty("openLabel").stringValue = "下の階へ";
            serialized.FindProperty("closedLabel").stringValue = "下へは降りられない";
            serialized.FindProperty("title").stringValue = "下り階段";
            serialized.FindProperty("description").stringValue = "下の階へ続く階段。";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetLocalPositionAndRotation(StairSpawnPosition, Quaternion.Euler(0f, 180f, 0f));

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 出口に置く、上の階へ続く上り階段。原点がセルの中心の床の高さで、ドアは -Z の側。行き先がまだ無いので見た目だけ。
        /// ドアから入ってそのまま +Z へ、奥の壁際の天井まで真っ直ぐ上る階段を置き、奥の方の天井に穴を開けて（FloorFeature.replacesCeiling）
        /// 蓋をした竪穴の中へ段が消えていくように見せる。段は登れないよう、まわりを 1 つの当たりで囲む。
        /// </summary>
        private static GameObject BuildStairUpPrefab(string name, StairMaterials m, float cellSize, float wallHeight)
        {
            var root = new GameObject(name);
            var feature = root.AddComponent<FloorFeature>();
            feature.replacesCeiling = true;

            StairFootprint(cellSize, out var footMin, out var footMax);
            var holeMin = new Vector2(footMin.x, StairUpOpeningStart);
            var holeMax = footMax;
            var holeCenter = (holeMin + holeMax) * 0.5f;
            var holeSize = holeMax - holeMin;

            var ceiling = HoledSlab(root.transform, "Ceiling", m.Ceiling, cellSize, holeMin, holeMax, wallHeight, CeilingThickness, true);
            foreach (var renderer in ceiling.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.TwoSided;

            float shaftTop = wallHeight + CeilingThickness + StairUpShaft;
            Shaft(root.transform, m.Wall, holeMin, holeMax, wallHeight, shaftTop);
            Box(root.transform, "Cap", m.Wall,
                new Vector3(holeCenter.x, shaftTop + CeilingThickness * 0.5f, holeCenter.y),
                new Vector3(holeSize.x + WallThickness * 2f, CeilingThickness, holeSize.y + WallThickness * 2f), false);

            // ドア側の端から奥の壁際まで。段数は一番上の段が天井の高さに届く数にし、奥行きはその数で割り付ける（壁の高さを変えても届く）。
            var footSize = footMax - footMin;
            int steps = Mathf.CeilToInt(wallHeight / StairUpRise - 0.001f);
            Flight(root.transform, m.Steps, footMin.y, footMin.x, footMax.x, steps, footSize.y / steps, StairUpRise, 0f);

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, (footMin.y + footMax.y) * 0.5f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(footSize.x, wallHeight, footSize.y);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 階段の占める XZ の矩形。ドアの正面（x = 0 を中心に幅 StairWidth）で、扉板の開く範囲の先から奥の壁際まで。
        /// </summary>
        private static void StairFootprint(float cellSize, out Vector2 min, out Vector2 max)
        {
            float half = cellSize * 0.5f;
            min = new Vector2(-StairWidth * 0.5f, -half + StairDoorClearance);
            max = new Vector2(StairWidth * 0.5f, half - StairWallClearance);
        }

        /// <summary>x の位置に、z = minZ〜maxZ の長さで立てる、柱と横木だけの手すり。当たりは無し（穴の見えない当たりで止める）。</summary>
        private static void Railing(Transform parent, string name, Material material, float x, float minZ, float maxZ)
        {
            var railing = new GameObject(name);
            railing.transform.SetParent(parent, false);

            float length = maxZ - minZ;
            Box(railing.transform, "Bar", material,
                new Vector3(x, RailingHeight - RailingBar * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(RailingBar, RailingBar, length), false);
            int posts = Mathf.Max(2, Mathf.CeilToInt(length / RailingPostSpacing) + 1);
            for (int i = 0; i < posts; i++)
            {
                float z = Mathf.Lerp(minZ + RailingBar * 0.5f, maxZ - RailingBar * 0.5f, i / (float)(posts - 1));
                Box(railing.transform, $"Post{i}", material,
                    new Vector3(x, (RailingHeight - RailingBar) * 0.5f, z),
                    new Vector3(RailingBar, RailingHeight - RailingBar, RailingBar), false);
            }
        }

        /// <summary>
        /// 一辺 size の板に、XZ の矩形 holeMin〜holeMax の穴を空けたもの。下面が y = bottom、厚み thickness。
        /// 箱 4 枚（南北は全幅、東西は穴の長さ）で、穴が端に接して幅の無くなる箱は作らない。
        /// </summary>
        private static GameObject HoledSlab(Transform parent, string name, Material material, float size,
            Vector2 holeMin, Vector2 holeMax, float bottom, float thickness, bool collider)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);

            float h = size * 0.5f;
            float y = bottom + thickness * 0.5f;
            Piece("South", -h, h, -h, holeMin.y);
            Piece("North", -h, h, holeMax.y, h);
            Piece("West", -h, holeMin.x, holeMin.y, holeMax.y);
            Piece("East", holeMax.x, h, holeMin.y, holeMax.y);
            return group;

            void Piece(string pieceName, float minX, float maxX, float minZ, float maxZ)
            {
                if (maxX - minX < 0.001f || maxZ - minZ < 0.001f) return;
                Box(group.transform, pieceName, material,
                    new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
                    new Vector3(maxX - minX, thickness, maxZ - minZ), collider);
            }
        }

        /// <summary>XZ の矩形 holeMin〜holeMax の穴のまわりを、y = bottom から top まで壁 4 枚で囲む竪穴。壁は穴の外側に立つ。当たりは無し（届かない所なので）。</summary>
        private static GameObject Shaft(Transform parent, Material material, Vector2 holeMin, Vector2 holeMax, float bottom, float top)
        {
            var group = new GameObject("Shaft");
            group.transform.SetParent(parent, false);

            float height = top - bottom;
            float y = bottom + height * 0.5f;
            var center = (holeMin + holeMax) * 0.5f;
            var size = holeMax - holeMin;
            float t = WallThickness;
            Box(group.transform, "North", material, new Vector3(center.x, y, holeMax.y + t * 0.5f), new Vector3(size.x + t * 2f, height, t), false);
            Box(group.transform, "South", material, new Vector3(center.x, y, holeMin.y - t * 0.5f), new Vector3(size.x + t * 2f, height, t), false);
            Box(group.transform, "East", material, new Vector3(holeMax.x + t * 0.5f, y, center.y), new Vector3(t, height, size.y), false);
            Box(group.transform, "West", material, new Vector3(holeMin.x - t * 0.5f, y, center.y), new Vector3(t, height, size.y), false);
            return group;
        }

        /// <summary>
        /// +Z へ真っ直ぐ伸びる階段。count 段を、z = startZ から run ずつ、最初の段の上面を y = 0（下りなら床と同じ高さ）か rise（上り）にして
        /// 1 段ごとに rise ずつ（負なら下へ）ずらす。X は minX〜maxX。段は y = baseY まで詰まった塊にして、横から見ても浮かないようにする。当たりは無し（飾り）。
        /// </summary>
        private static void Flight(Transform parent, Material material, float startZ, float minX, float maxX,
            int count, float run, float rise, float baseY)
        {
            var group = new GameObject("Steps");
            group.transform.SetParent(parent, false);

            float firstTop = rise > 0f ? rise : 0f;
            for (int i = 0; i < count; i++)
            {
                float top = firstTop + rise * i;
                float low = Mathf.Min(top, baseY);
                float high = Mathf.Max(top, baseY);
                Box(group.transform, $"Step{i:00}", material,
                    new Vector3((minX + maxX) * 0.5f, (low + high) * 0.5f, startZ + run * (i + 0.5f)),
                    new Vector3(maxX - minX, high - low, run), false);
            }
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
        private static GameObject BuildDoorPrefab(string name, Material material, Material leafMaterial, Material knobMaterial,
            float width, float height, bool reuseAnimator = false)
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

            // ドアノブは扉板の子（一緒に回る）。蝶番の反対の端の、表と裏に 1 つずつ。
            var knobAt = new Vector3(leafSize.x * 0.5f - KnobInset, KnobHeight - openH * 0.5f, 0f);
            AddKnob(leaf.transform, "Knob_Front", knobMaterial, knobAt, 1f);
            AddKnob(leaf.transform, "Knob_Back", knobMaterial, knobAt, -1f);

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
            serialized.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorOpenSoundPath);
            serialized.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorCloseSoundPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// ドアノブ 1 つ。扉板の面から首を出し、その先に玉を付ける。side は +1 で表（+Z）、-1 で裏。
        /// 当たりは持たない（通るときや扉板の当たりの邪魔をしないように）。
        /// </summary>
        private static void AddKnob(Transform leaf, string name, Material material, Vector3 at, float side)
        {
            var knob = new GameObject(name).transform;
            knob.SetParent(leaf, false);
            knob.localPosition = new Vector3(at.x, at.y, side * DoorLeafThickness * 0.5f);

            var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck";
            neck.transform.SetParent(knob, false);
            neck.transform.localPosition = new Vector3(0f, 0f, side * KnobNeckLength * 0.5f);
            neck.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // 円柱は高さ 2 で作られるので、y は長さの半分。
            neck.transform.localScale = new Vector3(KnobNeckWidth, KnobNeckLength * 0.5f, KnobNeckWidth);
            Object.DestroyImmediate(neck.GetComponent<Collider>());
            Paint(neck, material);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.SetParent(knob, false);
            ball.transform.localPosition = new Vector3(0f, 0f, side * (KnobNeckLength + KnobSize * 0.4f));
            ball.transform.localScale = Vector3.one * KnobSize;
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            Paint(ball, material);
        }

        /// <summary>ドアノブの真鍮っぽい素材。</summary>
        private static Material KnobMaterial()
        {
            var material = Material("Placeholder_DoorKnob", new Color(0.78f, 0.62f, 0.3f));
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.85f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.6f);
            EditorUtility.SetDirty(material);
            return material;
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

            AddSockets(root, spec.Sockets(), CellSize);

            return SavePrefab(root, $"{RoomsFolder}/{spec.Name}.prefab");
        }

        /// <summary>部屋 Prefab にドア候補（子の DoorSockets の下）を打つ。</summary>
        private static void AddSockets(GameObject room, List<DoorSocketData> sockets, float cellSize)
        {
            var socketsRoot = new GameObject("DoorSockets");
            socketsRoot.transform.SetParent(room.transform, false);

            foreach (var data in sockets)
            {
                var socketObject = new GameObject($"Socket_{data.Facing}_{data.LocalCell.X}_{data.LocalCell.Y}");
                socketObject.transform.SetParent(socketsRoot.transform, false);

                var socket = socketObject.AddComponent<DoorSocket>();
                socket.Configure(new Vector2Int(data.LocalCell.X, data.LocalCell.Y), data.Facing);
                socket.SnapToCell(cellSize);
            }
        }

        /// <summary>
        /// 部屋 Prefab のドア候補を Rooms の指定に揃える（階段の部屋をドア 1 面にした、など）。
        /// もう揃っていれば触らない（作り直すと中の ID が変わって差分だけが出る）。
        /// </summary>
        private static bool SyncRoomSockets(GameObject prefab, float cellSize)
        {
            RoomSpec? found = null;
            foreach (var spec in Rooms) if (spec.Name == prefab.name) found = spec;
            if (!found.HasValue) return false;

            var wanted = new HashSet<DoorSocketData>(found.Value.Sockets());
            var current = new HashSet<DoorSocketData>();
            foreach (var socket in prefab.GetComponentsInChildren<DoorSocket>(true)) current.Add(socket.ToData());
            if (wanted.SetEquals(current)) return false;

            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = root.transform.Find("DoorSockets");
                int index = old != null ? old.GetSiblingIndex() : root.transform.childCount;
                if (old != null) Object.DestroyImmediate(old.gameObject);

                AddSockets(root, found.Value.Sockets(), cellSize);
                root.transform.Find("DoorSockets").SetSiblingIndex(index);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
            if (config.volumeProfile == null) config.volumeProfile = LoadOrCreateVolumeProfile();

            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>
        /// 階の Volume の profile。無ければ作る。既にあれば読むだけ（手で調節した値を上書きしない）。
        /// 天井が日の光を遮るので、室内は環境光だけで暗い。暗部を持ち上げて、窓の外は飛ばさずに室内を見えるようにする。
        /// </summary>
        private static VolumeProfile LoadOrCreateVolumeProfile()
        {
            string path = $"{SettingsFolder}/Floor_VolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // 暗部と中間を持ち上げる（w が明るさのずらし）。明るい所はそのままなので、窓の外の景色は飛ばない。
            var tones = profile.Add<ShadowsMidtonesHighlights>();
            tones.shadows.Override(new Vector4(1f, 1f, 1f, 0.35f));
            tones.midtones.Override(new Vector4(1f, 1f, 1f, 0.15f));

            // 窓から入る日向が白く飛ばないよう、明るい所をなだらかに丸める。
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.3f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
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
