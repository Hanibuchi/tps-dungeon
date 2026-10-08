using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 武器種ごとのエンチャントのランク表（CSV）を、各武器種（WeaponTypeDefinition）の enchantmentRanks に書き込む。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// 武器種は id（"01" など）で、エンチャントは種類（Enchantments/Enchant_*.asset の kind）で引く。CSV に出てこない武器種の表は空にする。
    /// 読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class EnchantmentRankCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = PlaceholderWeaponAssetGenerator.WeaponsFolder + "/EnchantmentRanks.csv";

        [MenuItem("Tools/TPS Dungeon/Items/エンチャントのランク表を取り込む")]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"エンチャントのランク表が無い: {CsvPath}");
                return;
            }

            var errors = new List<string>();
            List<EnchantmentRankCsv.Row> rows = EnchantmentRankCsv.Parse(File.ReadAllText(CsvPath), errors);

            var enchantments = new Dictionary<EnchantmentKind, EnchantmentDefinition>();
            foreach (EnchantmentDefinition e in LoadAll<EnchantmentDefinition>())
            {
                if (!enchantments.ContainsKey(e.Kind)) enchantments[e.Kind] = e;
            }

            var types = new Dictionary<string, WeaponTypeDefinition>(StringComparer.Ordinal);
            foreach (WeaponTypeDefinition t in LoadAll<WeaponTypeDefinition>())
            {
                if (!string.IsNullOrEmpty(t.Id) && !types.ContainsKey(t.Id)) types[t.Id] = t;
            }

            var tables = new Dictionary<WeaponTypeDefinition, List<EnchantmentRankEntry>>();
            foreach (WeaponTypeDefinition t in types.Values) tables[t] = new List<EnchantmentRankEntry>();

            foreach (EnchantmentRankCsv.Row row in rows)
            {
                if (!types.TryGetValue(row.WeaponType, out WeaponTypeDefinition type))
                {
                    errors.Add($"{row.Line} 行目: 武器種 {row.WeaponType} が無い");
                    continue;
                }

                if (!enchantments.TryGetValue(row.Kind, out EnchantmentDefinition definition))
                {
                    errors.Add($"{row.Line} 行目: エンチャント {row.Kind} のアセットが無い");
                    continue;
                }

                tables[type].Add(new EnchantmentRankEntry(definition, row.Level, row.Rank));
            }

            int changed = 0;
            foreach (KeyValuePair<WeaponTypeDefinition, List<EnchantmentRankEntry>> pair in tables)
            {
                if (Write(pair.Key, pair.Value)) changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            foreach (string error in errors) Debug.LogError($"エンチャントのランク表（{CsvPath}）の {error}");
            Debug.Log($"エンチャントのランク表を取り込んだ: {rows.Count} 行、書き換えた武器種 {changed}");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（武器種・エンチャント）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>表が今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(WeaponTypeDefinition type, List<EnchantmentRankEntry> entries)
        {
            IReadOnlyList<EnchantmentRankEntry> current = type.EnchantmentRanks;
            bool same = current.Count == entries.Count;
            for (int i = 0; same && i < entries.Count; i++)
            {
                same = current[i].Definition == entries[i].Definition && current[i].Level == entries[i].Level
                    && current[i].Rank == entries[i].Rank;
            }

            if (same) return false;

            var serialized = new SerializedObject(type);
            SerializedProperty list = serialized.FindProperty("enchantmentRanks");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("definition").objectReferenceValue = entries[i].Definition;
                element.FindPropertyRelative("level").intValue = entries[i].Level;
                element.FindPropertyRelative("rank").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(WeaponRank)), entries[i].Rank);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        internal static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return asset;
            }
        }
    }
}
