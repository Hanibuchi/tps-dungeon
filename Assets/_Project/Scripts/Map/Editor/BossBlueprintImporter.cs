using System;
using System.Collections.Generic;
using System.IO;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// ボス階の設計図（Settings/Tables/BossFloors.md）を、道順（DungeonRoute）の各層のボス階の設定に取り込む。
    /// - 層ごとの設計図の本文を FloorConfig.bossBlueprint に入れる（実行中はこれを読んで組む）。
    /// - 設計図に出てくる大扉の手前の部屋（Room_BossHall_{幅}x{奥行}）と道のりの部屋（Room_BossPath_{幅}x{奥行}）の Prefab が無ければ、
    ///   その層の部屋のフォルダに作り、層のボス階のカタログに足す。使わなくなった分はカタログから外す（Prefab は消さない）。
    /// - 取り込んだあとに実際に組んでみて、組めなければ層と理由を知らせる（その層の設定は前のまま）。
    /// 設計図を保存すると自動で取り込む。メニューからも呼べる。
    /// </summary>
    public sealed class BossBlueprintImporter : AssetPostprocessor
    {
        public const string BlueprintPath = "Assets/_Project/Settings/Tables/BossFloors.md";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/ボス階の設計図", false, 106)]
        public static void Import()
        {
            if (!File.Exists(BlueprintPath))
            {
                Debug.LogError($"{BlueprintPath} が無いので、ボス階の設計図を取り込めない。");
                return;
            }
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            if (route == null)
            {
                Debug.LogError($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、ボス階の設計図を取り込めない。");
                return;
            }

            var errors = new List<string>();
            var summary = new List<string>();
            var touched = new List<UnityEngine.Object>();
            foreach (var blueprint in BossFloorBlueprint.ParseFile(File.ReadAllText(BlueprintPath), errors))
            {
                int index = blueprint.Layer - 1;
                if (index < 0 || index >= route.Layers.Count || route.Layers[index].bossConfig == null)
                {
                    errors.Add($"第 {blueprint.Layer} 層: 道順にこの層のボス階が無い");
                    continue;
                }

                var config = route.Layers[index].bossConfig;
                try
                {
                    Apply(config, blueprint);
                    touched.Add(config);
                    touched.Add(config.roomCatalog);
                    var layout = config.GenerateBossLayout(1);
                    int enemyRooms = 0;
                    foreach (var room in layout.Rooms) if (RoomEnemyRoller.CanHostEnemies(room)) enemyRooms++;
                    var bounds = BossFloorLayout.Bounds(blueprint);
                    summary.Add($"第 {blueprint.Layer} 層: 部屋 {layout.Rooms.Count}・扉 {layout.Edges.Count}・敵の出る部屋 {enemyRooms}（{bounds.Width}x{bounds.Height}）");
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    errors.Add($"第 {blueprint.Layer} 層: {e.Message}");
                }
            }

            // 触った設定とカタログだけを保存する。SaveAssets だとほかの未保存のアセット（実行中に書き換わったフォントなど）まで保存され、
            // UI の読み直しでエディタがエラーを出す。
            foreach (var asset in touched) AssetDatabase.SaveAssetIfDirty(asset);
            foreach (string error in errors) Debug.LogError($"ボス階の設計図（{BlueprintPath}）: {error}");
            Debug.Log($"ボス階の設計図を取り込んだ\n{string.Join("\n", summary)}");
        }

        /// <summary>設計図の部屋の Prefab を揃えてカタログを入れ替え、設計図の本文を設定に入れる。組めなければ元に戻して例外。</summary>
        private static void Apply(FloorConfig config, BossFloorBlueprint blueprint)
        {
            var catalog = config.roomCatalog;
            if (catalog == null) throw new ArgumentException($"{config.name} にカタログが無い（「ボス階の一式を作る」を回すこと）");
            var stair = catalog.PrefabFor(BossFloorLayout.StairId);
            if (stair == null) throw new ArgumentException($"{catalog.name} に {BossFloorLayout.StairId} が無い");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(stair)).Replace('\\', '/');
            var material = PlaceholderMapAssetGenerator.RoomFloorMaterial(catalog);

            // 決まった部屋（階段・ボス部屋・ショートカットの部屋）と、ほかの層の設計図用ではない部屋はそのまま残し、
            // 大扉の手前の部屋・道のりの部屋は、この設計図で使う大きさだけにする。
            var entries = new List<RoomTemplateCatalog.Entry>();
            foreach (var entry in catalog.Entries)
            {
                if (entry?.prefab == null) continue;
                string id = entry.prefab.name;
                if (id.StartsWith(BossFloorLayout.HallPrefix, StringComparison.Ordinal) || id.StartsWith(BossFloorLayout.PathPrefix, StringComparison.Ordinal)) continue;
                if (id == "Room_BossHall") continue; // 前の決まった 3x1 の部屋
                entries.Add(entry);
            }
            var added = new HashSet<string>();
            foreach (char room in blueprint.RoomOrder)
            {
                if (room != BossFloorBlueprint.Hall && !char.IsLower(room)) continue;
                var rect = blueprint.Rooms[room];
                string id = BossFloorLayout.TemplateId(room, rect);
                if (!added.Add(id)) continue;
                string path = $"{folder}/{id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                             ?? PlaceholderMapAssetGenerator.BuildBossRoomPrefab(id, rect.Width, rect.Height, BossFloorLayout.TagOf(room), material, folder, config.cellSize);
                entries.Add(new RoomTemplateCatalog.Entry { prefab = prefab });
            }

            var oldEntries = new List<RoomTemplateCatalog.Entry>(catalog.Entries);
            string oldBlueprint = config.bossBlueprint;
            catalog.SetEntries(entries);
            config.bossBlueprint = blueprint.Body;
            try
            {
                config.GenerateBossLayout(1);
            }
            catch
            {
                catalog.SetEntries(oldEntries);
                config.bossBlueprint = oldBlueprint;
                throw;
            }
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(config);
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, BlueprintPath) < 0 && Array.IndexOf(movedAssets, BlueprintPath) < 0) return;

            // 取り込み中はほかのアセット（道順・設定）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }
    }
}
