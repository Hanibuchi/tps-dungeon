using System;
using System.Collections.Generic;
using System.IO;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 層ごとの階の光と色（Settings/Tables/FloorMoods.csv）を、道順（DungeonRoute）の各層のランダムな階とボス階の FloorConfig.mood に書き込む。
    /// 行の無い層は変えない。CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// </summary>
    public sealed class FloorMoodCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/FloorMoods.csv";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/階の光と色", false, 103)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、階の光と色の CSV を取り込めない。");
                return;
            }
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            if (route == null)
            {
                Debug.LogError($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、階の光と色の CSV を取り込めない。");
                return;
            }

            var errors = new List<string>();
            List<FloorMoodCsv.Row> rows = FloorMoodCsv.Parse(File.ReadAllText(CsvPath), errors);
            foreach (string error in errors) Debug.LogError($"階の光と色の CSV（{CsvPath}）の {error}");

            var byLayer = new Dictionary<int, FloorMoodCsv.Row>();
            foreach (FloorMoodCsv.Row row in rows)
            {
                if (row.Layer > route.Layers.Count) Debug.LogWarning($"階の光と色の CSV: {row.Line} 行目の層 {row.Layer} は道順に無い（{route.Layers.Count} 層まで）");
                else byLayer[row.Layer] = row;
            }

            int changed = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                if (!byLayer.TryGetValue(i + 1, out FloorMoodCsv.Row row))
                {
                    Debug.LogWarning($"階の光と色の CSV: 層 {i + 1} の行が無いので、その層の光と色はそのままにした");
                    continue;
                }
                FloorMood mood = ToMood(row);
                foreach (FloorConfig config in new[] { route.Layers[i].randomConfig, route.Layers[i].bossConfig })
                {
                    if (config == null || config.mood.Equals(mood)) continue;
                    config.mood = mood;
                    EditorUtility.SetDirty(config);
                    changed++;
                }
            }
            if (changed > 0) AssetDatabase.SaveAssets();
            Debug.Log($"階の光と色の CSV を取り込んだ: {rows.Count} 行、{changed} つの階の設定を書き換えた");
        }

        private static FloorMood ToMood(FloorMoodCsv.Row row) => new FloorMood
        {
            ambient = ToColor(row.Ambient),
            filter = ToColor(row.Filter),
            saturation = row.Saturation,
            vignette = row.Vignette,
            torchColor = ToColor(row.TorchColor),
            torchIntensity = row.TorchIntensity,
            torchRange = row.TorchRange,
            flicker = row.Flicker,
        };

        private static Color ToColor(FloorMoodCsv.Rgb rgb) => new Color(rgb.R, rgb.G, rgb.B, 1f);

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（道順・設定）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }
    }
}
