using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// ユニーク武器に必ず付くエンチャントの表（CSV）を、各ユニーク（WeaponDefinition）の fixedEnchantments に書き込む。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// 武器は id（"Weapon_ShadowRunnerBlade" など）で、エンチャントは種類（Enchantments/Enchant_*.asset の kind）で引く。
    /// CSV に出てこないユニークは固定のエンチャント無しにする。ユニークでない武器の行は使わない（固定のエンチャントはユニークだけ）。
    /// 読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class UniqueEnchantmentCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = PlaceholderWeaponAssetGenerator.WeaponsFolder + "/UniqueEnchantments.csv";

        [MenuItem("Tools/TPS Dungeon/Items/ユニークのエンチャント表を取り込む")]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"ユニークのエンチャント表が無い: {CsvPath}");
                return;
            }

            var errors = new List<string>();
            List<UniqueEnchantmentCsv.Row> rows = UniqueEnchantmentCsv.Parse(File.ReadAllText(CsvPath), errors);

            var enchantments = new Dictionary<EnchantmentKind, EnchantmentDefinition>();
            foreach (EnchantmentDefinition e in EnchantmentRankCsvImporter.LoadAll<EnchantmentDefinition>())
            {
                if (!enchantments.ContainsKey(e.Kind)) enchantments[e.Kind] = e;
            }

            var weapons = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
            foreach (WeaponDefinition w in EnchantmentRankCsvImporter.LoadAll<WeaponDefinition>())
            {
                if (!string.IsNullOrEmpty(w.Id) && !weapons.ContainsKey(w.Id)) weapons[w.Id] = w;
            }

            var tables = new Dictionary<WeaponDefinition, List<FixedEnchantmentEntry>>();
            foreach (WeaponDefinition w in weapons.Values)
            {
                if (w.Rank == WeaponRank.Unique) tables[w] = new List<FixedEnchantmentEntry>();
            }

            foreach (UniqueEnchantmentCsv.Row row in rows)
            {
                if (!weapons.TryGetValue(row.Weapon, out WeaponDefinition weapon))
                {
                    errors.Add($"{row.Line} 行目: 武器 {row.Weapon} が無い");
                    continue;
                }

                if (!tables.TryGetValue(weapon, out List<FixedEnchantmentEntry> table))
                {
                    errors.Add($"{row.Line} 行目: {row.Weapon} はユニークでない（固定のエンチャントはユニークだけ）");
                    continue;
                }

                if (!enchantments.TryGetValue(row.Kind, out EnchantmentDefinition definition))
                {
                    errors.Add($"{row.Line} 行目: エンチャント {row.Kind} のアセットが無い");
                    continue;
                }

                table.Add(new FixedEnchantmentEntry(definition, row.Level));
            }

            int changed = 0;
            foreach (KeyValuePair<WeaponDefinition, List<FixedEnchantmentEntry>> pair in tables)
            {
                if (Write(pair.Key, pair.Value)) changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            foreach (string error in errors) Debug.LogError($"ユニークのエンチャント表（{CsvPath}）の {error}");
            Debug.Log($"ユニークのエンチャント表を取り込んだ: {rows.Count} 行、書き換えた武器 {changed}");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（武器・エンチャント）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>表が今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(WeaponDefinition weapon, List<FixedEnchantmentEntry> entries)
        {
            IReadOnlyList<FixedEnchantmentEntry> current = weapon.FixedEnchantments;
            bool same = current.Count == entries.Count;
            for (int i = 0; same && i < entries.Count; i++)
            {
                same = current[i].Definition == entries[i].Definition && current[i].Level == entries[i].Level;
            }

            if (same) return false;

            var serialized = new SerializedObject(weapon);
            SerializedProperty list = serialized.FindProperty("fixedEnchantments");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("definition").objectReferenceValue = entries[i].Definition;
                element.FindPropertyRelative("level").intValue = entries[i].Level;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
