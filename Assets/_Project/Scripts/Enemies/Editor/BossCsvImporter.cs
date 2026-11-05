using System;
using System.Collections.Generic;
using System.IO;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Editor;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// ボスの表（Settings/Tables/Bosses.csv、<see cref="BossCsv"/>）を取り込む。
    ///   ・ボスのプレハブ（Prefabs/Enemies の、id の合う EnemyIdentity）の <see cref="BossPhases"/> に名前・段階の境目・テンポを書く
    ///   ・道順（DungeonRoute）のその層のボス階の FloorConfig の boss にプレハブを、bossBgm に bgm 列の曲を差す
    ///     （ThirdParty は git に載らず GUID が環境ごとに違うので、曲は CSV にパスで書いておき、取り込みで差し直す）
    /// 強さ（hp など）は Enemies.csv に書く（EnemySpawnCsvImporter）。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。値が今と同じなら書き換えない。
    /// </summary>
    public sealed class BossCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = EnemySpawnCsvImporter.TablesFolder + "/Bosses.csv";

        /// <summary>bgm 列のパスの前に付ける所。</summary>
        public const string SoundRoot = "Assets/ThirdParty/Sound/";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/ボス", false, 108)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無い");
                return;
            }

            var errors = new List<string>();
            List<BossCsv.Row> rows = BossCsv.Parse(File.ReadAllText(CsvPath), errors);

            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyIdentitySetup.PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var identity = prefab != null ? prefab.GetComponent<EnemyIdentity>() : null;
                if (identity != null && !string.IsNullOrEmpty(identity.Id) && !prefabs.ContainsKey(identity.Id)) prefabs[identity.Id] = prefab;
            }

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            int changed = 0;
            foreach (BossCsv.Row row in rows)
            {
                string where = $"{CsvPath} の {row.Line} 行目: {row.Id}";
                if (!prefabs.TryGetValue(row.Id, out GameObject prefab))
                {
                    errors.Add($"{where} のプレハブが無い（{EnemyIdentitySetup.PrefabFolder} の EnemyIdentity の id で書く）");
                    continue;
                }

                if (WritePhases(prefab, row, where, errors)) changed++;

                if (route == null) continue;
                if (row.Layer > route.Layers.Count)
                {
                    errors.Add($"{where} の第 {row.Layer} 層が道順に無い");
                    continue;
                }

                FloorConfig config = route.Layers[row.Layer - 1].bossConfig;
                if (config == null)
                {
                    errors.Add($"{where} の第 {row.Layer} 層にボス階の設定（bossConfig）が無い");
                    continue;
                }

                AudioClip bgm = null;
                if (row.Bgm.Length > 0)
                {
                    bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundRoot + row.Bgm);
                    if (bgm == null) errors.Add($"{where} の bgm「{row.Bgm}」が {SoundRoot} の下に無い");
                }

                if (config.boss == prefab && config.bossBgm == bgm && Mathf.Approximately(config.bossBgmVolume, row.BgmVolume)) continue;
                config.boss = prefab;
                config.bossBgm = bgm;
                config.bossBgmVolume = row.BgmVolume;
                EditorUtility.SetDirty(config);
                changed++;
            }

            if (route == null) errors.Add($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、ボス階にボスを差さなかった");
            if (changed > 0) AssetDatabase.SaveAssets();

            foreach (string error in errors) Debug.LogError($"ボスの表の {error}");
            Debug.Log($"ボスの表を取り込んだ: {rows.Count} 体{(changed > 0 ? $"、{changed} か所書き換えた" : "（変更なし）")}");
        }

        /// <summary>BossPhases の欄を書く。書き換えたら真。</summary>
        private static bool WritePhases(GameObject prefab, BossCsv.Row row, string where, List<string> errors)
        {
            var phases = prefab.GetComponent<BossPhases>();
            if (phases == null)
            {
                errors.Add($"{where} のプレハブ {prefab.name} に BossPhases が無い");
                return false;
            }

            var so = new SerializedObject(phases);
            bool dirty = false;
            void SetString(string field, string value)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p.stringValue == value) return;
                p.stringValue = value;
                dirty = true;
            }

            void SetFloat(string field, float value)
            {
                SerializedProperty p = so.FindProperty(field);
                if (Mathf.Approximately(p.floatValue, value)) return;
                p.floatValue = value;
                dirty = true;
            }

            SetString("displayName", row.Name);
            SetFloat("phase2Hp", row.Phase2Hp);
            SetFloat("phase3Hp", row.Phase3Hp);
            SetFloat("tempo2", row.Tempo2);
            SetFloat("tempo3", row.Tempo3);
            if (!dirty) return false;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);
            return true;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（敵のプレハブ・道順）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }
    }
}
