using System.Collections.Generic;
using TpsDungeon.Map.Authoring;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 層ごとの壁・天井・階段・ドアの素材を決めて各層のテーマ（L{n}_Theme）に差し、その層の壁・ドア・階段の部品を作り直す。天井は壁と同じ素材にする。
    /// 指定があれば、その層の部屋（ランダムな階とボス階のカタログ）の床の素材も差し替える。
    /// 素材は 2 通り：
    /// - 元の素材をそのまま使う（Yughues の舗石・仮の単色など）。
    /// - 元の素材の写し（Assets/_Project/Materials/Floors/L{n}_*.mat）の色（_BaseColor）を層の色味にしたもの。写しが既にあれば元から写し直す（GUID は変わらない）。
    ///   元の素材をそのまま使うことにした所の古い写しは消す。
    /// 階段のある部屋も他の部屋と同じ壁・床・天井に見えるよう、階段まわり（テーマの stair）は床と同じ素材にする（各層とも壁と床を同じ素材にしてある）。
    /// </summary>
    public static class FloorMaterialBuilder
    {
        private const string Folder = "Assets/_Project/Materials/Floors";
        private const string Pavements = "Assets/ThirdParty/Material/YughuesFreePavementsMaterials/Materials/";
        private const string Flooring = "Assets/ThirdParty/Material/YughuesFreeFlooringMaterials/Materials/";
        private const string Placeholder = "Assets/_Project/Materials/Placeholder/";
        private const string ThemeFolder = "Assets/_Project/Settings/Map/";

        private readonly struct Look
        {
            public readonly string Source;
            /// <summary>写しに入れる色。null なら元の素材をそのまま使う。</summary>
            public readonly Color? Tint;

            public Look(string source, Color? tint)
            {
                Source = source;
                Tint = tint;
            }

            public bool IsSet => Source != null;
        }

        private readonly struct LayerLook
        {
            public readonly Look Wall, DoorBrick, DoorLeaf, RoomFloor;

            public LayerLook(Look wall, Look doorBrick, Look doorLeaf, Look roomFloor = default)
            {
                Wall = wall;
                DoorBrick = doorBrick;
                DoorLeaf = doorLeaf;
                RoomFloor = roomFloor;
            }
        }

        // 元の素材そのまま（P・Ph）か、色を変えた写し（Pc・Fc・Phc）。
        private static Look P(string name) => new Look(Pavements + name + ".mat", null);
        private static Look Ph(string name) => new Look(Placeholder + name + ".mat", null);
        private static Look Pc(string name, float r, float g, float b) => new Look(Pavements + name + ".mat", new Color(r, g, b, 1f));
        private static Look Fc(string name, float r, float g, float b) => new Look(Flooring + name + ".mat", new Color(r, g, b, 1f));
        private static Look Phc(string name, float r, float g, float b) => new Look(Placeholder + name + ".mat", new Color(r, g, b, 1f));

        // 壁・床・天井は層ごとに同じ舗石（床の素材は部屋のプレハブ、指定のある層は RoomFloor で差し替える）。扉板は木か仮の単色。
        private static readonly LayerLook[] Looks =
        {
            // 1 層：割れた石畳。ドアの扉板と縁のレンガは仮の単色のまま。
            new LayerLook(P("M_YFPM_BrokenRectangles"), Ph("Placeholder_Brick"), Ph("Placeholder_DoorLeaf")),
            // 2 層：地中海風の石積み。縁のレンガは仮の茶色の単色。
            new LayerLook(P("M_YFPM_Mediterranean01"), Ph("Placeholder_Brick"), Fc("M_YFFlM_01", 0.9f, 0.88f, 0.85f)),
            // 3 層：灰色の石積み（床も同じに）。縁のレンガは灰色の単色。
            new LayerLook(P("M_YFPM_Rough01"), Phc("Placeholder_Brick", 0.42f, 0.42f, 0.42f), Fc("M_YFFlM_02", 1f, 1f, 1f),
                roomFloor: P("M_YFPM_Rough01")),
            // 4 層：舗道の石。
            new LayerLook(P("M_YFPM_Pavement01"), Pc("M_YFPM_Rough01", 0.6f, 0.62f, 0.72f), Fc("M_YFFlM_07", 0.9f, 0.9f, 0.95f)),
            // 5 層：スレート。縁のレンガは黒い石。
            new LayerLook(P("M_YFPM_Slates"), P("M_YFPM_SmoothSquare"), Fc("M_YFFlM_12", 0.85f, 0.8f, 0.9f)),
        };

        [MenuItem("Tools/TPS Dungeon/階の素材を作る")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            EnsureFolder(Folder);
            var changed = new List<FloorConfig>();
            var stale = new List<string>();
            int themes = 0, floors = 0;
            for (int i = 0; i < Looks.Length; i++)
            {
                int layer = i + 1;
                var theme = AssetDatabase.LoadAssetAtPath<FloorTheme>($"{ThemeFolder}L{layer}_Theme.asset");
                if (theme == null)
                {
                    Debug.LogWarning($"L{layer}_Theme が無いので、層 {layer} の素材を差さなかった");
                    continue;
                }

                LayerLook look = Looks[i];
                Material wall = Make($"L{layer}_Wall", look.Wall, stale);
                theme.wall = wall;
                // 階段まわり（下りの穴の空いた床・竪穴の壁）は、部屋の床・壁と同じ素材にする。
                theme.stair = look.RoomFloor.IsSet ? Make($"L{layer}_Floor", look.RoomFloor, stale) : wall;
                // 天井も壁と同じ素材にする。前に作った天井の写しは消す。
                theme.ceiling = wall;
                string oldCeiling = $"{Folder}/L{layer}_Ceiling.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(oldCeiling) != null && !stale.Contains(oldCeiling)) stale.Add(oldCeiling);
                theme.doorBrick = Make($"L{layer}_DoorBrick", look.DoorBrick, stale);
                theme.doorLeaf = Make($"L{layer}_DoorLeaf", look.DoorLeaf, stale);
                EditorUtility.SetDirty(theme);
                themes++;

                var configs = new List<FloorConfig>();
                foreach (string guid in AssetDatabase.FindAssets("t:FloorConfig"))
                {
                    var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    if (config != null && config.theme == theme) configs.Add(config);
                }
                changed.AddRange(configs);
                if (look.RoomFloor.IsSet) floors += SetRoomFloors(configs, theme.stair);
            }

            AssetDatabase.SaveAssets();
            PlaceholderMapAssetGenerator.RebuildParts(changed);
            // 元の素材をそのまま使うことにした所の、前に作った写しを消す（部品を作り直したので、もうどこからも使われない）。
            foreach (string path in stale) AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            return $"階の素材を差した（テーマ {themes} つ、床を替えた部屋 {floors} 個）。その層の壁・ドア・天井・階段の部品を作り直した（階の設定 {changed.Count} つ）。";
        }

        /// <summary>
        /// 素材を返す。色の指定が無ければ元の素材そのもの（前に作った写し Folder/name.mat があれば stale に積む）。
        /// 指定があれば写しを作り（あれば写し直し）、色を入れる。元が無ければ null。
        /// </summary>
        private static Material Make(string name, Look look, List<string> stale)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(look.Source);
            if (source == null)
            {
                Debug.LogWarning($"素材が無い: {look.Source}");
                return null;
            }

            string path = $"{Folder}/{name}.mat";
            if (!look.Tint.HasValue)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null && !stale.Contains(path)) stale.Add(path);
                return source;
            }

            stale.Remove(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            // 元の素材の色は素材ごとにまちまち（Slates・SmoothSquare は 0.1 と黒寄り）なので、掛けずに指定の色をそのまま入れる。
            Color tint = look.Tint.Value;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>configs のカタログの部屋のプレハブの床（子の Floor）の素材を material にする。変えた部屋の数を返す。</summary>
        private static int SetRoomFloors(List<FloorConfig> configs, Material material)
        {
            if (material == null) return 0;
            var done = new HashSet<GameObject>();
            int count = 0;
            foreach (FloorConfig config in configs)
            {
                if (config.roomCatalog == null) continue;
                foreach (var entry in config.roomCatalog.Entries)
                {
                    if (entry.prefab == null || !done.Add(entry.prefab)) continue;
                    var floor = entry.prefab.transform.Find("Floor");
                    if (floor == null || !floor.TryGetComponent(out MeshRenderer renderer) || renderer.sharedMaterial == material) continue;

                    string path = AssetDatabase.GetAssetPath(entry.prefab);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var target = root.transform.Find("Floor").GetComponent<MeshRenderer>();
                        var materials = target.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) materials[i] = material;
                        target.sharedMaterials = materials;
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        count++;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
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
