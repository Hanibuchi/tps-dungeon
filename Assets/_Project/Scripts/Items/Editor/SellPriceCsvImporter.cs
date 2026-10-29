using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// ランクごとの売値の表（Settings/Tables/SellPrices.csv）を、売値の表のアセット（Resources/Weapons/WeaponPriceTable）に書き込む。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。アセットが無ければ作る。CSV に無いランクは 0 になる。
    /// 読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class SellPriceCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/SellPrices.csv";
        public const string TablePath = "Assets/_Project/Resources/" + WeaponPriceTable.ResourcePath + ".asset";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/売値の表", false, 114)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"売値の表が無い: {CsvPath}");
                return;
            }

            var errors = new List<string>();
            List<SellPriceCsv.Row> rows = SellPriceCsv.Parse(File.ReadAllText(CsvPath), errors);

            var table = AssetDatabase.LoadAssetAtPath<WeaponPriceTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<WeaponPriceTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            bool changed = Write(table, rows);
            if (changed) AssetDatabase.SaveAssets();
            foreach (string error in errors) Debug.LogError($"売値の表（{CsvPath}）の {error}");
            Debug.Log($"売値の表を取り込んだ: {rows.Count} 行{(changed ? "" : "（変わらず）")}");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はアセットを作れないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>表が今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(WeaponPriceTable table, List<SellPriceCsv.Row> rows)
        {
            var serialized = new SerializedObject(table);
            SerializedProperty list = serialized.FindProperty("entries");
            bool same = list.arraySize == rows.Count;
            for (int i = 0; same && i < rows.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                same = element.FindPropertyRelative("rank").enumValueIndex == RankIndex(rows[i].Rank)
                    && element.FindPropertyRelative("weapon").intValue == rows[i].Weapon
                    && element.FindPropertyRelative("enchantment").intValue == rows[i].Enchantment;
            }

            if (same) return false;

            list.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("rank").enumValueIndex = RankIndex(rows[i].Rank);
                element.FindPropertyRelative("weapon").intValue = rows[i].Weapon;
                element.FindPropertyRelative("enchantment").intValue = rows[i].Enchantment;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            return true;
        }

        private static int RankIndex(WeaponRank rank) => Array.IndexOf(Enum.GetValues(typeof(WeaponRank)), rank);
    }
}
