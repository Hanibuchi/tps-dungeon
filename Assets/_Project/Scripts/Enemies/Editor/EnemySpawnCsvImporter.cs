using System;
using System.Collections.Generic;
using System.IO;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Editor;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 部屋の敵の CSV 3 つを、道順（DungeonRoute）の層ごとの表（EnemySpawnTable）に書き込む。
    ///   Enemies.csv             … 敵の台帳。id・コスト・出る層（今後 hp などの列を足す）
    ///   EnemySpawnLayers.csv    … 層ごとのセル 1 つあたりの予算・モンスターハウスの確率と倍率・単種の重み
    ///   EnemyCompositions.csv   … 編成タイプ（混成・主役＋脇役など）。単種は書かず、層に出る敵ごとにここで作る
    /// 表の敵の一覧・予算・確率・倍率・編成タイプを差し替える。音は表のまま（Inspector で直す）。
    /// 道順の層に表が無ければ「部屋の敵の表を作る」で作ってから書く。
    /// どれかの CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// 敵は id（EnemyIdentity の id）で引く。読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class EnemySpawnCsvImporter : AssetPostprocessor
    {
        public const string TablesFolder = "Assets/_Project/Settings/Tables";
        public const string EnemiesCsvPath = TablesFolder + "/Enemies.csv";
        public const string LayersCsvPath = TablesFolder + "/EnemySpawnLayers.csv";
        public const string CompositionsCsvPath = TablesFolder + "/EnemyCompositions.csv";

        private static readonly string[] CsvPaths = { EnemiesCsvPath, LayersCsvPath, CompositionsCsvPath };

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/敵", false, 107)]
        public static void Import()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            List<EnemyCsv.Row> enemyRows = Read(EnemiesCsvPath, EnemyCsv.Parse, errors);
            List<EnemySpawnLayerCsv.Row> layerRows = Read(LayersCsvPath, EnemySpawnLayerCsv.Parse, errors);
            List<EnemyCompositionCsv.Row> compositionRows = Read(CompositionsCsvPath, EnemyCompositionCsv.Parse, errors);

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            if (route == null)
            {
                Debug.LogError($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、部屋の敵の CSV を取り込めない。先に「ダンジョンの道順を作る」を回すこと。");
                return;
            }

            bool missingTable = false;
            foreach (DungeonRoute.Layer layer in route.Layers) missingTable |= layer.enemySpawns == null;
            if (missingTable) PlaceholderMapAssetGenerator.GenerateEnemySpawnTables();

            Dictionary<string, GameObject> prefabs = EnemyPrefabs(warnings);
            var usable = new List<EnemyCsv.Row>();
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyCsv.Row row in enemyRows)
            {
                listed.Add(row.Id);
                if (row.Layers.Length == 0) continue;
                if (!prefabs.ContainsKey(row.Id))
                {
                    errors.Add($"{EnemiesCsvPath} の {row.Line} 行目: 敵 {row.Id} のプレハブが無い（{EnemyIdentitySetup.PrefabFolder} の敵の EnemyIdentity の id で書く）");
                    continue;
                }

                WarnLayersOutsideRoute(row.Layers, route, $"{EnemiesCsvPath} の {row.Line} 行目: {row.Id}", warnings);
                usable.Add(row);
            }

            foreach (EnemyCompositionCsv.Row row in compositionRows)
            {
                WarnLayersOutsideRoute(row.Layers, route, $"{CompositionsCsvPath} の {row.Line} 行目: {row.Name}", warnings);
                foreach (EnemyCompositionCsv.Share share in row.Shares)
                {
                    if (!listed.Contains(share.EnemyId)) errors.Add($"{CompositionsCsvPath} の {row.Line} 行目: {row.Name} の敵 {share.EnemyId} が {EnemiesCsvPath} に無い");
                }
            }

            var layerValues = new Dictionary<int, EnemySpawnLayerCsv.Row>();
            foreach (EnemySpawnLayerCsv.Row row in layerRows)
            {
                if (row.Layer > route.Layers.Count) warnings.Add($"{LayersCsvPath} の {row.Line} 行目: 層 {row.Layer} は道順に無い（{route.Layers.Count} 層まで）");
                else layerValues[row.Layer] = row;
            }

            int changed = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                int layer = i + 1;
                EnemySpawnTable table = route.Layers[i].enemySpawns;
                if (table == null)
                {
                    errors.Add($"層 {layer} に部屋の敵の表が無い");
                    continue;
                }

                var enemies = new List<EnemySpawnTable.Enemy>();
                foreach (EnemyCsv.Row row in usable)
                {
                    if (Array.IndexOf(row.Layers, layer) >= 0) enemies.Add(new EnemySpawnTable.Enemy { id = row.Id, prefab = prefabs[row.Id], cost = row.Cost });
                }

                float perCell = table.BudgetPerCell, chance = table.MonsterHouseChance, multiplier = table.MonsterHouseBudgetMultiplier, singleWeight = 0f;
                RoomBudgetBasis basis = table.BudgetBasis;
                if (layerValues.TryGetValue(layer, out EnemySpawnLayerCsv.Row values))
                {
                    perCell = values.BudgetPerCell;
                    basis = values.BudgetBasis;
                    chance = values.MonsterHouseChance;
                    multiplier = values.MonsterHouseMultiplier;
                    singleWeight = values.SingleWeight;
                }
                else
                {
                    warnings.Add($"{LayersCsvPath} に層 {layer} の行が無いので、{table.name} の予算・モンスターハウスの値はそのままにし、単種は作らない");
                }

                if (enemies.Count == 0) warnings.Add($"層 {layer} に出る敵が {EnemiesCsvPath} に 1 体も無い（部屋に敵が出ない）");
                List<EnemySpawnTable.Composition> compositions = Compositions(compositionRows, enemies, layer, singleWeight, warnings);

                if (Write(table, perCell, basis, chance, multiplier, enemies, compositions)) changed++;
            }

            foreach (string warning in warnings) Debug.LogWarning($"部屋の敵の CSV: {warning}");
            foreach (string error in errors) Debug.LogError($"部屋の敵の CSV の {error}");
            Debug.Log($"部屋の敵の CSV を取り込んだ: 敵 {usable.Count} 種、編成 {compositionRows.Count} 行、" +
                      $"層 {route.Layers.Count} つのうち {changed} つの表を書き換えた");
        }

        /// <summary>
        /// その層の編成タイプ。CSV の編成のうちこの層を使うもの（* はその層の敵全部を比率 1 で）と、その層の敵ごとの単種。
        /// その層に出ない敵の比率は入れず知らせる。1 体も使えない編成は入れない。
        /// </summary>
        private static List<EnemySpawnTable.Composition> Compositions(List<EnemyCompositionCsv.Row> rows, List<EnemySpawnTable.Enemy> enemies,
            int layer, float singleWeight, List<string> warnings)
        {
            var byId = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (EnemySpawnTable.Enemy e in enemies) byId[e.id] = e.prefab;

            var compositions = new List<EnemySpawnTable.Composition>();
            foreach (EnemyCompositionCsv.Row row in rows)
            {
                if (Array.IndexOf(row.Layers, layer) < 0) continue;

                var shares = new List<EnemySpawnTable.Share>();
                if (row.AllEnemies)
                {
                    foreach (EnemySpawnTable.Enemy e in enemies) shares.Add(new EnemySpawnTable.Share { enemy = e.prefab, share = 1f });
                }
                else
                {
                    var absent = new List<string>();
                    foreach (EnemyCompositionCsv.Share share in row.Shares)
                    {
                        if (byId.TryGetValue(share.EnemyId, out GameObject prefab)) shares.Add(new EnemySpawnTable.Share { enemy = prefab, share = share.Value });
                        else absent.Add(share.EnemyId);
                    }

                    if (absent.Count > 0 && shares.Count > 0)
                    {
                        warnings.Add($"{CompositionsCsvPath} の {row.Line} 行目: {row.Name} の {string.Join("・", absent)} は層 {layer} に出ないので、層 {layer} ではほかの敵だけにする");
                    }
                }

                if (shares.Count == 0)
                {
                    if (enemies.Count > 0) warnings.Add($"{CompositionsCsvPath} の {row.Line} 行目: {row.Name} の敵が層 {layer} に 1 体も出ないので、層 {layer} には入れない");
                    continue;
                }

                compositions.Add(new EnemySpawnTable.Composition { name = row.Name, weight = row.Weight, shares = shares });
            }

            if (singleWeight > 0f)
            {
                foreach (EnemySpawnTable.Enemy e in enemies)
                {
                    compositions.Add(new EnemySpawnTable.Composition
                    {
                        name = $"単種（{e.id}）",
                        weight = singleWeight,
                        shares = new List<EnemySpawnTable.Share> { new EnemySpawnTable.Share { enemy = e.prefab, share = 1f } },
                    });
                }
            }

            return compositions;
        }

        private static void WarnLayersOutsideRoute(int[] layers, DungeonRoute route, string where, List<string> warnings)
        {
            foreach (int layer in layers)
            {
                if (layer > route.Layers.Count) warnings.Add($"{where} の層 {layer} は道順に無い（{route.Layers.Count} 層まで）");
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!Array.Exists(CsvPaths, path => Array.IndexOf(importedAssets, path) >= 0)) return;

            // 取り込み中はほかのアセット（敵のプレハブ・表）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        private static List<T> Read<T>(string path, Func<string, List<string>, List<T>> parse, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add($"{path} が無い");
                return new List<T>();
            }

            var parseErrors = new List<string>();
            List<T> rows = parse(File.ReadAllText(path), parseErrors);
            foreach (string error in parseErrors) errors.Add($"{path} の {error}");
            return rows;
        }

        /// <summary>id → 敵のプレハブ（Prefabs/Enemies の EnemyIdentity）。</summary>
        private static Dictionary<string, GameObject> EnemyPrefabs(List<string> warnings)
        {
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyIdentitySetup.PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var identity = prefab != null ? prefab.GetComponent<EnemyIdentity>() : null;
                if (identity == null || string.IsNullOrEmpty(identity.Id)) continue;

                if (prefabs.TryGetValue(identity.Id, out GameObject other)) warnings.Add($"id {identity.Id} の敵が 2 つある（{other.name}・{prefab.name}）。{other.name} を使う");
                else prefabs[identity.Id] = prefab;
            }

            if (prefabs.Count == 0) warnings.Add("id（EnemyIdentity）の付いた敵のプレハブが無い。メニュー「Tools/TPS Dungeon/敵/敵に id を付ける」を先に実行すること。");
            return prefabs;
        }

        /// <summary>表が今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(EnemySpawnTable table, float perCell, RoomBudgetBasis basis, float chance, float multiplier, List<EnemySpawnTable.Enemy> enemies,
            List<EnemySpawnTable.Composition> compositions)
        {
            string before = EditorJsonUtility.ToJson(table);
            table.Assign(perCell, basis, chance, multiplier, enemies, compositions);
            if (EditorJsonUtility.ToJson(table) == before) return false;

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
