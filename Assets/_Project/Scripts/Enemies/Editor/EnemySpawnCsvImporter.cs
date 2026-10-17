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
    /// 部屋の敵の CSV 2 つを、道順（DungeonRoute）の層ごとの表（EnemySpawnTable）に書き込む。
    ///   Enemies.csv          … 敵の台帳。id・コスト・出る層（今後 hp などの列を足す）
    ///   EnemySpawnLayers.csv … 層ごとのセル 1 つあたりの予算・モンスターハウスの確率と倍率
    /// 表の敵の一覧・予算・確率・倍率を差し替える。編成タイプと音は表のまま（Inspector で直す）。
    /// 道順の層に表が無ければ「部屋の敵の表を作る」で作ってから書く。
    /// どちらかの CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// 敵は id（EnemyIdentity の id）で引く。読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class EnemySpawnCsvImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Enemies";
        public const string EnemiesCsvPath = Folder + "/Enemies.csv";
        public const string LayersCsvPath = Folder + "/EnemySpawnLayers.csv";

        [MenuItem("Tools/TPS Dungeon/Enemies/敵の CSV を取り込む")]
        public static void Import()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            List<EnemyCsv.Row> enemyRows = Read(EnemiesCsvPath, EnemyCsv.Parse, errors);
            List<EnemySpawnLayerCsv.Row> layerRows = Read(LayersCsvPath, EnemySpawnLayerCsv.Parse, errors);

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
            foreach (EnemyCsv.Row row in enemyRows)
            {
                if (row.Layers.Length == 0) continue;
                if (!prefabs.ContainsKey(row.Id))
                {
                    errors.Add($"{EnemiesCsvPath} の {row.Line} 行目: 敵 {row.Id} のプレハブが無い（{EnemyIdentitySetup.PrefabFolder} の敵の EnemyIdentity の id で書く）");
                    continue;
                }

                foreach (int layer in row.Layers)
                {
                    if (layer > route.Layers.Count)
                    {
                        warnings.Add($"{EnemiesCsvPath} の {row.Line} 行目: {row.Id} の層 {layer} は道順に無い（{route.Layers.Count} 層まで）");
                    }
                }
                usable.Add(row);
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
                var onLayer = new HashSet<GameObject>();
                foreach (EnemyCsv.Row row in usable)
                {
                    if (Array.IndexOf(row.Layers, layer) < 0) continue;
                    enemies.Add(new EnemySpawnTable.Enemy { id = row.Id, prefab = prefabs[row.Id], cost = row.Cost });
                    onLayer.Add(prefabs[row.Id]);
                }

                float perCell = table.BudgetPerCell, chance = table.MonsterHouseChance, multiplier = table.MonsterHouseBudgetMultiplier;
                if (layerValues.TryGetValue(layer, out EnemySpawnLayerCsv.Row values))
                {
                    perCell = values.BudgetPerCell;
                    chance = values.MonsterHouseChance;
                    multiplier = values.MonsterHouseMultiplier;
                }
                else
                {
                    warnings.Add($"{LayersCsvPath} に層 {layer} の行が無いので、{table.name} の予算・モンスターハウスの値はそのまま");
                }

                if (enemies.Count == 0) warnings.Add($"層 {layer} に出る敵が {EnemiesCsvPath} に 1 体も無い（部屋に敵が出ない）");
                WarnUnusedShares(table, onLayer, layer, warnings);

                if (Write(table, perCell, chance, multiplier, enemies)) changed++;
            }

            foreach (string warning in warnings) Debug.LogWarning($"部屋の敵の CSV: {warning}");
            foreach (string error in errors) Debug.LogError($"部屋の敵の CSV の {error}");
            Debug.Log($"部屋の敵の CSV を取り込んだ: 敵 {usable.Count} 種、層 {route.Layers.Count} つのうち {changed} つの表を書き換えた");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, EnemiesCsvPath) < 0 && Array.IndexOf(importedAssets, LayersCsvPath) < 0) return;

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

            if (prefabs.Count == 0) warnings.Add("id（EnemyIdentity）の付いた敵のプレハブが無い。メニュー「Tools/TPS Dungeon/Enemies/敵に id を付ける」を先に実行すること。");
            return prefabs;
        }

        /// <summary>編成タイプがその層に出ない敵を指していたら知らせる（その比率は使われない）。</summary>
        private static void WarnUnusedShares(EnemySpawnTable table, HashSet<GameObject> onLayer, int layer, List<string> warnings)
        {
            var missing = new SortedSet<string>(StringComparer.Ordinal);
            foreach (EnemySpawnTable.Composition composition in table.Compositions)
            {
                if (composition.shares == null) continue;
                foreach (EnemySpawnTable.Share share in composition.shares)
                {
                    if (share.enemy != null && !onLayer.Contains(share.enemy)) missing.Add(share.enemy.name);
                }
            }

            if (missing.Count > 0)
            {
                warnings.Add($"{table.name} の編成タイプに、層 {layer} に出ない敵（{string.Join("・", missing)}）が入っている。その比率は使われない");
            }
        }

        /// <summary>表が今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(EnemySpawnTable table, float perCell, float chance, float multiplier, List<EnemySpawnTable.Enemy> enemies)
        {
            string before = EditorJsonUtility.ToJson(table);
            table.Assign(perCell, chance, multiplier, enemies);
            if (EditorJsonUtility.ToJson(table) == before) return false;

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
