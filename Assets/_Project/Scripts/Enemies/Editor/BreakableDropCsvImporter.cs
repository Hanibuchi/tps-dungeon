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
    /// 箱（木箱・宝箱）の中身の CSV 2 つを取り込む。CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// - Breakables.csv：敵・武器・何も出ないの重み。どの層でも同じ。<see cref="BreakableDropTable"/>（Settings/Map/BreakableDrops.asset）に書く（無ければ作る）。
    /// - BreakableWeaponRanks.csv：武器が出たときのランクの重み。層ごと。道順の各層のランダムな階の FloorConfig.boxWeaponRanks に書く。
    /// </summary>
    public sealed class BreakableDropCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/Breakables.csv";
        public const string TablePath = "Assets/_Project/Settings/Map/BreakableDrops.asset";
        public const string RanksCsvPath = "Assets/_Project/Settings/Tables/BreakableWeaponRanks.csv";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/箱の中身", false, 108)]
        public static void ImportFromMenu() => Import();

        /// <summary>CSV を読んで表に書き、表を返す。CSV が無ければ表をそのまま返す（無ければ null）。</summary>
        public static BreakableDropTable Import()
        {
            BreakableDropTable table = EnsureTable();
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、箱の中身の CSV を取り込めない。");
                return table;
            }

            var errors = new List<string>();
            List<BreakableDropCsv.Row> rows = BreakableDropCsv.Parse(File.ReadAllText(CsvPath), errors);
            foreach (string error in errors) Debug.LogError($"箱の中身の CSV（{CsvPath}）の {error}");

            var entries = new List<BreakableDropTable.Entry>();
            foreach (BreakableDropCsv.Row row in rows)
            {
                entries.Add(new BreakableDropTable.Entry
                {
                    kind = row.Kind,
                    enemy = row.Enemy,
                    weapon = row.Weapon,
                    nothing = row.Nothing,
                });
            }
            foreach (BreakableDropKind kind in (BreakableDropKind[])Enum.GetValues(typeof(BreakableDropKind)))
            {
                if (!entries.Exists(e => e.kind == kind)) Debug.LogWarning($"箱の中身の CSV: {kind.ToString().ToLowerInvariant()} の行が無いので、その箱からは何も出ない");
            }

            string before = EditorJsonUtility.ToJson(table);
            table.SetEntries(entries);
            if (EditorJsonUtility.ToJson(table) != before)
            {
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"箱の中身の CSV を取り込んだ: {rows.Count} 行");
            ImportRanks();
            return table;
        }

        /// <summary>層ごとの武器のランクの重みを、道順の各層のランダムな階の設定に書く。行の無い層は変えない。</summary>
        private static void ImportRanks()
        {
            if (!File.Exists(RanksCsvPath))
            {
                Debug.LogError($"{RanksCsvPath} が無いので、箱から出る武器のランクを取り込めない。");
                return;
            }
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            if (route == null)
            {
                Debug.LogError($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、箱から出る武器のランクを取り込めない。");
                return;
            }

            var errors = new List<string>();
            List<BreakableWeaponRankCsv.Row> rows = BreakableWeaponRankCsv.Parse(File.ReadAllText(RanksCsvPath), errors);
            foreach (string error in errors) Debug.LogError($"箱の武器のランクの CSV（{RanksCsvPath}）の {error}");

            var byLayer = new Dictionary<int, BreakableWeaponRankCsv.Row>();
            foreach (BreakableWeaponRankCsv.Row row in rows)
            {
                if (row.Layer > route.Layers.Count) Debug.LogWarning($"箱の武器のランクの CSV: {row.Line} 行目の層 {row.Layer} は道順に無い（{route.Layers.Count} 層まで）");
                else byLayer[row.Layer] = row;
            }

            int changed = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                FloorConfig config = route.Layers[i].randomConfig;
                if (config == null) continue;
                if (!byLayer.TryGetValue(i + 1, out BreakableWeaponRankCsv.Row row))
                {
                    Debug.LogWarning($"箱の武器のランクの CSV: 層 {i + 1} の行が無いので、{config.name} のランクはそのままにした");
                    continue;
                }
                if (config.boxWeaponRanks != null && System.Linq.Enumerable.SequenceEqual(config.boxWeaponRanks, row.Weights)) continue;
                config.boxWeaponRanks = (float[])row.Weights.Clone();
                EditorUtility.SetDirty(config);
                changed++;
            }
            if (changed > 0) AssetDatabase.SaveAssets();
            Debug.Log($"箱の武器のランクの CSV を取り込んだ: {rows.Count} 行、{changed} つの階の設定を書き換えた");
        }

        private static BreakableDropTable EnsureTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<BreakableDropTable>(TablePath);
            if (table != null) return table;

            table = ScriptableObject.CreateInstance<BreakableDropTable>();
            AssetDatabase.CreateAsset(table, TablePath);
            return table;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0 && Array.IndexOf(importedAssets, RanksCsvPath) < 0) return;

            EditorApplication.delayCall -= ImportFromMenu;
            EditorApplication.delayCall += ImportFromMenu;
        }
    }
}
