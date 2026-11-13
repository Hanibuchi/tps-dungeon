using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 地上の草花を壊したときに出てくる敵の CSV（PlantSpawns.csv）を、地上の表（PlantSpawnTable_Surface）に書き込む。
    /// 敵は id（EnemyIdentity の id）で引く。地上の敵は Enemies.csv の layers を 0 にしておく（0 が無い敵を書いたら知らせる）。
    /// CSV か Enemies.csv を保存すると自動で取り込む。メニューからも呼べる。表が無ければ作る（<see cref="BreakablePlantSetup"/>）。
    /// </summary>
    public sealed class PlantSpawnCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = EnemySpawnCsvImporter.TablesFolder + "/PlantSpawns.csv";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/草花の敵", false, 109)]
        public static void ImportFromMenu() => Import(BreakablePlantSetup.EnsureAssets());

        /// <summary>CSV を読んで table に書く。書き換えたら真。</summary>
        public static bool Import(PlantSpawnTable table)
        {
            if (table == null) return false;
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、草花の敵の表を取り込めない。");
                return false;
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            List<PlantSpawnCsv.Row> rows = PlantSpawnCsv.Parse(File.ReadAllText(CsvPath), errors);
            Dictionary<string, GameObject> prefabs = EnemySpawnCsvImporter.EnemyPrefabs(warnings);
            HashSet<string> surfaceEnemies = SurfaceEnemies(errors);

            var entries = new List<PlantSpawnTable.Entry>();
            float total = 0f;
            foreach (PlantSpawnCsv.Row row in rows)
            {
                if (!prefabs.TryGetValue(row.EnemyId, out GameObject prefab))
                {
                    errors.Add($"{row.Line} 行目: 敵 {row.EnemyId} のプレハブが無い（{EnemyIdentitySetup.PrefabFolder} の敵の EnemyIdentity の id で書く）");
                    continue;
                }

                if (surfaceEnemies != null && !surfaceEnemies.Contains(row.EnemyId))
                {
                    warnings.Add($"{row.Line} 行目: {row.EnemyId} は {EnemySpawnCsvImporter.EnemiesCsvPath} の layers に 0（地上）が無い");
                }

                entries.Add(new PlantSpawnTable.Entry { enemy = prefab, chance = row.Chance });
                total += row.Chance;
            }

            if (total > 1f) warnings.Add($"chance の合計 {total:0.###} が 1 を超えている（比に縮めて振るので、何も出ないことが無くなる）");

            foreach (string warning in warnings) Debug.LogWarning($"草花の敵の CSV（{CsvPath}）: {warning}");
            foreach (string error in errors) Debug.LogError($"草花の敵の CSV（{CsvPath}）の {error}");

            string before = EditorJsonUtility.ToJson(table);
            table.SetEntries(entries);
            bool changed = EditorJsonUtility.ToJson(table) != before;
            if (changed)
            {
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"草花の敵の CSV を取り込んだ: 敵 {entries.Count} 種、出る確率の合計 {total:P0}" + (changed ? "（表を書き換えた）" : "（表は変わらない）"));
            return changed;
        }

        /// <summary>Enemies.csv で layers に 0 を持つ敵の id。読めなければ null（確かめない）。</summary>
        private static HashSet<string> SurfaceEnemies(List<string> errors)
        {
            if (!File.Exists(EnemySpawnCsvImporter.EnemiesCsvPath)) return null;

            // Enemies.csv の読めない行は EnemySpawnCsvImporter が知らせるので、ここでは捨てる。
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(File.ReadAllText(EnemySpawnCsvImporter.EnemiesCsvPath), new List<string>());
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyCsv.Row row in rows)
            {
                if (Array.IndexOf(row.Layers, 0) >= 0) ids.Add(row.Id);
            }
            return ids;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0 && Array.IndexOf(importedAssets, EnemySpawnCsvImporter.EnemiesCsvPath) < 0) return;

            // 取り込み中は敵のプレハブがまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= ImportFromMenu;
            EditorApplication.delayCall += ImportFromMenu;
        }
    }
}
