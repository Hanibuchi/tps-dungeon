using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;
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
        // 第1層のプレハブ。部屋と共通パーツを層ごとのフォルダ（Prefabs/Map/層）にまとめる。
        private const string RoomsFolder = "Assets/_Project/Prefabs/Map/L1/Rooms";
        private const string PartsFolder = "Assets/_Project/Prefabs/Map/L1/Parts";
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";
        private const string SettingsFolder = "Assets/_Project/Settings/Map";
        // 一式の生成で作る第1層のテーマ。
        private const string DefaultThemePath = SettingsFolder + "/L1_Theme.asset";

        private const float CellSize = 5f;
        private const float WallHeight = 5f;
        private const float WallThickness = 0.15f;
        private const float CeilingThickness = 0.2f;

        // 窓の開口。壁の幅と高さに対する比。腰の高さ（下端）と上端を比で持つので、壁の高さを変えても形が保たれる。
        // 高い建物の上の階なので、小さめの窓を上の方に開ける（5 m の壁なら幅 1.25 m、高さ 3.0〜4.0 m）。
        private const float WindowOpeningWidth = 0.25f;
        private const float WindowSill = 0.6f;
        private const float WindowTop = 0.8f;
        // 窓の枠の太さと、壁の面からの出っ張り（窓台はこの倍出す）。地上の塔の窓（SurfaceTower）も同じ寸法。
        private const float WindowFrameWidth = 0.14f;
        private const float WindowFrameDepth = 0.1f;
        // 枠を開口の内側へ寄せる量。枠の内側の面が開口の面（柱・腰壁・上の壁）と重なるとチラつくので、開口の面を枠の中に隠す。
        private const float WindowFrameInset = 0.02f;
        private const float Elevation = 40f;
        private const float FloorThickness = 0.1f;

        // ドアの開口。セル幅と壁高に対する比で持つので、CellSize を変えても通れる幅のまま追従する。
        // 上は開口の幅を直径とする半円のアーチで、高さはアーチの頂まで。5 m のセル・5 m の壁なら幅 1.5 m、頂 2.8 m、アーチの起点 2.06 m。
        private const float DoorOpeningWidth = 0.3f;
        private const float DoorOpeningHeight = 0.5625f;
        // 大扉（両開き。ボス部屋の入口）の開口。セル幅 5 m・壁 5 m なら幅 3 m・頂 4 m。
        private const float GrandDoorOpeningWidth = 0.6f;
        private const float GrandDoorOpeningHeight = 0.8f;
        // 大扉の 2 枚の扉板の合わせ目の隙間（メートル）。扉板の厚みの角が回るときに相手へ食い込まない程度に狭く。
        private const float GrandDoorSeamGap = 0.004f;
        // アーチの縁を 4 分の 1 円あたり何本の線分で近似するか（扉板は半円なのでその倍）。
        private const int ArchSegments = 16;
        // アーチの肩の当たりを何段の箱で近似するか。各段は開口へはみ出さない側に寄せる。
        private const int ArchColliderSteps = 6;

        // 開口の縁に並べるレンガ（メートル）。1 段の高さの目安（両脇は床からアーチの起点までをこれに近い段数で割り、アーチは中ほどの弧の長さで個数を決める）、
        // 開口から外への長さ（両脇もアーチも同じにして、縁取りの太さを揃える）、目地の幅、壁の面から前へ出す分とそのばらつき、
        // 開口へはみ出させる分（壁の切り口を隠す）。
        private const float BrickCourse = 0.17f;
        private const float BrickLength = 0.25f;
        private const float BrickJoint = 0.012f;
        private const float BrickProtrude = 0.02f;
        private const float BrickJitter = 0.006f;
        private const float BrickInset = 0.005f;

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
        // 大扉（ボス部屋の入口）は重い鉄の扉の音で開く。閉める音は普通のドアと同じ。
        private const string GrandDoorOpenSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/鉄の扉を開ける.mp3";
        private const string DoorCloseSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/ドアを閉める2.mp3";

        // 開口に張る照準用トリガーの奥行き（メートル。ルートの z スケールは 1 のまま）。
        private const float DoorInteractVolumeDepth = 0.3f;

        // 階段。入口の下り・出口の上りとも、ドアから入って正面へ +Z に真っ直ぐ伸びる 1 本の階段。
        // 段と竪穴には見た目どおりの当たりを付ける。下りは竪穴の底まで歩いて下りられる（1 段の高さはキャラの越えられる段差 0.25 m より低く）。
        // 上りは 1 段の奥行きがキャラの太さより短く段では引っかかるので、段の上に見えない坂の当たりを張って滑らかに上り下りさせる
        // （5 m の壁で約 57°。キャラの上れる傾き 60° より緩い）。
        // 階段の部屋はドアを南（-Z）の 1 面にだけ開けるので、ドアの真正面（x = 0）から奥の北の壁際まで伸ばす。
        // 階段の幅（X）、下りの 1 段の奥行きと高さ、上りの 1 段の高さ（メートル）。上りの奥行きは天井まで届く段数で割り付けるので急になる（5 m の壁で 0.16 m）。
        private const float StairWidth = 1.4f;
        private const float StairDownRun = 0.3f;
        private const float StairDownRise = 0.2f;
        private const float StairUpRise = 0.25f;
        // 上り階段の見えない坂の当たりの厚み。
        private const float StairRampThickness = 0.3f;
        // 階段と奥の壁の内側との隙間（壁の厚みの半分＋少し）。
        private const float StairWallClearance = 0.2f;
        // 階段の始まる所の、ドアの壁からの距離。扉板（1.5 m）が部屋の内へ開いても段や穴に掛からないよう、それより少し先から。
        private const float StairDoorClearance = 1.6f;
        // 上り階段の天井の穴の、ドア側の端（セルの中心からの Z）。
        private const float StairUpOpeningStart = 0.4f;
        // 下り階段の竪穴の深さ、上り階段の天井より上の竪穴の高さ。
        private const float StairwellDepth = 3f;
        private const float StairUpShaft = 2f;
        // 階段の先を暗くして見えにくくする暗がりの箱（DarknessVolume）の、素材を新しく作るときの既定（メートル）。
        // 下りは床からこれだけ下で暗くなり始め、ここまで下で真っ黒。上りは天井からこれだけ下で暗くなり始め、天井からこれだけ上（竪穴の中）で真っ黒。
        // 作った後の暗さ加減は、階段の横の素材（Stair_Down_Darkness / Stair_Up_Darkness）で調節する（作り直しても上書きしない）。
        private const float StairDownDarkStart = 0.2f;
        private const float StairDownDarkFull = 1.6f;
        private const float StairUpDarkStart = 1.5f;
        private const float StairUpDarkFull = 0.3f;
        // 暗がりの箱を穴より広げる分。竪穴の壁の内側の面を箱の中に入れる。
        private const float StairDarkMargin = 0.02f;
        private const string DarknessShaderName = "TpsDungeon/Darkness Volume";
        // 階に着いたパーティーが立つ所は階段の途中。段の並び（足場の手前の端から奥まで）のこの割合の所に、段を上り下りしてきた向きで立つ。
        private const float StairSpawnAlong = 0.35f;
        private const float StairSpawnLift = 0.05f;

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
                ["Shop"] = Material("Placeholder_Shop", new Color(0.85f, 0.72f, 0.18f)),
                ["Stair"] = Material("Placeholder_Stair", new Color(0.35f, 0.55f, 0.75f)),
            };

            var theme = LoadOrCreateTheme(DefaultThemePath);
            var parts = BuildParts(theme, CellSize, WallHeight, Elevation, reuseAnimator: false);
            parts["ShopMarker"] = BuildMarkerPrefab($"{PartsFolder}/Shop_Marker.prefab", materials["Shop"], PrimitiveType.Cylinder);
            var roomPrefabs = new List<GameObject>();
            foreach (var spec in Rooms) roomPrefabs.Add(BuildRoomPrefab(spec, materials[spec.MaterialKey]));

            var catalog = BuildCatalog(roomPrefabs);
            BuildFloorConfig(catalog, parts, theme);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"プレースホルダのマップ一式を生成した: 部屋 {roomPrefabs.Count} 種類 + 共通パーツ {parts.Count} 種類");

            // 普通の部屋の大きさと重みは Rooms.csv、階の部屋数は Floors.csv が正。道順があれば取り込み直して揃える。
            if (AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath) != null)
            {
                RoomCsvImporter.Import();
                FloorCsvImporter.Import();
            }
        }

        /// <summary>
        /// テーマの素材で、壁・窓付きの壁・ドア・天井・土台・階段を作る。置き場所はテーマの partsFolder。
        /// どれも ProBuilder で実寸に作り、FloorBuilder はスケールしない（素材の模様の密度を揃えるため）。
        /// </summary>
        private static Dictionary<string, GameObject> BuildParts(FloorTheme theme, float cellSize, float wallHeight, float elevation, bool reuseAnimator)
        {
            EnsureFolder(MaterialsFolder);
            FillMissingMaterials(theme);
            string folder = theme.partsFolder;
            EnsureFolder(folder);
            return new Dictionary<string, GameObject>
            {
                ["Wall"] = BuildWallPrefab($"{folder}/Wall.prefab", theme.wall, cellSize, wallHeight),
                ["Window"] = BuildWindowPrefab($"{folder}/Window.prefab", theme, cellSize, wallHeight),
                ["Door"] = BuildDoorPrefab($"{folder}/Door.prefab", theme, cellSize, wallHeight, reuseAnimator),
                ["GrandDoor"] = BuildGrandDoorPrefab($"{folder}/GrandDoor.prefab", theme, cellSize, wallHeight, reuseAnimator),
                ["Ceiling"] = BuildCeilingPrefab($"{folder}/Ceiling.prefab", theme.ceiling, cellSize),
                ["Foundation"] = BuildFoundationPrefab($"{folder}/Foundation.prefab", theme.foundation, cellSize, elevation),
                ["StairUp"] = BuildStairUpPrefab($"{folder}/Stair_Up.prefab", theme, cellSize, wallHeight),
                ["StairDown"] = BuildStairDownPrefab($"{folder}/Stair_Down.prefab", theme, cellSize),
            };
        }

        /// <summary>
        /// テーマ（FloorTheme）を差してある FloorConfig ごとに、壁・窓付きの壁・ドア・天井・土台・階段をそのテーマの素材と
        /// FloorConfig の寸法（cellSize・wallHeight・elevation）で作り直して差し、部屋の床・ドア候補も揃える。
        /// 寸法やテーマの素材を変えたらこれを回す。同じテーマの階が複数あれば、パーツは最初の階の寸法で 1 組だけ作って共有する。
        /// 一式の生成と違い、部屋の並びやカタログ・FloorConfig の他の値には触らない。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/壁・床・天井のパーツを生成")]
        public static void GenerateStructureParts()
        {
            var built = new Dictionary<FloorTheme, (FloorConfig owner, Dictionary<string, GameObject> parts)>();
            int floors = 0;
            int sockets = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:FloorConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config.theme == null || string.IsNullOrEmpty(config.theme.partsFolder))
                {
                    Debug.LogWarning($"{config.name} にテーマ（FloorTheme）が差されていないか、テーマにパーツの置き場所が無いので、パーツを作らずに飛ばした。", config);
                    continue;
                }

                if (!built.TryGetValue(config.theme, out var entry))
                {
                    entry = (config, BuildParts(config.theme, config.cellSize, config.wallHeight, config.elevation, reuseAnimator: true));
                    built.Add(config.theme, entry);
                }
                else if (!Mathf.Approximately(entry.owner.cellSize, config.cellSize) || !Mathf.Approximately(entry.owner.wallHeight, config.wallHeight)
                         || !Mathf.Approximately(entry.owner.elevation, config.elevation))
                {
                    Debug.LogWarning($"{config.name} は同じテーマの {entry.owner.name} と寸法が違うが、パーツは {entry.owner.name} の寸法で作ってある。", config);
                }

                var parts = entry.parts;
                config.wallPrefab = parts["Wall"];
                config.windowWallPrefab = parts["Window"];
                config.doorPrefab = parts["Door"];
                config.grandDoorPrefab = parts["GrandDoor"];
                config.ceilingPrefab = parts["Ceiling"];
                config.foundationPrefab = parts["Foundation"];
                config.stairUpPrefab = parts["StairUp"];
                config.stairDownPrefab = parts["StairDown"];
                if (config.volumeProfile == null) config.volumeProfile = LoadOrCreateVolumeProfile();
                EditorUtility.SetDirty(config);

                if (config.roomCatalog == null) continue;
                foreach (var room in config.roomCatalog.Entries)
                {
                    if (room.prefab != null && RebuildRoomFloor(room.prefab, config.cellSize)) floors++;
                    if (room.prefab != null && SyncRoomSockets(room.prefab, config.cellSize)) sockets++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"壁・ドア・天井・土台・階段のパーツをテーマ {built.Count} 種類ぶん作り直して FloorConfig に差した（作り直した部屋の床 {floors} 個、ドア候補を揃えた部屋 {sockets} 個）");
        }

        /// <summary>ボス階を作る層の数。Floor_L{n}_Config（n = 1..この数）を元にする。</summary>
        private const int BossFloorLayers = 5;
        private const int BossFloorGridSize = 16;
        private const string ShortcutMarkerPath = PartsFolder + "/Shortcut_Marker.prefab";

        /// <summary>ボス階の 2 部屋。入口は各層の Room_Stair をそのまま使う。抽選には出ない（重み 0、タグ Boss）。</summary>
        private static readonly RoomSpec[] BossRooms =
        {
            new RoomSpec(FloorConfig.BossHallId, BossFloorLayout.HallWidth, 1, RoomTag.Boss, false, 0f, "Floor"),
            new RoomSpec(FloorConfig.BossArenaId, BossFloorLayout.ArenaSize, BossFloorLayout.ArenaSize, RoomTag.Boss, false, 0f, "Floor"),
        };

        /// <summary>
        /// 各層のボス階の一式を作る：部屋（Room_BossHall 3x1・Room_BossArena 5x5、床はその層の Room_1x1 と同じ素材）、
        /// カタログ（L{n}_BossCatalog）、設定（Floor_L{n}_Boss_Config。間取りは Boss、他の値はその層の Floor_L{n}_Config から写す）、
        /// ショートカットの目印。最後に「壁・床・天井のパーツを生成」を回して大扉などを差す。
        /// 既にある部屋・カタログ・設定は作り直さない（手で変えた値を残す）。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/ボス階の一式を作る")]
        public static void GenerateBossFloors()
        {
            var shortcutMarker = AssetDatabase.LoadAssetAtPath<GameObject>(ShortcutMarkerPath);
            if (shortcutMarker == null)
            {
                EnsureFolder(PartsFolder);
                shortcutMarker = BuildMarkerPrefab(ShortcutMarkerPath, Material("Placeholder_Shortcut", new Color(0.2f, 0.75f, 0.7f)), PrimitiveType.Cylinder);
            }

            int made = 0;
            for (int n = 1; n <= BossFloorLayers; n++)
            {
                var baseConfig = AssetDatabase.LoadAssetAtPath<FloorConfig>($"{SettingsFolder}/Floor_L{n}_Config.asset");
                var stair = baseConfig != null && baseConfig.roomCatalog != null ? baseConfig.roomCatalog.PrefabFor(FloorConfig.BossEntranceId) : null;
                if (stair == null)
                {
                    Debug.LogWarning($"Floor_L{n}_Config か、そのカタログの {FloorConfig.BossEntranceId} が無いので、L{n} のボス階を飛ばした。");
                    continue;
                }

                string roomsFolder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(stair)).Replace('\\', '/');
                var floorMaterial = RoomFloorMaterial(baseConfig.roomCatalog);
                var entries = new List<RoomTemplateCatalog.Entry> { new RoomTemplateCatalog.Entry { prefab = stair } };
                foreach (var spec in BossRooms)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{roomsFolder}/{spec.Name}.prefab")
                        ?? BuildRoomPrefab(spec, floorMaterial, roomsFolder, baseConfig.cellSize);
                    entries.Add(new RoomTemplateCatalog.Entry { prefab = prefab });
                }

                string catalogPath = $"{SettingsFolder}/L{n}_BossCatalog.asset";
                var catalog = AssetDatabase.LoadAssetAtPath<RoomTemplateCatalog>(catalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<RoomTemplateCatalog>();
                    catalog.SetEntries(entries);
                    AssetDatabase.CreateAsset(catalog, catalogPath);
                }

                string configPath = $"{SettingsFolder}/Floor_L{n}_Boss_Config.asset";
                var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(configPath);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<FloorConfig>();
                    EditorUtility.CopySerialized(baseConfig, config);
                    config.layoutShape = FloorLayoutShape.Boss;
                    config.widthInCells = BossFloorGridSize;
                    config.heightInCells = BossFloorGridSize;
                    config.includeShop = false;
                    config.roomCatalog = catalog;
                    config.shortcutMarkerPrefab = shortcutMarker;
                    config.name = System.IO.Path.GetFileNameWithoutExtension(configPath);
                    AssetDatabase.CreateAsset(config, configPath);
                    made++;
                }
            }

            AssetDatabase.SaveAssets();
            GenerateStructureParts();
            Debug.Log($"ボス階の一式を作った（新しく作った設定 {made} 個）");
        }

        public const string RoutePath = SettingsFolder + "/DungeonRoute.asset";
        private const int RandomFloorsPerLayer = 4;

        /// <summary>層ごとの床の高さ（メートル）。上の層ほど高い建物の上にある。道順を作るとき（初回）だけ各層の設定に入れる。</summary>
        private static readonly float[] LayerElevations = { 20f, 50f, 80f, 120f, 150f };

        /// <summary>
        /// ダンジョンの道順（DungeonRoute）を作る：L1〜L5 の各層を「ランダムな階 4 つ（L{n}_Random、Floor_L{n}_Config）→ ボス階（L{n}_Boss、Floor_L{n}_Boss_Config）」で並べる。
        /// 初回は各層の設定の高さ（elevation）を LayerElevations にし、「壁・床・天井のパーツを生成」で土台を作り直す。
        /// 既に道順があれば何もしない（手で変えた層の数・階の数・高さを残す）。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/ダンジョンの道順を作る")]
        public static void GenerateDungeonRoute()
        {
            if (AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath) != null)
            {
                Debug.Log($"{RoutePath} は既にあるので触らなかった。");
                return;
            }

            var layers = new List<DungeonRoute.Layer>();
            for (int n = 1; n <= LayerElevations.Length; n++)
            {
                var random = AssetDatabase.LoadAssetAtPath<FloorConfig>($"{SettingsFolder}/Floor_L{n}_Config.asset");
                var boss = AssetDatabase.LoadAssetAtPath<FloorConfig>($"{SettingsFolder}/Floor_L{n}_Boss_Config.asset");
                if (random == null)
                {
                    Debug.LogWarning($"Floor_L{n}_Config が無いので、L{n} から上は道順に入れなかった。");
                    break;
                }

                foreach (var config in new[] { random, boss })
                {
                    if (config == null) continue;
                    config.elevation = LayerElevations[n - 1];
                    EditorUtility.SetDirty(config);
                }

                layers.Add(new DungeonRoute.Layer
                {
                    name = $"第 {n} 層",
                    randomScene = $"L{n}_Random",
                    randomConfig = random,
                    randomFloorCount = RandomFloorsPerLayer,
                    bossScene = boss != null ? $"L{n}_Boss" : "",
                    bossConfig = boss,
                });
            }

            var route = ScriptableObject.CreateInstance<DungeonRoute>();
            route.SetLayers(layers);
            AssetDatabase.CreateAsset(route, RoutePath);
            AssetDatabase.SaveAssets();

            // 土台の深さは高さで決まるので作り直す。
            GenerateStructureParts();
            Debug.Log($"ダンジョンの道順を作った（{layers.Count} 層）: {RoutePath}");
        }

        // モンスターハウスに閉じ込めたとき・鍵が開いたときの音（効果音ラボ「生活」）。
        private const string MonsterHouseTrappedSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/牢屋の扉を閉める.mp3";
        private const string MonsterHouseReleasedSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/鍵を開ける1.mp3";

        /// <summary>
        /// 層ごとの部屋の敵の表（EnemySpawnTable_L{n}）を、無いものだけ作り、道順の層の enemySpawns が空なら差す。
        /// 既にある表（手で調節したもの）と、差してある層には触らない。ただし表のモンスターハウスの音が空なら埋める（差してある音は残す）。
        /// 作るのは空の表と音だけ。敵・コスト・予算・モンスターハウスの確率と倍率・編成タイプは
        /// 「敵の CSV を取り込む」（EnemySpawnCsvImporter）で入る。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/部屋の敵の表を作る")]
        public static void GenerateEnemySpawnTables()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route == null)
            {
                Debug.LogWarning($"{RoutePath} が無いので、先に「ダンジョンの道順を作る」を回すこと。");
                return;
            }

            var trappedSound = AssetDatabase.LoadAssetAtPath<AudioClip>(MonsterHouseTrappedSoundPath);
            var releasedSound = AssetDatabase.LoadAssetAtPath<AudioClip>(MonsterHouseReleasedSoundPath);
            if (trappedSound == null) Debug.LogWarning($"{MonsterHouseTrappedSoundPath} が無いので、閉じ込めの音を入れなかった。");
            if (releasedSound == null) Debug.LogWarning($"{MonsterHouseReleasedSoundPath} が無いので、鍵が開く音を入れなかった。");

            int created = 0, assigned = 0, sounded = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                string path = $"{SettingsFolder}/EnemySpawnTable_L{i + 1}.asset";
                var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(path);
                if (table == null)
                {
                    table = ScriptableObject.CreateInstance<EnemySpawnTable>();
                    AssetDatabase.CreateAsset(table, path);
                    created++;
                }
                if (table.FillMissingSounds(trappedSound, releasedSound))
                {
                    EditorUtility.SetDirty(table);
                    sounded++;
                }

                var layer = route.Layers[i];
                if (layer.enemySpawns != null) continue;
                layer.enemySpawns = table;
                assigned++;
            }

            if (assigned > 0) EditorUtility.SetDirty(route);
            AssetDatabase.SaveAssets();
            Debug.Log($"部屋の敵の表を {created} 個作り、道順の {assigned} 層に差し、{sounded} 個の表にモンスターハウスの音を入れた（既にある値は触っていない）。" +
                      (created > 0 ? "中身はメニュー「Tools/TPS Dungeon/Enemies/敵の CSV を取り込む」で入れる。" : ""));
        }

        /// <summary>その層の部屋の床の素材（カタログの先頭の部屋の Floor）。手で差し替えた素材をボス階の部屋にも使う。</summary>
        internal static Material RoomFloorMaterial(RoomTemplateCatalog catalog)
        {
            foreach (var entry in catalog.Entries)
            {
                var floor = entry.prefab != null ? entry.prefab.transform.Find("Floor") : null;
                if (floor != null && floor.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != null) return renderer.sharedMaterial;
            }
            return ExistingMaterial("Placeholder_Floor", new Color(0.55f, 0.53f, 0.50f));
        }

        /// <summary>テーマを読む。無ければ作る。空いている素材は仮の素材で埋める。</summary>
        private static FloorTheme LoadOrCreateTheme(string path)
        {
            var theme = AssetDatabase.LoadAssetAtPath<FloorTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<FloorTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }

            if (string.IsNullOrEmpty(theme.partsFolder)) theme.partsFolder = PartsFolder;

            FillMissingMaterials(theme);
            return theme;
        }

        /// <summary>テーマの空いている素材だけ仮の素材で埋める。差してある素材（手で選んだもの）は変えない。</summary>
        private static void FillMissingMaterials(FloorTheme theme)
        {
            if (theme.wall == null) theme.wall = ExistingMaterial("Placeholder_Wall", new Color(0.35f, 0.34f, 0.33f));
            if (theme.ceiling == null) theme.ceiling = ExistingMaterial("Placeholder_Ceiling", new Color(0.28f, 0.27f, 0.26f));
            if (theme.foundation == null) theme.foundation = theme.wall;
            if (theme.doorBrick == null) theme.doorBrick = BrickMaterial();
            if (theme.doorLeaf == null) theme.doorLeaf = ExistingMaterial("Placeholder_DoorLeaf", new Color(0.42f, 0.24f, 0.12f));
            if (theme.doorKnob == null) theme.doorKnob = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Placeholder_DoorKnob.mat") ?? KnobMaterial();
            if (theme.stair == null) theme.stair = ExistingMaterial("Placeholder_Floor", new Color(0.55f, 0.53f, 0.50f));
            if (theme.stairVoid == null) theme.stairVoid = VoidMaterial();
            EditorUtility.SetDirty(theme);
        }

        private static Material ExistingMaterial(string name, Color color) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat") ?? Material(name, color);

        /// <summary>壁 1 セル分。原点が壁の下端の中心で、幅 width・高さ height の実寸。</summary>
        private static GameObject BuildWallPrefab(string path, Material material, float width, float height)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            Box(root.transform, "Mesh", material, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, WallThickness), true);
            return SavePrefab(root, path);
        }

        /// <summary>
        /// 窓付きの壁。壁と同じ実寸で、左右の柱・腰壁・上の壁を組んで真ん中を開ける。
        /// 開口のまわりには扉板と同じ木の枠（子の Frame。両面へ少し出す、下は窓台）を付ける。枠に当たりは付けない。
        /// 開口には見えない当たりを張り、外へ出られないようにする。
        /// </summary>
        private static GameObject BuildWindowPrefab(string path, FloorTheme theme, float width, float height)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            Material material = theme.wall;

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

            var frame = new GameObject("Frame").transform;
            frame.SetParent(root.transform, false);
            const float f = WindowFrameWidth;
            const float inset = WindowFrameInset;
            float depth = WallThickness + WindowFrameDepth * 2f;
            float openH = top - sill;
            float sideX = (openW + f) * 0.5f - inset;
            Box(frame, "Left", theme.doorLeaf, new Vector3(-sideX, (sill + top) * 0.5f, 0f), new Vector3(f, openH + f * 2f, depth), false);
            Box(frame, "Right", theme.doorLeaf, new Vector3(sideX, (sill + top) * 0.5f, 0f), new Vector3(f, openH + f * 2f, depth), false);
            Box(frame, "Top", theme.doorLeaf, new Vector3(0f, top + f * 0.5f - inset, 0f), new Vector3(openW + f * 2f, f, depth - 0.01f), false); // 左右と前後の面が重ならないよう少し薄く
            Box(frame, "Sill", theme.doorLeaf, new Vector3(0f, sill - f * 0.5f + inset, 0f), new Vector3(openW + f * 2f + 0.2f, f, WallThickness + WindowFrameDepth * 4f), false);

            return SavePrefab(root, path);
        }

        /// <summary>config の高さ（elevation）で土台のプレハブを作り直して差す。高さを変えたあと（Floors.csv の取り込み）に呼ぶ。</summary>
        internal static void RebuildFoundation(FloorConfig config)
        {
            if (config.theme == null || string.IsNullOrEmpty(config.theme.partsFolder)) return;
            Material material = config.theme.foundation != null ? config.theme.foundation : config.theme.wall;
            config.foundationPrefab = BuildFoundationPrefab($"{config.theme.partsFolder}/Foundation.prefab", material, config.cellSize, config.elevation);
            EditorUtility.SetDirty(config);
        }

        /// <summary>
        /// 土台 1 セル分。原点が上面の中心で、下へ depth + 1 m（地面の凹凸に隠れるよう少し深め）。
        /// 窓の外に見せるだけなので当たりは持たせない。
        /// </summary>
        private static GameObject BuildFoundationPrefab(string path, Material material, float width, float depth)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            float d = depth + 1f;
            Box(root.transform, "Mesh", material, new Vector3(0f, -d * 0.5f, 0f), new Vector3(width, d, width), false);
            return SavePrefab(root, path);
        }

        /// <summary>
        /// 天井 1 セル分。原点が下面の中心。
        /// 日の光が室内へ抜けないよう影を落とす（裏表とも）。室内の暗さは階のシーンの Global Volume で持ち上げる。
        /// </summary>
        private static GameObject BuildCeilingPrefab(string path, Material material, float width)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var mesh = Box(root.transform, "Mesh", material,
                new Vector3(0f, CeilingThickness * 0.5f, 0f), new Vector3(width, CeilingThickness, width), true);
            mesh.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            return SavePrefab(root, path);
        }

        /// <summary>
        /// 入口に置く、下の階へ続く下り階段。原点がセルの中心の床の高さで、ドアは -Z の側（階段の部屋の南）。
        /// 部屋の床の代わりに、ドアの正面から奥の壁まで細長い穴の空いた床を持ち（FloorFeature.replacesRoomFloor）、
        /// ドアから入ってそのまま +Z へ真っ直ぐ下りる階段を穴の下の竪穴に置く。
        /// 穴のまわりには手すりを付けず、段と竪穴の壁に当たりを持たせて、見た目どおり穴の縁まで寄れて段を底まで歩いて下りられるようにする。
        /// 竪穴の奥（北）の壁は暗くして、階段がその先へ続いて見えるようにする（当たりがあるのでそこで行き止まり）。
        /// 竪穴には暗がりの箱（子の Darkness）を置き、下りるほど暗くして、底の方の段と奥の壁を見えにくくする。
        /// 調べると下の階へ移る（子の Stairwell の SceneGate。行き先は FloorBootstrap が入れる）。子の SpawnPoint に入口に着いたパーティーが立つ。
        /// </summary>
        private static GameObject BuildStairDownPrefab(string path, FloorTheme theme, float cellSize)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var feature = root.AddComponent<FloorFeature>();
            feature.replacesRoomFloor = true;
            feature.depthBelowFloor = StairwellDepth + CeilingThickness;

            StairFootprint(cellSize, out var holeMin, out var holeMax);
            var holeCenter = (holeMin + holeMax) * 0.5f;
            var holeSize = holeMax - holeMin;

            HoledSlab(root.transform, "Floor", theme.stair, cellSize, holeMin, holeMax, -FloorThickness, FloorThickness, true);
            Box(root.transform, "Bottom", theme.stairVoid,
                new Vector3(holeCenter.x, -StairwellDepth - CeilingThickness * 0.5f, holeCenter.y),
                new Vector3(holeSize.x + WallThickness * 2f, CeilingThickness, holeSize.y + WallThickness * 2f), false);

            // 調べられるのは穴の中（段・竪穴の壁）だけ。床を見て「下の階へ」が出ないよう、床とは別の子にまとめる。
            var well = new GameObject("Stairwell");
            well.transform.SetParent(root.transform, false);

            var shaft = Shaft(well.transform, theme.stair, holeMin, holeMax, -StairwellDepth, -FloorThickness);
            shaft.transform.Find("North").GetComponent<MeshRenderer>().sharedMaterial = theme.stairVoid;

            int steps = Mathf.FloorToInt(holeSize.y / StairDownRun + 0.001f);
            Flight(well.transform, theme.stair, holeMin.y, holeMin.x, holeMax.x, steps, StairDownRun, -StairDownRise, -StairwellDepth);

            AddStairGate(well, "下の階へ", "下へは降りられない", "下り階段", "下の階へ続く階段。");

            // 床の高さから竪穴の底まで。暗さは床から下へ測った距離で決める。
            float darkBottom = -StairwellDepth - CeilingThickness;
            AddDarkness(root.transform, path,
                new Vector3(holeCenter.x, darkBottom * 0.5f, holeCenter.y),
                new Vector3(holeSize.x + StairDarkMargin * 2f, -darkBottom, holeSize.y + StairDarkMargin * 2f),
                upward: false, StairDownDarkStart, StairDownDarkFull);

            // 下の階から上ってきたパーティーが立つ所。段の途中で、上る向き（ドアの方）を向く。
            int spawnStep = Mathf.Clamp(Mathf.FloorToInt(steps * StairSpawnAlong), 1, steps - 1);
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetLocalPositionAndRotation(
                new Vector3(holeCenter.x, -StairDownRise * spawnStep + StairSpawnLift, holeMin.y + StairDownRun * (spawnStep + 0.5f)),
                Quaternion.Euler(0f, 180f, 0f));

            return SavePrefab(root, path);
        }

        /// <summary>
        /// 出口に置く、上の階へ続く上り階段。原点がセルの中心の床の高さで、ドアは -Z の側。
        /// 子の SpawnPoint に、上の階から降りてきたパーティーが立つ。
        /// ドアから入ってそのまま +Z へ、奥の壁際の天井まで真っ直ぐ上る階段を置き、奥の方の天井に穴を開けて（FloorFeature.replacesCeiling）
        /// 蓋をした竪穴の中へ段が消えていくように見せる。竪穴の壁・蓋には見た目どおりの当たりを付け、段は見えない坂（子の Ramp）で上り下りさせる。
        /// 天井の穴の下から竪穴の蓋までに暗がりの箱（子の Darkness）を置き、上るほど暗くして、穴の先を見えにくくする。
        /// 調べると上の階へ移る（子の Stairway の SceneGate。行き先は FloorBootstrap が入れる。上の階が無い間はこの階を作り直す）。
        /// </summary>
        private static GameObject BuildStairUpPrefab(string path, FloorTheme theme, float cellSize, float wallHeight)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var feature = root.AddComponent<FloorFeature>();
            feature.replacesCeiling = true;

            StairFootprint(cellSize, out var footMin, out var footMax);
            var holeMin = new Vector2(footMin.x, StairUpOpeningStart);
            var holeMax = footMax;
            var holeCenter = (holeMin + holeMax) * 0.5f;
            var holeSize = holeMax - holeMin;

            // 部屋の天井を置き換えるので、天井と同じ素材にして継ぎ目を見せない。
            var ceiling = HoledSlab(root.transform, "Ceiling", theme.ceiling, cellSize, holeMin, holeMax, wallHeight, CeilingThickness, true);
            foreach (var renderer in ceiling.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.TwoSided;

            // 調べられるのは段・坂・竪穴だけ。天井を見て「上の階へ」が出ないよう、天井とは別の子にまとめる。
            var way = new GameObject("Stairway");
            way.transform.SetParent(root.transform, false);

            float shaftTop = wallHeight + CeilingThickness + StairUpShaft;
            // 竪穴の壁は天井の上の面から立てる。天井の厚みの所から立てると、壁の内側の面が天井の穴の切り口と同じ所に重なってちらつく。
            var shaft = Shaft(way.transform, theme.stair, holeMin, holeMax, wallHeight + CeilingThickness, shaftTop);
            shaft.transform.Find("North").GetComponent<MeshRenderer>().sharedMaterial = theme.stairVoid;
            Box(way.transform, "Cap", theme.stair,
                new Vector3(holeCenter.x, shaftTop + CeilingThickness * 0.5f, holeCenter.y),
                new Vector3(holeSize.x + WallThickness * 2f, CeilingThickness, holeSize.y + WallThickness * 2f), true);

            // ドア側の端から奥の壁際まで。段数は一番上の段が天井の高さに届く数にし、奥行きはその数で割り付ける（壁の高さを変えても届く）。
            var footSize = footMax - footMin;
            int steps = Mathf.CeilToInt(wallHeight / StairUpRise - 0.001f);
            Flight(way.transform, theme.stair, footMin.y, footMin.x, footMax.x, steps, footSize.y / steps, StairUpRise, 0f);
            AddStairRamp(way.transform, footMin, footSize, steps);

            AddStairGate(way, "上の階へ", "上へは行けない", "上り階段", "上の階へ続く階段。");

            // 上の階から降りてきたパーティーが立つ所。段の途中（見えない坂の面の上）で、降りる向き（ドアの方）を向く。
            float spawnZ = footMin.y + footSize.y * StairSpawnAlong;
            float spawnY = Mathf.Lerp(StairUpRise * 0.5f, StairUpRise * (steps + 0.5f), StairSpawnAlong);
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetLocalPositionAndRotation(new Vector3(footMin.x + footSize.x * 0.5f, spawnY + StairSpawnLift, spawnZ),
                Quaternion.Euler(0f, 180f, 0f));

            // 床の高さから竪穴の蓋まで。暗さは床から上へ測った距離で決める。
            // 穴の無い所の天井を暗くしないよう、広さは天井の穴だけにする。
            float darkTop = shaftTop + CeilingThickness;
            AddDarkness(root.transform, path,
                new Vector3(holeCenter.x, darkTop * 0.5f, holeCenter.y),
                new Vector3(holeSize.x + StairDarkMargin * 2f, darkTop, holeSize.y + StairDarkMargin * 2f),
                upward: true, wallHeight - StairUpDarkStart, wallHeight + StairUpDarkFull);

            return SavePrefab(root, path);
        }

        /// <summary>階段の門（SceneGate）。行き先は置いたあとに FloorBootstrap が入れる。</summary>
        private static void AddStairGate(GameObject target, string openLabel, string closedLabel, string title, string description)
        {
            var gate = target.AddComponent<SceneGate>();
            var serialized = new SerializedObject(gate);
            serialized.FindProperty("openLabel").stringValue = openLabel;
            serialized.FindProperty("closedLabel").stringValue = closedLabel;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 上り階段の段の上に張る見えない坂。段の鼻をなぞると角が出て引っかかるので、各段の真ん中の高さを結ぶ面にする
        /// （段の鼻より半段低く、足が少し段に埋まる）。坂の面より上に段の当たりが出ないよう、各段の当たりは手前の端での坂の高さまで縮め、
        /// 坂の下へもぐり込めないよう床から下は埋めたままにする。
        /// </summary>
        private static void AddStairRamp(Transform root, Vector2 footMin, Vector2 footSize, int steps)
        {
            var group = root.Find("Steps");
            for (int i = 0; i < steps; i++)
            {
                var box = group.GetChild(i).GetComponent<BoxCollider>();
                float top = StairUpRise * (i + 1);
                float solid = StairUpRise * (i + 0.5f);
                box.size = new Vector3(box.size.x, solid, box.size.z);
                box.center = new Vector3(0f, (solid - top) * 0.5f, 0f);
            }

            // 坂の面の両端（z, y）。手前は最初の段の真ん中の高さ、奥は最後の段の真ん中の高さを 1 段先まで伸ばした所。
            var start = new Vector2(footMin.y, StairUpRise * 0.5f);
            var end = new Vector2(footMin.y + footSize.y, StairUpRise * (steps + 0.5f));
            var along = end - start;
            float length = along.magnitude;
            // 面の上向きの法線。箱は面の下に厚みを取る。
            var up = new Vector2(-along.y, along.x) / length;
            var center = (start + end) * 0.5f - up * (StairRampThickness * 0.5f);

            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(root, false);
            ramp.transform.SetLocalPositionAndRotation(new Vector3(footMin.x + footSize.x * 0.5f, center.y, center.x),
                Quaternion.Euler(-Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f, 0f));
            ramp.AddComponent<BoxCollider>().size = new Vector3(footSize.x, StairRampThickness, length);
        }

        /// <summary>
        /// 中にある物を暗くする箱（DarknessVolume のシェーダ）。中心 center・大きさ size で、upward でなければ上の面から下へ、
        /// upward なら下の面から上へ測った距離で暗くする（シェーダは上から下へ測るので、上向きは箱を逆さにする）。
        /// 素材は階段のプレハブの横（prefabPath の名前に _Darkness を付けた .mat）に置く。無いときだけ作って fadeStart〜fadeEnd（メートル）で
        /// 真っ黒になるようにし、既にあれば手で調節した値を残す。当たりと影は持たない。
        /// </summary>
        private static void AddDarkness(Transform parent, string prefabPath, Vector3 center, Vector3 size, bool upward, float fadeStart, float fadeEnd)
        {
            string materialPath = $"{System.IO.Path.ChangeExtension(prefabPath, null)}_Darkness.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(DarknessShaderName));
                material.SetColor("_Color", Color.black);
                material.SetFloat("_FadeStart", fadeStart);
                material.SetFloat("_FadeEnd", fadeEnd);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            var volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Darkness";
            Object.DestroyImmediate(volume.GetComponent<Collider>());
            volume.transform.SetParent(parent, false);
            volume.transform.SetLocalPositionAndRotation(center, upward ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity);
            volume.transform.localScale = size;
            var renderer = volume.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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

        /// <summary>XZ の矩形 holeMin〜holeMax の穴のまわりを、y = bottom から top まで壁 4 枚で囲む竪穴。壁は穴の外側に立ち、当たりを持つ。</summary>
        private static GameObject Shaft(Transform parent, Material material, Vector2 holeMin, Vector2 holeMax, float bottom, float top)
        {
            var group = new GameObject("Shaft");
            group.transform.SetParent(parent, false);

            float height = top - bottom;
            float y = bottom + height * 0.5f;
            var center = (holeMin + holeMax) * 0.5f;
            var size = holeMax - holeMin;
            float t = WallThickness;
            Box(group.transform, "North", material, new Vector3(center.x, y, holeMax.y + t * 0.5f), new Vector3(size.x + t * 2f, height, t), true);
            Box(group.transform, "South", material, new Vector3(center.x, y, holeMin.y - t * 0.5f), new Vector3(size.x + t * 2f, height, t), true);
            Box(group.transform, "East", material, new Vector3(holeMax.x + t * 0.5f, y, center.y), new Vector3(t, height, size.y), true);
            Box(group.transform, "West", material, new Vector3(holeMin.x - t * 0.5f, y, center.y), new Vector3(t, height, size.y), true);
            return group;
        }

        /// <summary>
        /// +Z へ真っ直ぐ伸びる階段。count 段を、z = startZ から run ずつ、最初の段の上面を y = 0（下りなら床と同じ高さ）か rise（上り）にして
        /// 1 段ごとに rise ずつ（負なら下へ）ずらす。X は minX〜maxX。段は y = baseY まで詰まった塊にして、横から見ても浮かないようにする。
        /// 段ごとに同じ大きさの当たりを持つ。
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
                    new Vector3(maxX - minX, high - low, run), true);
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
        /// 上が半円のアーチの開口をまわりの壁（子の Frame/Wall。テーマの壁の素材）に開け、その縁に沿ってレンガ（Frame/Bricks）を並べる。
        /// 開口にはアーチ形の扉板（Hinge の子の Leaf）を吊る。開閉の動きはルートの Animator が DoorAnimatorBuilder の作るクリップで Hinge を回す。
        /// </summary>
        private static GameObject BuildDoorPrefab(string path, FloorTheme theme, float width, float height, bool reuseAnimator) =>
            BuildDoorPrefab(path, theme, width, height, reuseAnimator, DoorOpeningWidth, DoorOpeningHeight, doubleLeaf: false);

        /// <summary>
        /// 大扉。普通のドアと同じ作り（アーチの開口・縁のレンガ・扉板）で開口を大きくし、扉板を真ん中で分けた左右 2 枚にして両側の軸で開く。
        /// </summary>
        private static GameObject BuildGrandDoorPrefab(string path, FloorTheme theme, float width, float height, bool reuseAnimator) =>
            BuildDoorPrefab(path, theme, width, height, reuseAnimator, GrandDoorOpeningWidth, GrandDoorOpeningHeight, doubleLeaf: true);

        private static GameObject BuildDoorPrefab(string path, FloorTheme theme, float width, float height, bool reuseAnimator,
            float openingWidth, float openingHeight, bool doubleLeaf)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));

            var frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);

            float openW = openingWidth * width;
            float crown = openingHeight * height;
            float radius = openW * 0.5f;
            // アーチの起点。両脇の縁はここまで真っ直ぐ立ち、ここから上が半円になる。
            float spring = crown - radius;

            BuildDoorWall(frame.transform, theme.wall, width, height, radius, spring);
            BuildDoorBricks(frame.transform, theme.doorBrick, radius, spring);

            // 開けた後に開口を覗いても「閉める」が出るよう、開口いっぱい（アーチの頂まで）に照準用のトリガーを張る。
            // 当たり判定は持たないので通行の邪魔はしない。
            var volume = new GameObject("InteractVolume");
            volume.transform.SetParent(frame.transform, false);
            volume.transform.localPosition = new Vector3(0f, crown * 0.5f, 0f);
            var trigger = volume.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(openW, crown, DoorInteractVolumeDepth);

            // 回す軸は開口の左端。扉板は軸から右へ開口いっぱい（枠との隙間だけ小さく）。
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(-radius, 0f, 0f);

            GameObject leaf;
            GameObject leafRight = null;
            Transform hingeRight = null;
            if (!doubleLeaf)
            {
                leaf = BuildDoorLeaf(hinge.transform, theme.doorLeaf, openW, radius, spring);

                // ドアノブは扉板の子（一緒に回る）。蝶番の反対の端の、表と裏に 1 つずつ。
                var knobAt = new Vector3(openW - DoorLeafGap - KnobInset, KnobHeight, 0f);
                AddKnob(leaf.transform, "Knob_Front", theme.doorKnob, knobAt, 1f);
                AddKnob(leaf.transform, "Knob_Back", theme.doorKnob, knobAt, -1f);
            }
            else
            {
                // 右の軸は開口の右端。扉板は真ん中の合わせ目まで。ノブはどちらも合わせ目の側に。
                hingeRight = new GameObject(DoorAnimatorBuilder.RightHingePath).transform;
                hingeRight.SetParent(root.transform, false);
                hingeRight.localPosition = new Vector3(radius, 0f, 0f);

                leaf = BuildDoorLeafHalf(hinge.transform, "Leaf", theme.doorLeaf, radius, spring, 1f);
                leafRight = BuildDoorLeafHalf(hingeRight, "LeafRight", theme.doorLeaf, radius, spring, -1f);
                float knobX = radius - GrandDoorSeamGap * 0.5f - KnobInset;
                AddKnob(leaf.transform, "Knob_Front", theme.doorKnob, new Vector3(knobX, KnobHeight, 0f), 1f);
                AddKnob(leaf.transform, "Knob_Back", theme.doorKnob, new Vector3(knobX, KnobHeight, 0f), -1f);
                AddKnob(leafRight.transform, "Knob_Front", theme.doorKnob, new Vector3(-knobX, KnobHeight, 0f), 1f);
                AddKnob(leafRight.transform, "Knob_Back", theme.doorKnob, new Vector3(-knobX, KnobHeight, 0f), -1f);
            }

            var animator = root.AddComponent<Animator>();
            // コントローラは組み直すと中の ID が全部変わるので、寸法だけ作り直すときは今のものを使う。
            string controllerPath = doubleLeaf ? DoorAnimatorBuilder.GrandControllerPath : DoorAnimatorBuilder.ControllerPath;
            var controller = reuseAnimator ? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) : null;
            if (controller == null) controller = doubleLeaf ? DoorAnimatorBuilder.BuildGrand() : DoorAnimatorBuilder.Build();
            animator.runtimeAnimatorController = controller;

            var door = root.AddComponent<Door>();
            var serialized = new SerializedObject(door);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("hinge").objectReferenceValue = hinge.transform;
            serialized.FindProperty("leaf").objectReferenceValue = leaf.transform;
            serialized.FindProperty("hingeRight").objectReferenceValue = hingeRight;
            serialized.FindProperty("leafRight").objectReferenceValue = leafRight != null ? leafRight.transform : null;
            // 大扉（ボス部屋の入口）は一度開けたら閉められない。
            serialized.FindProperty("canClose").boolValue = !doubleLeaf;
            serialized.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(doubleLeaf ? GrandDoorOpenSoundPath : DoorOpenSoundPath);
            serialized.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorCloseSoundPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        /// <summary>
        /// ドアのまわりの壁。セル幅いっぱいの壁から、上が半円（中心 (0, spring)、半径 radius）の開口を抜いた形を 1 つのメッシュにする。
        /// 両脇の柱・アーチの上の壁・アーチの両肩を同じ原点で並べるので、継ぎ目で模様がずれない。
        /// 当たりは柱と上の壁の箱に、アーチの肩を数段の箱で近似したものを足す。
        /// </summary>
        private static void BuildDoorWall(Transform frame, Material material, float width, float height, float radius, float spring)
        {
            float half = width * 0.5f;
            float crown = spring + radius;
            float z = WallThickness * 0.5f;

            var prisms = new PrismBuilder();
            prisms.Add(Rect(-half, 0f, -radius, height), -z, z);
            prisms.Add(Rect(radius, 0f, half, height), -z, z);
            prisms.Add(Rect(-radius, crown, radius, height), -z, z);
            foreach (float side in Sides)
            {
                // 肩の角 (±radius, crown) から、アーチの縁の点へ扇形に張る（角からは縁の 4 分の 1 円が全部見通せる）。
                var outline = new List<Vector2> { new Vector2(side * radius, crown) };
                for (int i = 0; i <= ArchSegments; i++)
                {
                    float angle = Mathf.PI * 0.5f * i / ArchSegments;
                    outline.Add(new Vector2(side * radius * Mathf.Cos(angle), spring + radius * Mathf.Sin(angle)));
                }
                prisms.Add(outline, -z, z);
            }
            var wall = prisms.Build(frame, "Wall", material);

            AddBoxCollider(wall, new Vector3(-(half + radius) * 0.5f, height * 0.5f, 0f), new Vector3(half - radius, height, WallThickness));
            AddBoxCollider(wall, new Vector3((half + radius) * 0.5f, height * 0.5f, 0f), new Vector3(half - radius, height, WallThickness));
            AddBoxCollider(wall, new Vector3(0f, (crown + height) * 0.5f, 0f), new Vector3(radius * 2f, height - crown, WallThickness));
            for (int i = 0; i < ArchColliderSteps; i++)
            {
                float y0 = spring + radius * i / ArchColliderSteps;
                float y1 = spring + radius * (i + 1) / ArchColliderSteps;
                // この段のいちばん下での開口の半幅。上ほど狭いので、ここから外を塞げば段の中で開口にはみ出さない。
                float inner = Mathf.Sqrt(Mathf.Max(0f, radius * radius - (y0 - spring) * (y0 - spring)));
                if (radius - inner < 0.001f) continue;
                foreach (float side in Sides)
                {
                    AddBoxCollider(wall, new Vector3(side * (inner + radius) * 0.5f, (y0 + y1) * 0.5f, 0f),
                        new Vector3(radius - inner, y1 - y0, WallThickness));
                }
            }
        }

        /// <summary>
        /// ドアの開口の縁に並べるレンガ。両脇は床からアーチの起点まで同じ長さで積み、アーチは放射状に奇数個並べる。
        /// どれも壁より厚く（表裏に BrickProtrude ずつ、1 個ずつ少しばらつかせる）、縁は開口へわずかに出して壁の切り口を隠す。
        /// 目地の隙間からは奥の壁が見える。全部で 1 つのメッシュにまとめ、当たりは持たない（壁の当たりに収まる）。
        /// </summary>
        private static void BuildDoorBricks(Transform frame, Material material, float radius, float spring)
        {
            // ばらつきは毎回同じにする（作り直しても差分が出ないように）。
            var random = new System.Random(1);
            var prisms = new PrismBuilder();
            float inner = radius - BrickInset;

            int courses = Mathf.Max(1, Mathf.RoundToInt(spring / BrickCourse));
            float course = spring / courses;
            for (int k = 0; k < courses; k++)
            {
                float bottom = k == 0 ? 0f : k * course + BrickJoint * 0.5f;
                float top = (k + 1) * course - BrickJoint * 0.5f;
                float outer = radius + BrickLength;
                foreach (float side in Sides)
                {
                    var outline = side < 0f ? Rect(-outer, bottom, -inner, top) : Rect(inner, bottom, outer, top);
                    prisms.Add(outline, -BrickHalfDepth(random), BrickHalfDepth(random));
                }
            }

            float middle = radius + BrickLength * 0.5f;
            // 奇数にして、頂の真ん中に 1 個来るようにする。
            int count = Mathf.Max(3, Mathf.RoundToInt(Mathf.PI * middle / BrickCourse) | 1);
            float step = Mathf.PI / count;
            float joint = BrickJoint / middle;
            for (int i = 0; i < count; i++)
            {
                float a0 = i * step + joint * 0.5f;
                float a1 = (i + 1) * step - joint * 0.5f;
                float outer = radius + BrickLength;
                var outline = new List<Vector2>
                {
                    Polar(inner, a0, spring), Polar(outer, a0, spring), Polar(outer, a1, spring), Polar(inner, a1, spring),
                };
                prisms.Add(outline, -BrickHalfDepth(random), BrickHalfDepth(random));
            }

            prisms.Build(frame, "Bricks", material);
        }

        /// <summary>レンガの厚みの半分。壁の厚みの半分に、前へ出す分とそのばらつきを足す。</summary>
        private static float BrickHalfDepth(System.Random random) =>
            WallThickness * 0.5f + BrickProtrude + ((float)random.NextDouble() * 2f - 1f) * BrickJitter;

        /// <summary>アーチの中心 (0, spring) から角度 angle（0 が右、π/2 が頂）・半径 radius の点。</summary>
        private static Vector2 Polar(float radius, float angle, float spring) =>
            new Vector2(radius * Mathf.Cos(angle), spring + radius * Mathf.Sin(angle));

        /// <summary>
        /// アーチ形の扉板。蝶番（Hinge）の原点から右へ開口の幅で、床のすぐ上から、上は開口のアーチに沿う（枠との隙間だけ小さく）。
        /// 当たりは外接する箱（Door は閉じたときに人が居ないかもこの箱で見る）。
        /// </summary>
        private static GameObject BuildDoorLeaf(Transform hinge, Material material, float openW, float radius, float spring)
        {
            float bottom = DoorLeafGap * 0.5f;
            float r = radius - DoorLeafGap;
            var outline = new List<Vector2> { new Vector2(DoorLeafGap, bottom), new Vector2(openW - DoorLeafGap, bottom) };
            for (int i = 0; i <= ArchSegments * 2; i++)
            {
                float angle = Mathf.PI * i / (ArchSegments * 2);
                outline.Add(new Vector2(radius + r * Mathf.Cos(angle), spring + r * Mathf.Sin(angle)));
            }

            var prisms = new PrismBuilder();
            float z = DoorLeafThickness * 0.5f;
            prisms.Add(outline, -z, z);
            var leaf = prisms.Build(hinge, "Leaf", material);

            float top = spring + r;
            var box = leaf.AddComponent<BoxCollider>();
            box.center = new Vector3(openW * 0.5f, (bottom + top) * 0.5f, 0f);
            box.size = new Vector3(openW - DoorLeafGap * 2f, top - bottom, DoorLeafThickness);
            return leaf;
        }

        /// <summary>
        /// 両開きの扉板の片方。開口のアーチを真ん中で分けた半分の形で、軸（hinge）の原点から合わせ目まで。
        /// side が 1 なら左の扉板（軸から +X へ）、-1 なら右の扉板（軸から -X へ。左の扉板を映した形）。
        /// </summary>
        private static GameObject BuildDoorLeafHalf(Transform hinge, string name, Material material, float radius, float spring, float side)
        {
            float bottom = DoorLeafGap * 0.5f;
            float r = radius - DoorLeafGap;
            float seam = radius - GrandDoorSeamGap * 0.5f;
            // 合わせ目でアーチに当たる角度（真ん中の少し左）から、左の縁（π）まで。
            float start = Mathf.Acos(Mathf.Clamp((seam - radius) / r, -1f, 1f));

            var outline = new List<Vector2> { new Vector2(DoorLeafGap, bottom), new Vector2(seam, bottom) };
            for (int i = 0; i <= ArchSegments; i++)
            {
                float angle = Mathf.Lerp(start, Mathf.PI, (float)i / ArchSegments);
                outline.Add(new Vector2(radius + r * Mathf.Cos(angle), spring + r * Mathf.Sin(angle)));
            }
            if (side < 0f)
            {
                // 映すと向きが裏返るので、並びも逆にして表を保つ。
                for (int i = 0; i < outline.Count; i++) outline[i] = new Vector2(-outline[i].x, outline[i].y);
                outline.Reverse();
            }

            var prisms = new PrismBuilder();
            float z = DoorLeafThickness * 0.5f;
            prisms.Add(outline, -z, z);
            var leaf = prisms.Build(hinge, name, material);

            float top = spring + r;
            var box = leaf.AddComponent<BoxCollider>();
            box.center = new Vector3(side * (DoorLeafGap + seam) * 0.5f, (bottom + top) * 0.5f, 0f);
            box.size = new Vector3(seam - DoorLeafGap, top - bottom, DoorLeafThickness);
            return leaf;
        }

        private static readonly float[] Sides = { -1f, 1f };

        private static List<Vector2> Rect(float minX, float minY, float maxX, float maxY) => new List<Vector2>
        {
            new Vector2(minX, minY), new Vector2(maxX, minY), new Vector2(maxX, maxY), new Vector2(minX, maxY),
        };

        private static void AddBoxCollider(GameObject target, Vector3 center, Vector3 size)
        {
            var box = target.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        /// <summary>
        /// XY の輪郭を Z の向きに押し出した柱体をいくつか集めて、1 つの ProBuilder のメッシュにする（アーチの壁・レンガ・扉板）。
        /// 輪郭は凸か、最初の頂点から全部の頂点が見通せる形に限る（表と裏は最初の頂点から扇形に三角形を張る）。回る向きはどちらでもよい。
        /// 面ごとに頂点を分けて角を立て、UV は Box と同じく面ごとの平面投影（1 m = 1）。どの柱体も原点が同じなので、同じ向きの面どうしは模様が続く。
        /// </summary>
        private sealed class PrismBuilder
        {
            private readonly List<Vector3> positions = new List<Vector3>();
            private readonly List<Face> faces = new List<Face>();

            /// <summary>輪郭 outline を z = back から front（back &lt; front）まで押し出す。</summary>
            public void Add(IList<Vector2> outline, float back, float front)
            {
                var points = new List<Vector2>(outline);
                // 反時計回りにそろえる。扇の要の最初の頂点は先頭のまま。
                if (SignedArea(points) < 0f) points.Reverse(1, points.Count - 1);

                // 反時計回りの輪郭なら (0, i, i+1) が +Z を向く（Unity は cross(b - a, c - a) の向きが表）。
                faces.Add(Cap(points, front, false));
                faces.Add(Cap(points, back, true));
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[(i + 1) % points.Count];
                    int s = positions.Count;
                    positions.Add(new Vector3(a.x, a.y, back));
                    positions.Add(new Vector3(b.x, b.y, back));
                    positions.Add(new Vector3(b.x, b.y, front));
                    positions.Add(new Vector3(a.x, a.y, front));
                    faces.Add(new Face(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }));
                }
            }

            public GameObject Build(Transform parent, string name, Material material)
            {
                var mesh = ProBuilderMesh.Create(positions, faces);
                mesh.gameObject.name = name;
                mesh.transform.SetParent(parent, false);
                if (material != null) mesh.SetMaterial(mesh.faces, material);
                mesh.ToMesh();
                mesh.Refresh();
                return mesh.gameObject;
            }

            private Face Cap(List<Vector2> points, float z, bool flip)
            {
                int s = positions.Count;
                foreach (var p in points) positions.Add(new Vector3(p.x, p.y, z));

                var triangles = new List<int>();
                for (int i = 1; i + 1 < points.Count; i++)
                {
                    triangles.Add(s);
                    triangles.Add(s + (flip ? i + 1 : i));
                    triangles.Add(s + (flip ? i : i + 1));
                }
                return new Face(triangles);
            }

            private static float SignedArea(List<Vector2> points)
            {
                float area = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[(i + 1) % points.Count];
                    area += a.x * b.y - b.x * a.y;
                }
                return area * 0.5f;
            }
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

        /// <summary>
        /// 竪穴の奥と底の素材。光を受けない真っ黒の単色（URP の Unlit）。無いときだけ作る。
        /// </summary>
        private static Material VoidMaterial()
        {
            string path = $"{MaterialsFolder}/Placeholder_Void.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader != null ? shader : DefaultShader());
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.black);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>ドアの縁のレンガの仮の素材。赤茶の素焼き。</summary>
        private static Material BrickMaterial()
        {
            var material = Material("Placeholder_Brick", new Color(0.52f, 0.26f, 0.18f));
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
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

        private static GameObject BuildMarkerPrefab(string path, Material material, PrimitiveType primitive)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var mesh = GameObject.CreatePrimitive(primitive);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            mesh.transform.localScale = new Vector3(1.4f, primitive == PrimitiveType.Cylinder ? 0.75f : 1.5f, 1.4f);
            Paint(mesh, material);

            return SavePrefab(root, path);
        }

        /// <summary>
        /// 部屋 Prefab。床とドア候補だけを持ち、外周壁は持たない（FloorBuilder が建てる）。
        /// ローカル原点はセル (0,0) の角。
        /// </summary>
        private static GameObject BuildRoomPrefab(RoomSpec spec, Material material) =>
            BuildRoomPrefab(spec, material, RoomsFolder, CellSize);

        /// <summary>普通の部屋の Prefab（回転してよい、ドア候補は外周全部）を folder/name.prefab に作る。Rooms.csv の取り込みで使う。</summary>
        internal static GameObject BuildNormalRoomPrefab(string name, int width, int height, float weight, int maxDoors, Material material, string folder, float cellSize) =>
            BuildRoomPrefab(new RoomSpec(name, width, height, RoomTag.Normal, true, weight, "Floor"), material, folder, cellSize, maxDoors);

        private static GameObject BuildRoomPrefab(RoomSpec spec, Material material, string folder, float cellSize, int maxDoors = 0)
        {
            var root = new GameObject(spec.Name);
            var template = root.AddComponent<RoomTemplate>();
            template.Configure(new Vector2Int(spec.Width, spec.Height), cellSize, spec.Tags, spec.AllowRotation, spec.Weight);
            template.SetMaxDoors(maxDoors);

            AddRoomFloor(root, spec.Width, spec.Height, cellSize, material);

            AddSockets(root, spec.Sockets(), cellSize);

            return SavePrefab(root, $"{folder}/{spec.Name}.prefab");
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

        private static FloorConfig BuildFloorConfig(RoomTemplateCatalog catalog, Dictionary<string, GameObject> parts, FloorTheme theme)
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
            config.theme = theme;
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
