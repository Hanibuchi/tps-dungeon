using System;
using System.Collections.Generic;
using System.IO;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 層ごとの普通の部屋（Rooms.csv。大きさと重み）を、道順（DungeonRoute）の層のランダムな階のカタログに書き込む。
    /// 部屋は層の部屋のフォルダ（階段の部屋と同じ所）の Room_{x}x{y}.prefab。無ければ作り（床はその層の床の素材）、あれば重みだけ揃える。
    /// カタログの普通の部屋（Normal タグ）を CSV の部屋に差し替える。階段・ショップの部屋とボス階のカタログには触らない。
    /// CSV から消した部屋はカタログから外すだけで、プレハブは消さない。
    /// CSV を保存すると自動で取り込む。メニューからも呼べる。読めない行は飛ばし、行番号付きでコンソールに出す。
    /// </summary>
    public sealed class RoomCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Map/Rooms.csv";

        [MenuItem("Tools/TPS Dungeon/部屋の CSV を取り込む")]
        public static void Import()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、部屋の CSV を取り込めない。");
                return;
            }

            var parseErrors = new List<string>();
            List<RoomLayerCsv.Row> rows = RoomLayerCsv.Parse(File.ReadAllText(CsvPath), parseErrors);
            foreach (string error in parseErrors) errors.Add($"{CsvPath} の {error}");

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            if (route == null)
            {
                Debug.LogError($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、部屋の CSV を取り込めない。先に「ダンジョンの道順を作る」を回すこと。");
                return;
            }

            var byLayer = new Dictionary<int, List<RoomLayerCsv.Row>>();
            foreach (RoomLayerCsv.Row row in rows)
            {
                if (row.Layer > route.Layers.Count)
                {
                    warnings.Add($"{row.Line} 行目: 層 {row.Layer} は道順に無い（{route.Layers.Count} 層まで）");
                    continue;
                }
                if (!byLayer.TryGetValue(row.Layer, out List<RoomLayerCsv.Row> list)) byLayer[row.Layer] = list = new List<RoomLayerCsv.Row>();
                list.Add(row);
            }

            int changedCatalogs = 0, createdRooms = 0, reweighted = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                int layer = i + 1;
                FloorConfig config = route.Layers[i].randomConfig;
                RoomTemplateCatalog catalog = config != null ? config.roomCatalog : null;
                if (catalog == null)
                {
                    errors.Add($"層 {layer} のランダムな階の設定かカタログが無い");
                    continue;
                }

                if (!byLayer.TryGetValue(layer, out List<RoomLayerCsv.Row> layerRows))
                {
                    warnings.Add($"層 {layer} の行が無いので、{catalog.name} の普通の部屋はそのままにした");
                    continue;
                }

                if (!layerRows.Exists(row => row.Weight > 0f))
                {
                    errors.Add($"層 {layer} に重みが 0 より大きい部屋が無い（階を作れなくなる）ので、{catalog.name} はそのままにした");
                    continue;
                }

                string folder = RoomsFolder(catalog);
                if (folder == null)
                {
                    errors.Add($"{catalog.name} に部屋が 1 つも無く、部屋の置き場所が分からない");
                    continue;
                }

                // CSV の部屋を CSV の順に並べ、その後ろに階段・ショップの部屋をそのまま残す（並び順が生成結果に効くので、今の並びを崩さない）。
                var entries = new List<RoomTemplateCatalog.Entry>();
                var kept = new List<RoomTemplateCatalog.Entry>();
                var removed = new List<string>();
                foreach (RoomTemplateCatalog.Entry entry in catalog.Entries)
                {
                    var template = entry?.prefab != null ? entry.prefab.GetComponent<RoomTemplate>() : null;
                    if (template != null && (template.Tags & RoomTag.Normal) == 0) kept.Add(new RoomTemplateCatalog.Entry { prefab = entry.prefab, weightOverride = entry.weightOverride });
                    else if (entry?.prefab != null) removed.Add(entry.prefab.name);
                }

                Material floorMaterial = null;
                foreach (RoomLayerCsv.Row row in layerRows)
                {
                    string name = $"Room_{row.Width}x{row.Height}";
                    string path = $"{folder}/{name}.prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        floorMaterial ??= PlaceholderMapAssetGenerator.RoomFloorMaterial(catalog);
                        prefab = PlaceholderMapAssetGenerator.BuildNormalRoomPrefab(name, row.Width, row.Height, row.Weight, floorMaterial, folder, config.cellSize);
                        createdRooms++;
                    }
                    else
                    {
                        var template = prefab.GetComponent<RoomTemplate>();
                        if (template == null || template.SizeInCells != new Vector2Int(row.Width, row.Height) || (template.Tags & RoomTag.Normal) == 0)
                        {
                            errors.Add($"{row.Line} 行目: {path} が {row.Width}x{row.Height} の普通の部屋になっていないので使わなかった");
                            continue;
                        }
                        if (SetWeight(prefab, template, row.Weight)) reweighted++;
                    }

                    entries.Add(new RoomTemplateCatalog.Entry { prefab = prefab });
                    removed.Remove(name);
                }

                entries.AddRange(kept);
                foreach (string name in removed) Debug.Log($"部屋の CSV: 層 {layer} の {name} は CSV に無いので {catalog.name} から外した（プレハブは残してある）");

                if (SameEntries(catalog.Entries, entries)) continue;
                catalog.SetEntries(entries);
                EditorUtility.SetDirty(catalog);
                changedCatalogs++;
            }

            AssetDatabase.SaveAssets();
            foreach (string warning in warnings) Debug.LogWarning($"部屋の CSV: {warning}");
            foreach (string error in errors) Debug.LogError($"部屋の CSV の {error}");
            Debug.Log($"部屋の CSV を取り込んだ: {rows.Count} 行、部屋を {createdRooms} 個作り、{reweighted} 個の重みを揃え、" +
                      $"層 {route.Layers.Count} つのうち {changedCatalogs} つのカタログを書き換えた");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0) return;

            // 取り込み中はほかのアセット（部屋のプレハブ・カタログ）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>その層の部屋の置き場所。階段の部屋のフォルダ（無ければカタログの先頭の部屋のフォルダ）。</summary>
        private static string RoomsFolder(RoomTemplateCatalog catalog)
        {
            GameObject anchor = catalog.PrefabFor(FloorConfig.BossEntranceId);
            if (anchor == null)
            {
                foreach (RoomTemplateCatalog.Entry entry in catalog.Entries)
                {
                    if (entry?.prefab == null) continue;
                    anchor = entry.prefab;
                    break;
                }
            }
            return anchor != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(anchor)).Replace('\\', '/') : null;
        }

        /// <summary>部屋の Prefab の重みを weight にする。違っていて書き換えたら真。</summary>
        private static bool SetWeight(GameObject prefab, RoomTemplate template, float weight)
        {
            if (Mathf.Approximately(template.Weight, weight)) return false;

            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var contents = root.GetComponent<RoomTemplate>();
                contents.Configure(contents.SizeInCells, contents.CellSize, contents.Tags, contents.AllowRotation, weight);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool SameEntries(IReadOnlyList<RoomTemplateCatalog.Entry> current, List<RoomTemplateCatalog.Entry> wanted)
        {
            if (current.Count != wanted.Count) return false;
            for (int i = 0; i < wanted.Count; i++)
            {
                if (current[i] == null || current[i].prefab != wanted[i].prefab || !Mathf.Approximately(current[i].weightOverride, wanted[i].weightOverride)) return false;
            }
            return true;
        }
    }
}
