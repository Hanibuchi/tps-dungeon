using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Enemies.Editor;
using TpsDungeon.Items;
using System.IO;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 置き物のプレハブを作り、ダンジョンの部屋に置く層ごとの一覧（<see cref="RoomPropSet"/>。RoomPropSet_L{n}）にまとめて、その層の階の設定に差す。
    /// - 壊せる草花（Prefabs/Map/Breakables/Breakable_rpgpp_lt_*）：RPGPP の草・花・草木のバリアントに、壊せるようにする物（BreakablePlantSetup.MakeBreakable）を書き込む。
    ///   地上の景色（SurfaceScenery）はこれを置く。出てくる敵は地上の表（PlantSpawnTable_Surface）。
    /// - 壊せる木箱・宝箱（Breakables/Crate・Chest）：Free Low Poly Dungeon Pack の箱に、壊せるようにする物（MakeBreakableBox）を書き込む。
    ///   出てくるものは Breakables.csv の表（BreakableDrops）。武器が出たら BreakableWeaponDrop が床に出す。
    /// - 部屋の飾り（Prefabs/Map/Props/Prop_*）：同じパックの松明・柱・机・椅子・小物。盾は拾える武器（盾）と見分けがつかないので使わない。
    ///   松明と火の付いたろうそくは点光源（FlickerLight）で本当に照らす。
    /// - 層ごとの一覧：小物の顔ぶれ（骨・本・薬瓶・ろうそくの重み）と机の確率を Settings/Tables/RoomProps.csv から入れる（<see cref="BuildPropSets"/>）。
    /// 部屋のプレハブはどれも、原点が床（松明は壁の面）で、正面（宝箱の錠・椅子の座る向き・松明の火）が +Z を向くように中の模型を回してある。
    /// 机・椅子・柱は当たりを付けて静的にする（NavMesh に焼き込まれる）。小物と松明は当たりを付けない。光は揺らすので静的にしない。
    /// ThirdParty は git に上げないので、本体のエディタで回す。
    /// </summary>
    public static class BreakablePrefabBuilder
    {
        public const string BreakablesFolder = "Assets/_Project/Prefabs/Map/Breakables";
        public const string PropsFolder = "Assets/_Project/Prefabs/Map/Props";
        public const string PropSetFolder = "Assets/_Project/Settings/Map";
        public const string RoomPropCsvPath = "Assets/_Project/Settings/Tables/RoomProps.csv";
        private const string OldSharedPropSetPath = "Assets/_Project/Settings/Map/RoomPropSet_Dungeon.asset";
        private const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";

        private const string PlantPack = "Assets/ThirdParty/3D Model/RPGPP_LT/Prefabs/Nature/Vegetation/";
        private const string DungeonPack = "Assets/ThirdParty/3D Model/Free Low Poly Dungeon Pack/Prefabs/";
        // 箱の壊れる音（効果音ラボ「建物が少し崩れる1」）。
        private const string BoxBreakSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/建物が少し崩れる1.mp3";

        /// <summary>壊せる草花にする RPGPP の素材（Vegetation からのパス）。</summary>
        public static readonly string[] PlantSources =
        {
            "Grass/rpgpp_lt_grass_small_01a",
            "Grass/rpgpp_lt_grass_small_01b",
            "Flowers/rpgpp_lt_flower_01",
            "Flowers/rpgpp_lt_flower_02",
            "Flowers/rpgpp_lt_flower_03",
            "Plants/rpgpp_lt_plant_01",
            "Plants/rpgpp_lt_plant_02",
        };

        [MenuItem("Tools/TPS Dungeon/素材・プレハブ/置き物のプレハブを作る（草花・木箱・宝箱・部屋の飾り）", false, 508)]
        public static void BuildFromMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            EnsureFolder(BreakablesFolder);
            EnsureFolder(PropsFolder);

            PlantSpawnTable plantTable = BreakablePlantSetup.EnsureAssets();
            int plants = 0;
            foreach (string source in PlantSources)
            {
                if (BuildPlant(source, plantTable) != null) plants++;
            }

            BreakableDropTable drops = BreakableDropCsvImporter.Import();
            BuildBox("Crate", "SM_Crate_01", 1.25f, 0f, drops, BreakableDropKind.Crate);
            BuildBox("Chest", "SM_Chest_Wooden_Locked", 1.2f, 90f, drops, BreakableDropKind.Chest);

            // 正面の向き：宝箱・椅子・松明は模型の -X が正面なので、+Z へ回す。
            // 松明と火の付いたろうそくは本当に照らす（FlickerLight。色・強さは階の光と色から入る）。ろうそくは松明より弱く短く。
            BuildProp("Prop_Torch", "SM_Torch_Mount_Large_Lit", Vector3.one * 1.3f, 90f, false, ground: false, flame: new Flame(1f, 1f));
            BuildProp("Prop_Pillar", "SM_Pillar_Stone_Full_TrimDouble", new Vector3(2.5f, 2.5f, 2.5f), 0f, true);
            BuildProp("Prop_Table", "SM_Table", Vector3.one, 0f, true);
            BuildProp("Prop_Chair", "SM_Chair", Vector3.one * 0.9f, 90f, true);
            BuildProp("Prop_Candles", "SM_Candle_Group_Small_Lit", Vector3.one * 1.2f, 0f, false, flame: new Flame(0.35f, 0.4f));
            BuildProp("Prop_Candles_Unlit", "SM_Candle_Group_Small_Unlit", Vector3.one * 1.2f, 0f, false);
            BuildProp("Prop_Books", "SM_Book_Pile_Small", Vector3.one, 0f, false);
            BuildProp("Prop_Bones_Small", "SM_Bone_Pile_Small", Vector3.one * 1.3f, 0f, false);
            BuildProp("Prop_Bones_Medium", "SM_Bone_Pile_Medium", Vector3.one * 1.3f, 0f, false);
            BuildProp("Prop_Potion", "SM_Potion_Medium_Empty", Vector3.one * 1.2f, 0f, false);

            int sets = BuildPropSets();
            AssetDatabase.SaveAssets();
            return $"置き物のプレハブを作った（壊せる草花 {plants} 種、木箱・宝箱、部屋の飾り 10 種）。層ごとの置き物の一覧を {sets} つ作って階の設定に差した。";
        }

        /// <summary>
        /// 層ごとの置き物の一覧（Settings/Map/RoomPropSet_L{n}）を、作ってあるプレハブと Settings/Tables/RoomProps.csv の重み・机の確率から作り、
        /// その層のランダムな階とボス階の設定に差す。行の無い層は同じ重み・机 40% にする。作った一覧の数を返す。
        /// RoomProps.csv を保存しても回る（RoomPropCsvImporter）。
        /// </summary>
        public static int BuildPropSets()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route == null)
            {
                Debug.LogError($"{RoutePath} が無いので、層ごとの置き物の一覧を作れない。");
                return 0;
            }

            GameObject Prop(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{name}.prefab");
            GameObject torch = Prop("Prop_Torch"), pillar = Prop("Prop_Pillar"), table = Prop("Prop_Table"), chair = Prop("Prop_Chair");
            GameObject candles = Prop("Prop_Candles"), candlesUnlit = Prop("Prop_Candles_Unlit"), books = Prop("Prop_Books");
            GameObject bonesSmall = Prop("Prop_Bones_Small"), bonesMedium = Prop("Prop_Bones_Medium"), potion = Prop("Prop_Potion");
            GameObject crate = AssetDatabase.LoadAssetAtPath<GameObject>($"{BreakablesFolder}/Crate.prefab");
            GameObject chest = AssetDatabase.LoadAssetAtPath<GameObject>($"{BreakablesFolder}/Chest.prefab");
            if (torch == null || crate == null)
            {
                Debug.LogError("置き物のプレハブがまだ無いので、層ごとの一覧を作れない。先に「置き物のプレハブを作る」を回すこと。");
                return 0;
            }

            var byLayer = new Dictionary<int, RoomPropCsv.Row>();
            if (File.Exists(RoomPropCsvPath))
            {
                var errors = new List<string>();
                foreach (RoomPropCsv.Row row in RoomPropCsv.Parse(File.ReadAllText(RoomPropCsvPath), errors)) byLayer[row.Layer] = row;
                foreach (string error in errors) Debug.LogError($"置き物の CSV（{RoomPropCsvPath}）の {error}");
            }
            else Debug.LogWarning($"{RoomPropCsvPath} が無いので、どの層も同じ重みにする。");

            int count = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                int layer = i + 1;
                if (!byLayer.TryGetValue(layer, out RoomPropCsv.Row row))
                {
                    Debug.LogWarning($"置き物の CSV に層 {layer} の行が無いので、同じ重み・机 40% にした");
                    row = new RoomPropCsv.Row(0, layer, 1f, 1f, 1f, 1f, 1f, 0.4f);
                }

                string path = $"{PropSetFolder}/RoomPropSet_L{layer}.asset";
                var set = AssetDatabase.LoadAssetAtPath<RoomPropSet>(path);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<RoomPropSet>();
                    AssetDatabase.CreateAsset(set, path);
                }
                set.torches = new List<GameObject> { torch };
                set.pillars = new List<GameObject> { pillar };
                set.tables = new List<GameObject> { table };
                set.chairs = new List<GameObject> { chair };
                set.crates = new List<GameObject> { crate };
                set.chests = new List<GameObject> { chest };
                // 骨は小・中に半分ずつ。机の上は火の付いた・消えたろうそくと本。
                set.wallClutter = new List<GameObject> { bonesSmall, bonesMedium, books, potion, candles, candlesUnlit };
                set.wallClutterWeights = new List<float> { row.Bones * 0.5f, row.Bones * 0.5f, row.Books, row.Potions, row.Candles, row.CandlesUnlit };
                set.tableTops = new List<GameObject> { candles, candlesUnlit, books };
                set.tableTopWeights = new List<float> { row.Candles, row.CandlesUnlit, row.Books };
                set.tableChance = row.TableChance;
                EditorUtility.SetDirty(set);

                foreach (FloorConfig config in new[] { route.Layers[i].randomConfig, route.Layers[i].bossConfig })
                {
                    if (config == null || config.propSet == set) continue;
                    config.propSet = set;
                    EditorUtility.SetDirty(config);
                }
                count++;
            }

            // 全部の層で共通だった一覧（前の作り）は、どの階も使わなくなったので消す。
            if (AssetDatabase.LoadAssetAtPath<RoomPropSet>(OldSharedPropSetPath) != null) AssetDatabase.DeleteAsset(OldSharedPropSetPath);
            AssetDatabase.SaveAssets();
            return count;
        }

        /// <summary>RPGPP の草花の素材の名前（rpgpp_lt_grass_small_01a など）から、壊せる草花のプレハブのパス。</summary>
        public static string PlantPrefabPath(string sourceName) => $"{BreakablesFolder}/Breakable_{sourceName}.prefab";

        /// <summary>RPGPP の素材の名前に当たる壊せる草花のプレハブ。無ければ null。</summary>
        public static GameObject PlantPrefabFor(string sourceName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PlantPrefabPath(sourceName));

        // ---- 草花 ----

        private static GameObject BuildPlant(string source, PlantSpawnTable table)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlantPack + source + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"草花の素材が無い: {PlantPack}{source}.prefab");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                BreakablePlantSetup.MakeBreakable(instance, table);
                return PrefabUtility.SaveAsPrefabAsset(instance, PlantPrefabPath(prefab.name));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ---- 箱・飾り ----

        /// <summary>
        /// パックの模型を子に入れた入れ物を作る。模型は scale 倍して yaw 度回す。
        /// ground なら模型の一番下を原点の高さ（床）に合わせる（骨や薬瓶などの模型は原点より下へはみ出していることがあり、そのままだと床に埋まる）。
        /// </summary>
        private static GameObject Wrap(string name, string model, Vector3 scale, float yaw, bool ground = true)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DungeonPack + model + ".prefab");
            if (source == null) throw new System.InvalidOperationException($"素材が無い: {DungeonPack}{model}.prefab（Free Low Poly Dungeon Pack を入れること）");

            var root = new GameObject(name);
            var child = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            child.name = "Model";
            child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            child.transform.localScale = scale;
            if (ground) child.transform.localPosition = Vector3.down * BreakablePlantSetup.LocalBounds(root).min.y;
            return root;
        }

        private static GameObject BuildBox(string name, string model, float scale, float yaw, BreakableDropTable drops, BreakableDropKind kind)
        {
            GameObject root = Wrap(name, model, Vector3.one * scale, yaw);
            try
            {
                BreakablePlantSetup.MakeBreakableBox(root, drops, kind, AssetDatabase.LoadAssetAtPath<AudioClip>(BoxBreakSoundPath));
                // 木箱からも武器が出ることがある（Breakables.csv の weapon）ので、どちらにも付ける。
                root.AddComponent<BreakableWeaponDrop>();
                return PrefabUtility.SaveAsPrefabAsset(root, $"{BreakablesFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>火の光（FlickerLight）の、層の松明の強さ・距離に掛ける倍率。</summary>
        private readonly struct Flame
        {
            public readonly float Intensity;
            public readonly float Range;

            public Flame(float intensity, float range)
            {
                Intensity = intensity;
                Range = range;
            }
        }

        private static GameObject BuildProp(string name, string model, Vector3 scale, float yaw, bool solid, bool ground = true, Flame? flame = null)
        {
            GameObject root = Wrap(name, model, scale, yaw, ground);
            try
            {
                if (solid)
                {
                    Bounds b = BreakablePlantSetup.LocalBounds(root);
                    var box = root.AddComponent<BoxCollider>();
                    box.center = b.center;
                    box.size = b.size;
                }
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
                // 光は揺らす（位置も動かす）ので、静的にした後で足す。
                if (flame.HasValue) AddFlameLight(root, flame.Value);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PropsFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 炎の所（模型の上端。松明は壁から張り出した先）に点光源を足す。影は落とさない（数が多いので）。
        /// 色・強さ・距離は階を作るときに層の光と色から入るので、ここでは既定の暖かい光にしておく。
        /// </summary>
        private static void AddFlameLight(GameObject root, Flame flame)
        {
            Bounds b = BreakablePlantSetup.LocalBounds(root);
            var holder = new GameObject("Flame Light");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = new Vector3(b.center.x, b.max.y - 0.08f, b.max.z > 0.15f ? b.max.z - 0.1f : b.center.z);
            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.3f);
            light.intensity = 2f * flame.Intensity;
            light.range = 8f * flame.Range;
            light.shadows = LightShadows.None;
            var flicker = holder.AddComponent<FlickerLight>();
            var serialized = new SerializedObject(flicker);
            serialized.FindProperty("intensityScale").floatValue = flame.Intensity;
            serialized.FindProperty("rangeScale").floatValue = flame.Range;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
