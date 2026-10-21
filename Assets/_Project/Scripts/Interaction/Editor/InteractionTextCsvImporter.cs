using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Interaction.Editor
{
    /// <summary>
    /// インタラクトできる場所の文字（Settings/Tables/InteractionTexts.csv）を、Resources の表（<see cref="InteractionTextTable"/>）に書き込む。
    /// 表のアセットが無ければ作る。CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// シーンやプレハブには書かないので、取り込めば生成器を回し直さなくても文字が変わる（Play 中でも次に照準を合わせたときから）。
    /// </summary>
    public sealed class InteractionTextCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/InteractionTexts.csv";
        public const string TablePath = "Assets/_Project/Resources/Interaction/InteractionTexts.asset";

        [MenuItem("Tools/TPS Dungeon/インタラクトの文字の CSV を取り込む")]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、インタラクトの文字の CSV を取り込めない。");
                return;
            }

            var errors = new List<string>();
            List<InteractionTextCsv.Row> rows = InteractionTextCsv.Parse(File.ReadAllText(CsvPath), errors);
            foreach (string error in errors) Debug.LogError($"インタラクトの文字の CSV（{CsvPath}）の {error}");

            var table = AssetDatabase.LoadAssetAtPath<InteractionTextTable>(TablePath);
            if (table == null)
            {
                EnsureFolder(Path.GetDirectoryName(TablePath)?.Replace('\\', '/'));
                table = ScriptableObject.CreateInstance<InteractionTextTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            var entries = new List<InteractionTextTable.Entry>();
            foreach (InteractionTextCsv.Row row in rows)
            {
                entries.Add(new InteractionTextTable.Entry
                {
                    id = row.Id,
                    prompt = row.Prompt,
                    closedPrompt = row.ClosedPrompt,
                    title = row.Title,
                    description = row.Description,
                });
            }
            table.SetEntries(entries);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"インタラクトの文字の CSV を取り込んだ: {entries.Count} 行");
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
