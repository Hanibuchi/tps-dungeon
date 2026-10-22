using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Progression.Editor
{
    /// <summary>
    /// 魂の強化の表（Settings/Tables/SoulUpgrades.csv）を、Resources の表（<see cref="SoulUpgradeTable"/>）に書き込む。
    /// 表のアセットが無ければ作る。CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// 書き換えるのは強化の木だけで、表のアセットで調整したソウルの式やブーストの倍率はそのまま残す。
    /// </summary>
    public sealed class SoulUpgradeCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/SoulUpgrades.csv";
        public const string TablePath = "Assets/_Project/Resources/Progression/SoulUpgrades.asset";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/魂の強化", false, 101)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、魂の強化の CSV を取り込めない。");
                return;
            }

            var errors = new List<string>();
            List<SoulUpgradeNode> rows = SoulUpgradeCsv.Parse(File.ReadAllText(CsvPath), errors);
            // 木として組めない行（根に着かないなど）も知らせる。
            new SoulUpgradeTree(rows, errors);
            foreach (string error in errors) Debug.LogError($"魂の強化の CSV（{CsvPath}）: {error}");

            var table = AssetDatabase.LoadAssetAtPath<SoulUpgradeTable>(TablePath);
            if (table == null)
            {
                EnsureFolder(Path.GetDirectoryName(TablePath)?.Replace('\\', '/'));
                table = ScriptableObject.CreateInstance<SoulUpgradeTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.SetNodes(rows);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"魂の強化の CSV を取り込んだ: {rows.Count} 行");
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            EnsureFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はアセットを作れないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }
    }
}
