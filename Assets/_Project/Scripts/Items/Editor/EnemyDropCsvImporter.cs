using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 敵が落とす武器の表（CSV 2 つ）を、ドロップ表（Resources/Weapons/EnemyDropTable.asset）に書き込む。無ければ作る。
    ///   EnemyWeaponDrops.csv … 敵ごとのランク E〜S の確率
    ///   EnemyUniqueDrops.csv … 敵 × ユニーク武器ごとの確率と天井
    /// どちらかの CSV を保存すると自動で取り込む。メニューからも呼べる。
    /// 敵は id（EnemyIdentity の id。pink_blob など）で、武器は id（"Weapon_FallenStarHammer" など）で引く。
    /// ランクで落とす武器の候補も、このとき全部の武器（素手とユニークを除く）から作り直す。
    /// 読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class EnemyDropCsvImporter : AssetPostprocessor
    {
        public const string RankCsvPath = "Assets/_Project/Settings/Tables/EnemyWeaponDrops.csv";
        public const string UniqueCsvPath = "Assets/_Project/Settings/Tables/EnemyUniqueDrops.csv";
        public const string TablePath = "Assets/_Project/Resources/" + EnemyDropTable.ResourcePath + ".asset";

        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemies";

        /// <summary>素手の武器種。落とさない。</summary>
        private const string UnarmedTypeId = "Unarmed";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/敵のドロップ表", false, 109)]
        public static void Import()
        {
            var errors = new List<string>();
            List<EnemyWeaponDropCsv.Row> rankRows = Read(RankCsvPath, EnemyWeaponDropCsv.Parse, errors);
            List<EnemyUniqueDropCsv.Row> uniqueRows = Read(UniqueCsvPath, EnemyUniqueDropCsv.Parse, errors);

            var weapons = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
            foreach (WeaponDefinition w in EnchantmentRankCsvImporter.LoadAll<WeaponDefinition>())
            {
                if (!string.IsNullOrEmpty(w.Id) && !weapons.ContainsKey(w.Id)) weapons[w.Id] = w;
            }

            HashSet<string> knownEnemies = KnownEnemies();
            if (knownEnemies.Count == 0)
            {
                Debug.LogWarning("id（EnemyIdentity）の付いた敵のプレハブが無いので、敵の id を確かめられない。" +
                                 "メニュー「Tools/TPS Dungeon/敵/敵に id を付ける」を先に実行すること。");
            }

            var entries = new List<EnemyDropTable.EnemyEntry>();
            var byEnemy = new Dictionary<string, EnemyDropTable.EnemyEntry>(StringComparer.Ordinal);
            EnemyDropTable.EnemyEntry EntryFor(string enemy)
            {
                if (byEnemy.TryGetValue(enemy, out EnemyDropTable.EnemyEntry entry)) return entry;
                entry = new EnemyDropTable.EnemyEntry { enemyId = enemy };
                byEnemy[enemy] = entry;
                entries.Add(entry);
                return entry;
            }

            bool IsKnown(string enemy, string csv, int line)
            {
                if (knownEnemies.Count == 0 || knownEnemies.Contains(enemy)) return true;
                errors.Add($"{csv} の {line} 行目: 敵 {enemy} のプレハブが無い（{EnemyPrefabFolder} の敵の EnemyIdentity の id で書く）");
                return false;
            }

            foreach (EnemyWeaponDropCsv.Row row in rankRows)
            {
                if (!IsKnown(row.Enemy, RankCsvPath, row.Line)) continue;
                EntryFor(row.Enemy).rankChances = (float[])row.Chances.Clone();
            }

            foreach (EnemyUniqueDropCsv.Row row in uniqueRows)
            {
                if (!IsKnown(row.Enemy, UniqueCsvPath, row.Line)) continue;

                if (!weapons.TryGetValue(row.Weapon, out WeaponDefinition weapon))
                {
                    errors.Add($"{UniqueCsvPath} の {row.Line} 行目: 武器 {row.Weapon} が無い");
                    continue;
                }

                if (weapon.Rank != WeaponRank.Unique)
                {
                    errors.Add($"{UniqueCsvPath} の {row.Line} 行目: {row.Weapon} はユニークでない（ランクの武器は {RankCsvPath} で決める）");
                    continue;
                }

                EntryFor(row.Enemy).uniques.Add(new EnemyDropTable.UniqueEntry { weapon = weapon, chance = row.Chance, pity = row.Pity });
            }

            var droppable = new List<WeaponDefinition>();
            foreach (WeaponDefinition w in weapons.Values)
            {
                if (w.Rank > WeaponRank.S || w.WorldPrefab == null) continue;
                if (w.WeaponType != null && w.WeaponType.Id == UnarmedTypeId) continue;
                droppable.Add(w);
            }
            droppable.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            foreach (WeaponRank rank in EnchantmentRankCsv.RankColumns)
            {
                if (droppable.Exists(w => w.Rank == rank)) continue;
                foreach (EnemyDropTable.EnemyEntry entry in entries)
                {
                    if (entry.rankChances[(int)rank] > 0f) errors.Add($"{entry.enemyId} は {rank} を落とすが、{rank} の武器が 1 本も無い");
                }
            }

            bool changed = Write(entries, droppable);
            foreach (string error in errors) Debug.LogError($"敵のドロップ表の {error}");
            Debug.Log($"敵のドロップ表を取り込んだ: 敵 {entries.Count} 体、ユニーク {uniqueRows.Count} 行、" +
                      $"ランクで落とす武器 {droppable.Count} 本{(changed ? "" : "（変更なし）")}");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, RankCsvPath) < 0 && Array.IndexOf(importedAssets, UniqueCsvPath) < 0) return;

            // 取り込み中はほかのアセット（武器・敵のプレハブ）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        private static List<T> Read<T>(string path, Func<string, List<string>, List<T>> parse, List<string> errors)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"敵のドロップ表が無い: {path}");
                return new List<T>();
            }

            var parseErrors = new List<string>();
            List<T> rows = parse(File.ReadAllText(path), parseErrors);
            foreach (string error in parseErrors) errors.Add($"{path} の {error}");
            return rows;
        }

        private static HashSet<string> KnownEnemies()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var identity = prefab != null ? prefab.GetComponent<EnemyIdentity>() : null;
                if (identity != null && !string.IsNullOrEmpty(identity.Id)) ids.Add(identity.Id);
            }

            return ids;
        }

        /// <summary>表が今と違えば書き換える（無ければ作る）。書き換えたら真。</summary>
        private static bool Write(List<EnemyDropTable.EnemyEntry> entries, List<WeaponDefinition> droppable)
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemyDropTable>(TablePath);
            if (table == null)
            {
                string folder = Path.GetDirectoryName(TablePath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                table = ScriptableObject.CreateInstance<EnemyDropTable>();
                table.Assign(entries, droppable);
                AssetDatabase.CreateAsset(table, TablePath);
                AssetDatabase.SaveAssets();
                return true;
            }

            string before = EditorJsonUtility.ToJson(table);
            table.Assign(entries, droppable);
            if (EditorJsonUtility.ToJson(table) == before) return false;

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
