using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 武器種ごとの値の表（Settings/Tables/WeaponTypes.csv）を、各武器種（WeaponTypeDefinition）に書き込む。
    /// 名前は displayName、盾の可否は canUseShield、クールタイムは近接なら comboCooldown・遠距離なら fireInterval に書く。
    /// 続く時間は撃ち方で欄を選ぶ（<see cref="DurationProperty"/>）。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。武器種は id で引く。CSV に無い武器種はそのまま。
    /// 読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class WeaponTypeCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/WeaponTypes.csv";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/武器種の表", false, 113)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogWarning($"武器種の表が無い: {CsvPath}");
                return;
            }

            var errors = new List<string>();
            List<WeaponTypeCsv.Row> rows = WeaponTypeCsv.Parse(File.ReadAllText(CsvPath), errors);

            var types = new Dictionary<string, WeaponTypeDefinition>(StringComparer.Ordinal);
            foreach (WeaponTypeDefinition t in EnchantmentRankCsvImporter.LoadAll<WeaponTypeDefinition>())
            {
                if (!string.IsNullOrEmpty(t.Id) && !types.ContainsKey(t.Id)) types[t.Id] = t;
            }

            int changed = 0;
            foreach (WeaponTypeCsv.Row row in rows)
            {
                if (!types.TryGetValue(row.Id, out WeaponTypeDefinition type))
                {
                    errors.Add($"{row.Line} 行目: 武器種 {row.Id} が無い");
                    continue;
                }

                string cooldownProperty = CooldownProperty(type);
                if (row.Cooldown.HasValue && cooldownProperty == null)
                {
                    errors.Add($"{row.Line} 行目: {row.Id} は振りも撃ちもしないので、クールタイムは使わない（空にする）");
                }

                string durationProperty = DurationProperty(type);
                if (row.Duration.HasValue && durationProperty == null)
                {
                    errors.Add($"{row.Line} 行目: {row.Id} は続く効果が無いので、続く時間は使わない（空にする）");
                }

                if (Write(type, row, cooldownProperty, durationProperty)) changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            foreach (string error in errors) Debug.LogError($"武器種の表（{CsvPath}）の {error}");
            Debug.Log($"武器種の表を取り込んだ: {rows.Count} 行、書き換えた武器種 {changed}");
        }

        /// <summary>クールタイムを書く欄。近接は comboCooldown、遠距離は fireInterval。どちらでもなければ null。</summary>
        public static string CooldownProperty(WeaponTypeDefinition type)
        {
            if (type.IsMelee) return "comboCooldown";
            if (type.IsRanged) return "fireInterval";
            return null;
        }

        /// <summary>
        /// 続く時間を書く欄。持続弓は rainDuration、加護は supportDuration、治癒持続は healDuration、召喚は summonDuration、
        /// 火炎放射器は flameDuration（吐ける時間）。どれでもなければ null。
        /// </summary>
        public static string DurationProperty(WeaponTypeDefinition type)
        {
            switch (type.RangedKind)
            {
                case RangedAttackKind.Rain: return "rainDuration";
                case RangedAttackKind.Buff: return "supportDuration";
                case RangedAttackKind.HealField: return "healDuration";
                case RangedAttackKind.Summon: return "summonDuration";
                case RangedAttackKind.Flame: return "flameDuration";
                default: return null;
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（武器種）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>今と違えば書き換える。書き換えたら真。</summary>
        private static bool Write(WeaponTypeDefinition type, WeaponTypeCsv.Row row, string cooldownProperty, string durationProperty)
        {
            var serialized = new SerializedObject(type);
            SerializedProperty displayName = serialized.FindProperty("displayName");
            SerializedProperty shield = serialized.FindProperty("canUseShield");
            SerializedProperty cooldown = row.Cooldown.HasValue && cooldownProperty != null ? serialized.FindProperty(cooldownProperty) : null;
            SerializedProperty duration = row.Duration.HasValue && durationProperty != null ? serialized.FindProperty(durationProperty) : null;

            bool same = displayName.stringValue == row.Name && shield.boolValue == row.CanUseShield
                && (cooldown == null || Mathf.Approximately(cooldown.floatValue, row.Cooldown.Value))
                && (duration == null || Mathf.Approximately(duration.floatValue, row.Duration.Value));
            if (same) return false;

            displayName.stringValue = row.Name;
            shield.boolValue = row.CanUseShield;
            if (cooldown != null) cooldown.floatValue = row.Cooldown.Value;
            if (duration != null) duration.floatValue = row.Duration.Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
