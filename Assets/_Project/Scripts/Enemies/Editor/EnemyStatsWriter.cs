using System;
using System.Collections.Generic;
using System.Globalization;
using TpsDungeon.Progression;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵の台帳（Enemies.csv）の強さの列を、敵のプレハブの欄に書き込む。<see cref="EnemySpawnCsvImporter"/> から呼ぶ
    /// （Enemies.csv を保存すると自動で走る）。空欄の値はプレハブのまま。値が今と同じなら何もしない。
    /// 列ごとの書き込み先（敵種は付いている EnemyBrain で決まる）:
    ///   hp → EnemyHealth.maxHp / damage → EnemyBrain.damage / move_speed → EnemyMotor.moveSpeed /
    ///   attack_interval → EnemyBrain.cooldown / detect_range → EnemyBrain.detectRadius / exp → EnemyExpReward.exp
    ///   windup       … 近距離・遠距離・毒霧は windup、飛びつきは crouchTime、自爆は fuseTime
    ///   attack_range … 近距離 attackRange、飛びつき leapRange、自爆 fuseRange、毒霧 emitRange、遠距離 maxRange
    ///   area_radius  … 自爆 blastRadius、毒霧 cloudRadius
    /// その敵種に無い列に値があれば警告を出して飛ばす。欄の下限（Min）より小さい値は下限にして警告を出す。
    /// </summary>
    public static class EnemyStatsWriter
    {
        private static readonly Dictionary<Type, string> WindupFields = new Dictionary<Type, string>
        {
            { typeof(MeleeEnemyBrain), "windup" },
            { typeof(RangedEnemyBrain), "windup" },
            { typeof(PoisonGasEnemyBrain), "windup" },
            { typeof(LeapEnemyBrain), "crouchTime" },
            { typeof(BomberEnemyBrain), "fuseTime" },
        };

        private static readonly Dictionary<Type, string> AttackRangeFields = new Dictionary<Type, string>
        {
            { typeof(MeleeEnemyBrain), "attackRange" },
            { typeof(LeapEnemyBrain), "leapRange" },
            { typeof(BomberEnemyBrain), "fuseRange" },
            { typeof(PoisonGasEnemyBrain), "emitRange" },
            { typeof(RangedEnemyBrain), "maxRange" },
        };

        private static readonly Dictionary<Type, string> AreaRadiusFields = new Dictionary<Type, string>
        {
            { typeof(BomberEnemyBrain), "blastRadius" },
            { typeof(PoisonGasEnemyBrain), "cloudRadius" },
        };

        /// <summary>書き換えたプレハブの数を返す。</summary>
        public static int Write(List<EnemyCsv.Row> rows, Dictionary<string, GameObject> prefabs, string csvPath,
            List<string> errors, List<string> warnings)
        {
            int changedPrefabs = 0;
            foreach (EnemyCsv.Row row in rows)
            {
                string where = $"{csvPath} の {row.Line} 行目: {row.Id}";
                if (!prefabs.TryGetValue(row.Id, out GameObject prefab))
                {
                    // 部屋に出る敵のプレハブが無いことは EnemySpawnCsvImporter が知らせる。
                    if (row.Layers.Length == 0 && HasAny(row.Stats)) errors.Add($"{where} のプレハブが無いので強さを書き込めない");
                    continue;
                }

                var changes = new List<string>();
                var writer = new PrefabWriter(prefab, where, changes, errors, warnings);
                writer.Apply(row.Stats);
                if (changes.Count == 0) continue;

                PrefabUtility.SavePrefabAsset(prefab);
                changedPrefabs++;
                Debug.Log($"敵の強さ: {prefab.name}（{row.Id}）の {string.Join("、", changes)}");
            }

            return changedPrefabs;
        }

        private static bool HasAny(EnemyCsv.Stats s) =>
            s.Hp.HasValue || s.Damage.HasValue || s.MoveSpeed.HasValue || s.AttackInterval.HasValue || s.Windup.HasValue ||
            s.AttackRange.HasValue || s.AreaRadius.HasValue || s.DetectRange.HasValue || s.Exp.HasValue;

        private sealed class PrefabWriter
        {
            private readonly GameObject prefab;
            private readonly string where;
            private readonly List<string> changes;
            private readonly List<string> errors;
            private readonly List<string> warnings;

            public PrefabWriter(GameObject prefab, string where, List<string> changes, List<string> errors, List<string> warnings)
            {
                this.prefab = prefab;
                this.where = where;
                this.changes = changes;
                this.errors = errors;
                this.warnings = warnings;
            }

            public void Apply(EnemyCsv.Stats stats)
            {
                if (stats.Hp.HasValue) SetInt<EnemyHealth>(EnemyCsv.HpColumn, "maxHp", stats.Hp.Value);
                if (stats.MoveSpeed.HasValue) SetFloat<EnemyMotor>(EnemyCsv.MoveSpeedColumn, "moveSpeed", stats.MoveSpeed.Value);
                if (stats.Exp.HasValue) SetInt<EnemyExpReward>(EnemyCsv.ExpColumn, "exp", stats.Exp.Value);

                bool needsBrain = stats.Damage.HasValue || stats.AttackInterval.HasValue || stats.DetectRange.HasValue ||
                                  stats.Windup.HasValue || stats.AttackRange.HasValue || stats.AreaRadius.HasValue;
                if (!needsBrain) return;

                var brain = prefab.GetComponent<EnemyBrain>();
                if (brain == null)
                {
                    errors.Add($"{where} のプレハブ {prefab.name} に EnemyBrain が無いので、攻撃の値を書き込めない");
                    return;
                }

                Type type = brain.GetType();
                var so = new SerializedObject(brain);
                if (stats.Damage.HasValue) SetInt(so, EnemyCsv.DamageColumn, "damage", stats.Damage.Value);
                if (stats.DetectRange.HasValue) SetFloat(so, EnemyCsv.DetectRangeColumn, "detectRadius", stats.DetectRange.Value);
                if (stats.AttackInterval.HasValue)
                {
                    if (type == typeof(BomberEnemyBrain)) NotUsed(EnemyCsv.AttackIntervalColumn, type);
                    else SetFloat(so, EnemyCsv.AttackIntervalColumn, "cooldown", stats.AttackInterval.Value);
                }

                SetByType(so, type, EnemyCsv.WindupColumn, WindupFields, stats.Windup);
                SetByType(so, type, EnemyCsv.AttackRangeColumn, AttackRangeFields, stats.AttackRange);
                SetByType(so, type, EnemyCsv.AreaRadiusColumn, AreaRadiusFields, stats.AreaRadius);

                if (type == typeof(RangedEnemyBrain) && stats.AttackRange.HasValue)
                {
                    float minRange = so.FindProperty("minRange").floatValue;
                    if (stats.AttackRange.Value <= minRange)
                    {
                        warnings.Add($"{where} の attack_range {Format(stats.AttackRange.Value)} が、下がり始める距離（minRange {Format(minRange)}）以下。撃つ前に下がり続ける");
                    }
                }

                if (stats.DetectRange.HasValue)
                {
                    float loseRadius = so.FindProperty("loseRadius").floatValue;
                    if (stats.DetectRange.Value > loseRadius)
                    {
                        warnings.Add($"{where} の detect_range {Format(stats.DetectRange.Value)} が、諦める距離（loseRadius {Format(loseRadius)}）より長い。気付いてすぐ諦める");
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private void SetByType(SerializedObject so, Type type, string column, Dictionary<Type, string> fields, float? value)
            {
                if (!value.HasValue) return;
                if (fields.TryGetValue(type, out string field)) SetFloat(so, column, field, value.Value);
                else NotUsed(column, type);
            }

            private void NotUsed(string column, Type type) =>
                warnings.Add($"{where} の {column} は {type.Name} の敵には無いので使わない（空欄にする）");

            private void SetInt<T>(string column, string field, int value) where T : Component
            {
                var component = prefab.GetComponent<T>();
                if (component == null)
                {
                    errors.Add($"{where} のプレハブ {prefab.name} に {typeof(T).Name} が無いので、{column} を書き込めない");
                    return;
                }

                var so = new SerializedObject(component);
                SetInt(so, column, field, value);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private void SetFloat<T>(string column, string field, float value) where T : Component
            {
                var component = prefab.GetComponent<T>();
                if (component == null)
                {
                    errors.Add($"{where} のプレハブ {prefab.name} に {typeof(T).Name} が無いので、{column} を書き込めない");
                    return;
                }

                var so = new SerializedObject(component);
                SetFloat(so, column, field, value);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            private void SetInt(SerializedObject so, string column, string field, int value)
            {
                SerializedProperty property = Find(so, column, field);
                if (property == null) return;

                int min = Mathf.CeilToInt(MinOf(so, field));
                if (value < min)
                {
                    warnings.Add($"{where} の {column} {value} は下限 {min} より小さいので {min} にする");
                    value = min;
                }

                if (property.intValue == value) return;
                changes.Add($"{column} {property.intValue} → {value}");
                property.intValue = value;
            }

            private void SetFloat(SerializedObject so, string column, string field, float value)
            {
                SerializedProperty property = Find(so, column, field);
                if (property == null) return;

                float min = MinOf(so, field);
                if (value < min)
                {
                    warnings.Add($"{where} の {column} {Format(value)} は下限 {Format(min)} より小さいので {Format(min)} にする");
                    value = min;
                }

                if (Mathf.Approximately(property.floatValue, value)) return;
                changes.Add($"{column} {Format(property.floatValue)} → {Format(value)}");
                property.floatValue = value;
            }

            private SerializedProperty Find(SerializedObject so, string column, string field)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null) errors.Add($"{where} の {column}: {so.targetObject.GetType().Name} に欄 {field} が見つからない");
                return property;
            }
        }

        /// <summary>欄に付いている [Min] の値。付いていなければ 0。</summary>
        private static float MinOf(SerializedObject so, string field)
        {
            for (Type type = so.targetObject.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                var info = type.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                if (info == null) continue;
                var min = (MinAttribute)Attribute.GetCustomAttribute(info, typeof(MinAttribute));
                return min != null ? min.min : 0f;
            }

            return 0f;
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
