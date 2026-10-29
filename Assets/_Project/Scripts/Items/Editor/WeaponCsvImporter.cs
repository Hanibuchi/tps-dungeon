using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 武器ごとの値の表（Settings/Tables/Weapons.csv）を、各武器（WeaponDefinition）のランク・強さ・名前・説明・売値に書き込む。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。武器は id で引く。CSV に無い武器はそのまま。
    /// ランクを変えても見た目（ランクごとの型や色、ユニークの光）とアセットの名前は、生成器を回し直すまで前のまま。
    /// ユニークかどうかが変わったら、固定のエンチャント（ユニークだけに付く）も取り込み直す。
    /// 武器種（type の列）は書き換えず、アセットの武器種と違えばエラーに出すだけ（武器種を変えるのは生成器）。
    /// 読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class WeaponCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/Weapons.csv";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/武器の表", false, 112)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"武器の表が無い: {CsvPath}");
                return;
            }

            var errors = new List<string>();
            List<WeaponCsv.Row> rows = WeaponCsv.Parse(File.ReadAllText(CsvPath), errors);

            var weapons = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
            foreach (WeaponDefinition w in EnchantmentRankCsvImporter.LoadAll<WeaponDefinition>())
            {
                if (!string.IsNullOrEmpty(w.Id) && !weapons.ContainsKey(w.Id)) weapons[w.Id] = w;
            }

            int changed = 0;
            bool uniqueChanged = false;
            foreach (WeaponCsv.Row row in rows)
            {
                if (!weapons.TryGetValue(row.Id, out WeaponDefinition weapon))
                {
                    errors.Add($"{row.Line} 行目: 武器 {row.Id} が無い");
                    continue;
                }

                string typeId = weapon.WeaponType != null ? weapon.WeaponType.Id : "";
                if (row.Type.Length > 0 && !string.Equals(row.Type, typeId, StringComparison.Ordinal))
                    errors.Add($"{row.Line} 行目: {row.Id} の武器種が「{row.Type}」になっているが、アセットは「{typeId}」");

                bool wasUnique = weapon.Rank == WeaponRank.Unique;
                if (!Write(weapon, row)) continue;

                changed++;
                if (wasUnique != (row.Rank == WeaponRank.Unique)) uniqueChanged = true;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            foreach (string error in errors) Debug.LogError($"武器の表（{CsvPath}）の {error}");
            Debug.Log($"武器の表を取り込んだ: {rows.Count} 行、書き換えた武器 {changed}");

            if (uniqueChanged) UniqueEnchantmentCsvImporter.Import();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（武器）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(WeaponDefinition weapon, WeaponCsv.Row row)
        {
            var serialized = new SerializedObject(weapon);
            SerializedProperty rank = serialized.FindProperty("rank");
            SerializedProperty strength = serialized.FindProperty("strength");
            SerializedProperty displayName = serialized.FindProperty("displayName");
            SerializedProperty description = serialized.FindProperty("description");
            SerializedProperty price = serialized.FindProperty("basePrice");

            int rankIndex = Array.IndexOf(Enum.GetValues(typeof(WeaponRank)), row.Rank);
            if (rank.enumValueIndex == rankIndex && Mathf.Approximately(strength.floatValue, row.Strength)
                && displayName.stringValue == row.Name && description.stringValue == row.Description && price.intValue == row.Price) return false;

            rank.enumValueIndex = rankIndex;
            strength.floatValue = row.Strength;
            displayName.stringValue = row.Name;
            description.stringValue = row.Description;
            price.intValue = row.Price;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
