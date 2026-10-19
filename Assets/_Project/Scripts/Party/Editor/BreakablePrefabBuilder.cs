using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Enemies.Editor;
using TpsDungeon.Items;
using TpsDungeon.Map.Authoring;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 置き物のプレハブを作り、ダンジョンの部屋に置く一覧（<see cref="RoomPropSet"/>）にまとめて、全部の階の設定に差す。
    /// - 壊せる草花（Prefabs/Map/Breakables/Breakable_rpgpp_lt_*）：RPGPP の草・花・草木のバリアントに、壊せるようにする物（BreakablePlantSetup.MakeBreakable）を書き込む。
    ///   地上の景色（SurfaceScenery）はこれを置く。出てくる敵は地上の表（PlantSpawnTable_Surface）。
    /// - 壊せる木箱・宝箱（Breakables/Crate・Chest）：Free Low Poly Dungeon Pack の箱に、壊せるようにする物（MakeBreakableBox）を書き込む。
    ///   出てくるものは Breakables.csv の表（BreakableDrops）。武器が出たら BreakableWeaponDrop が床に出す。
    /// - 部屋の飾り（Prefabs/Map/Props/Prop_*）：同じパックの松明・柱・机・椅子・小物。盾は拾える武器（盾）と見分けがつかないので使わない。
    /// 部屋のプレハブはどれも、原点が床（松明は壁の面）で、正面（宝箱の錠・椅子の座る向き・松明の火）が +Z を向くように中の模型を回してある。
    /// 机・椅子・柱は当たりを付けて静的にする（NavMesh に焼き込まれる）。小物と松明は当たりを付けない。
    /// ThirdParty は git に上げないので、本体のエディタで回す。
    /// </summary>
    public static class BreakablePrefabBuilder
    {
        public const string BreakablesFolder = "Assets/_Project/Prefabs/Map/Breakables";
        public const string PropsFolder = "Assets/_Project/Prefabs/Map/Props";
        public const string PropSetPath = "Assets/_Project/Settings/Map/RoomPropSet_Dungeon.asset";
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

        [MenuItem("Tools/TPS Dungeon/置き物のプレハブを作る（草花・木箱・宝箱・部屋の飾り）")]
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
            var set = LoadOrCreatePropSet();
            set.crates = new List<GameObject> { BuildBox("Crate", "SM_Crate_01", 1.25f, 0f, drops, BreakableDropKind.Crate) };
            set.chests = new List<GameObject> { BuildBox("Chest", "SM_Chest_Wooden_Locked", 1.2f, 90f, drops, BreakableDropKind.Chest) };

            // 正面の向き：宝箱・椅子・松明は模型の -X が正面なので、+Z へ回す。
            GameObject torch = BuildProp("Prop_Torch", "SM_Torch_Mount_Large_Lit", Vector3.one * 1.3f, 90f, false, ground: false);
            GameObject pillar = BuildProp("Prop_Pillar", "SM_Pillar_Stone_Full_TrimDouble", new Vector3(2.5f, 2.5f, 2.5f), 0f, true);
            GameObject table = BuildProp("Prop_Table", "SM_Table", Vector3.one, 0f, true);
            GameObject chair = BuildProp("Prop_Chair", "SM_Chair", Vector3.one * 0.9f, 90f, true);
            GameObject candles = BuildProp("Prop_Candles", "SM_Candle_Group_Small_Lit", Vector3.one * 1.2f, 0f, false);
            GameObject books = BuildProp("Prop_Books", "SM_Book_Pile_Small", Vector3.one, 0f, false);
            GameObject bonesSmall = BuildProp("Prop_Bones_Small", "SM_Bone_Pile_Small", Vector3.one * 1.3f, 0f, false);
            GameObject bonesMedium = BuildProp("Prop_Bones_Medium", "SM_Bone_Pile_Medium", Vector3.one * 1.3f, 0f, false);
            GameObject potion = BuildProp("Prop_Potion", "SM_Potion_Medium_Empty", Vector3.one * 1.2f, 0f, false);

            set.torches = new List<GameObject> { torch };
            set.pillars = new List<GameObject> { pillar };
            set.tables = new List<GameObject> { table };
            set.chairs = new List<GameObject> { chair };
            set.tableTops = new List<GameObject> { candles, candles, books };
            set.wallClutter = new List<GameObject> { bonesSmall, bonesMedium, books, potion, candles };
            EditorUtility.SetDirty(set);

            int configs = AssignToConfigs(set);
            AssetDatabase.SaveAssets();
            return $"置き物のプレハブを作った（壊せる草花 {plants} 種、木箱・宝箱、部屋の飾り 9 種）。{PropSetPath} を階の設定 {configs} つに差した。";
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

        private static GameObject BuildProp(string name, string model, Vector3 scale, float yaw, bool solid, bool ground = true)
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
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PropsFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- 一覧と設定 ----

        private static RoomPropSet LoadOrCreatePropSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<RoomPropSet>(PropSetPath);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<RoomPropSet>();
            AssetDatabase.CreateAsset(set, PropSetPath);
            return set;
        }

        /// <summary>道順の全部の層のランダムな階とボス階の設定に、まだ何も差していなければ set を差す（層ごとに差し替えた物は残す）。差した数を返す。</summary>
        private static int AssignToConfigs(RoomPropSet set)
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route == null) return 0;
            int count = 0;
            foreach (DungeonRoute.Layer layer in route.Layers)
            {
                foreach (FloorConfig config in new[] { layer.randomConfig, layer.bossConfig })
                {
                    if (config == null || config.propSet != null) continue;
                    config.propSet = set;
                    EditorUtility.SetDirty(config);
                    count++;
                }
            }
            return count;
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
