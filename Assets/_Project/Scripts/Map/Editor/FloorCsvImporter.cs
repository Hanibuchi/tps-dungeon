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
    /// 層ごとの階の広さと高さ（Floors.csv）を、道順（DungeonRoute）の層の設定に書き込む。
    /// - 広さ（area。平均面積）は、ランダムな階の部屋数（minRoomCount・maxRoomCount）に直す。部屋 1 つあたりの平均面積は Rooms.csv の部屋の大きさと重みから出す。
    ///   階段・ショップの部屋は必ず出るので、カタログの大きさで別に数える。上限は下限の 2 倍。ボス階には触らない。
    /// - 高さ（elevation）は、その層のランダムな階とボス階の両方の FloorConfig.elevation に入れ、変わった層は土台のプレハブを作り直す。
    ///   窓の外の景色と地上の塔は高さで置き方が変わるので、変わったら「階の外の景色を置く（全層）」と「地上のダンジョンの塔を建て直す」を回すよう知らせる。
    /// - 木箱の数（crates_per_cell）と宝箱の確率（chest_chance）は、その層のランダムな階の FloorConfig に入れる。
    /// - 壁の高さ（wall_height）は、その層のランダムな階とボス階の両方の FloorConfig.wallHeight に入れ、変わった層のテーマの壁・窓・ドア・階段を作り直す。
    /// - ボス階の道のり（boss_layout_seed・boss_path_rooms・boss_side_rooms・boss_path_enemies）は、その層のボス階の FloorConfig に入れる。
    ///   組んだ形がグリッドに収まらなければ、グリッドを広げる（まわりに 1 セルずつ余白を残す）。
    /// Floors.csv か Rooms.csv を保存すると自動で取り込む（部屋の大きさや重みを変えても平均面積が変わるので）。メニューからも呼べる。
    /// </summary>
    public sealed class FloorCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/Floors.csv";

        [MenuItem("Tools/TPS Dungeon/階の広さの CSV を取り込む")]
        public static void Import()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"{CsvPath} が無いので、階の広さの CSV を取り込めない。");
                return;
            }
            if (!File.Exists(RoomCsvImporter.CsvPath))
            {
                Debug.LogError($"{RoomCsvImporter.CsvPath} が無いので、部屋 1 つあたりの面積が分からず、階の広さの CSV を取り込めない。");
                return;
            }

            var parseErrors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(File.ReadAllText(CsvPath), parseErrors);
            foreach (string error in parseErrors) errors.Add($"{CsvPath} の {error}");

            // Rooms.csv の読めない行は RoomCsvImporter が知らせるので、ここでは黙って飛ばす。
            List<RoomLayerCsv.Row> rooms = RoomLayerCsv.Parse(File.ReadAllText(RoomCsvImporter.CsvPath), null);

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(PlaceholderMapAssetGenerator.RoutePath);
            if (route == null)
            {
                Debug.LogError($"{PlaceholderMapAssetGenerator.RoutePath} が無いので、階の広さの CSV を取り込めない。先に「ダンジョンの道順を作る」を回すこと。");
                return;
            }

            var byLayer = new Dictionary<int, FloorLayerCsv.Row>();
            foreach (FloorLayerCsv.Row row in rows)
            {
                if (row.Layer > route.Layers.Count)
                {
                    warnings.Add($"{row.Line} 行目: 層 {row.Layer} は道順に無い（{route.Layers.Count} 層まで）");
                    continue;
                }
                byLayer[row.Layer] = row;
            }

            var summary = new List<string>();
            var elevationChanged = new List<int>();
            var wallChanged = new List<FloorConfig>();
            var wallChangedLayers = new List<int>();
            int changed = 0;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                int layer = i + 1;
                FloorConfig config = route.Layers[i].randomConfig;
                if (config == null || config.roomCatalog == null)
                {
                    errors.Add($"層 {layer} のランダムな階の設定かカタログが無い");
                    continue;
                }

                if (!byLayer.TryGetValue(layer, out FloorLayerCsv.Row row))
                {
                    warnings.Add($"層 {layer} の行が無いので、{config.name} の部屋数はそのままにした");
                    continue;
                }

                double meanRoomArea = FloorLayerCsv.MeanRoomArea(rooms.FindAll(room => room.Layer == layer));
                if (meanRoomArea <= 0d)
                {
                    errors.Add($"層 {layer} は {RoomCsvImporter.CsvPath} に重みが 0 より大きい部屋が無いので、{config.name} の部屋数はそのままにした");
                    continue;
                }

                List<RoomTemplateData> templates = config.roomCatalog.BuildTemplateData();
                int specialCount = 2;
                float specialArea = 2f * MeanArea(templates, RoomTag.Stair);
                if (config.includeShop)
                {
                    specialCount++;
                    specialArea += MeanArea(templates, RoomTag.Shop);
                }

                // FloorGenerationParams.Validate と同じ下限（入口・出口・ショップを別々の部屋に置く分）。
                int minimum = config.includeShop ? 4 : 3;
                (int min, int max) = FloorLayerCsv.RoomCountRange(row.Area, meanRoomArea, specialCount, specialArea, minimum);
                if (min == minimum && specialCount + (row.Area - specialArea) / meanRoomArea < minimum * 1.5d)
                    warnings.Add($"{row.Line} 行目: 層 {layer} の面積 {row.Area} は狭すぎるので、部屋数を下限の {min}〜{max} にした");

                float gridArea = config.widthInCells * config.heightInCells;
                if (row.Area > gridArea / 2f)
                    warnings.Add($"{row.Line} 行目: 層 {layer} の面積 {row.Area} が {config.name} の枠 {config.widthInCells}x{config.heightInCells} の半分を超えている（伸びきれずに作り直しが増える）");

                summary.Add($"層 {layer}: 面積 {row.Area}、部屋 1 つあたり {meanRoomArea:0.##} → 部屋数 {min}〜{max}" + (row.HasElevation ? $"、高さ {row.Elevation} m" : "")
                            + (row.HasWallHeight ? $"、壁 {row.WallHeight} m" : ""));
                if (ApplyElevation(route.Layers[i], row)) elevationChanged.Add(layer);
                if (ApplyWallHeight(route.Layers[i], row, wallChanged)) wallChangedLayers.Add(layer);
                bool propsChanged = ApplyProps(route.Layers[i], row);
                propsChanged |= ApplyBossPath(route.Layers[i], row, summary, errors);

                if (config.minRoomCount == min && config.maxRoomCount == max)
                {
                    if (propsChanged) changed++;
                    continue;
                }
                config.minRoomCount = min;
                config.maxRoomCount = max;
                EditorUtility.SetDirty(config);
                changed++;
            }

            // 壁・窓・ドア・階段は壁の高さの実寸で作るので、高さが変わった層のテーマの部品を作り直す。
            if (wallChanged.Count > 0) PlaceholderMapAssetGenerator.RebuildParts(wallChanged);
            AssetDatabase.SaveAssets();
            if (wallChangedLayers.Count > 0) Debug.Log($"階の広さの CSV: 層 {string.Join("・", wallChangedLayers)} の壁の高さを変えて、壁・窓・ドア・階段の部品を作り直した");
            if (elevationChanged.Count > 0)
                warnings.Add($"層 {string.Join("・", elevationChanged)} の高さを変えて土台を作り直した。窓の外の景色と地上の塔に入れるには「階の外の景色を置く（全層）」と「地上のダンジョンの塔を建て直す」を回すこと");
            foreach (string warning in warnings) Debug.LogWarning($"階の広さの CSV: {warning}");
            foreach (string error in errors) Debug.LogError($"階の広さの CSV の {error}");
            Debug.Log($"階の広さの CSV を取り込んだ: {rows.Count} 行、層 {route.Layers.Count} つのうち {changed} つの設定を書き換えた\n{string.Join("\n", summary)}");
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, CsvPath) < 0 && Array.IndexOf(importedAssets, RoomCsvImporter.CsvPath) < 0) return;

            // 取り込み中はほかのアセット（道順・設定）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }

        /// <summary>row に高さがあれば、層のランダムな階とボス階の高さに入れて土台を作り直す。どれかが変わったら真。</summary>
        private static bool ApplyElevation(DungeonRoute.Layer layer, FloorLayerCsv.Row row)
        {
            if (!row.HasElevation) return false;
            bool changed = false;
            foreach (FloorConfig config in new[] { layer.randomConfig, layer.bossConfig })
            {
                if (config == null || Mathf.Approximately(config.elevation, row.Elevation)) continue;
                config.elevation = row.Elevation;
                PlaceholderMapAssetGenerator.RebuildFoundation(config);
                changed = true;
            }
            return changed;
        }

        /// <summary>row に壁の高さがあれば、層のランダムな階とボス階の両方に入れる。変わった設定は changed に足し、どれかが変わったら真。</summary>
        private static bool ApplyWallHeight(DungeonRoute.Layer layer, FloorLayerCsv.Row row, List<FloorConfig> changed)
        {
            if (!row.HasWallHeight) return false;
            bool any = false;
            foreach (FloorConfig config in new[] { layer.randomConfig, layer.bossConfig })
            {
                if (config == null || Mathf.Approximately(config.wallHeight, row.WallHeight)) continue;
                config.wallHeight = row.WallHeight;
                EditorUtility.SetDirty(config);
                changed.Add(config);
                any = true;
            }
            return any;
        }

        /// <summary>row に木箱・宝箱の量があれば、層のランダムな階に入れる（ボス階には置かない）。変わったら真。</summary>
        private static bool ApplyProps(DungeonRoute.Layer layer, FloorLayerCsv.Row row)
        {
            FloorConfig config = layer.randomConfig;
            if (config == null) return false;
            bool changed = false;
            if (row.HasCratesPerCell && !Mathf.Approximately(config.cratesPerCell, row.CratesPerCell))
            {
                config.cratesPerCell = row.CratesPerCell;
                changed = true;
            }
            if (row.HasChestChance && !Mathf.Approximately(config.chestChance, row.ChestChance))
            {
                config.chestChance = row.ChestChance;
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(config);
            return changed;
        }

        /// <summary>
        /// row にボス階の道のりの値があれば、層のボス階の設定に入れる。組んだ形がグリッドに収まらなければグリッドを広げる。変わったら真。
        /// </summary>
        private static bool ApplyBossPath(DungeonRoute.Layer layer, FloorLayerCsv.Row row, List<string> summary, List<string> errors)
        {
            FloorConfig config = layer.bossConfig;
            if (config == null) return false;
            bool changed = false;
            if (row.HasBossLayoutSeed && config.bossLayoutSeed != row.BossLayoutSeed)
            {
                config.bossLayoutSeed = row.BossLayoutSeed;
                changed = true;
            }
            if (row.HasBossPathRooms && config.bossPathRooms != row.BossPathRooms)
            {
                config.bossPathRooms = row.BossPathRooms;
                changed = true;
            }
            if (row.HasBossSideRooms && config.bossSideRooms != row.BossSideRooms)
            {
                config.bossSideRooms = row.BossSideRooms;
                changed = true;
            }
            if (row.HasBossPathEnemies && config.bossPathEnemies != (row.BossPathEnemies == 1))
            {
                config.bossPathEnemies = row.BossPathEnemies == 1;
                changed = true;
            }

            try
            {
                var (width, height) = Generation.BossFloorLayout.RequiredSize(config.BuildBossParams());
                int size = Mathf.Max(width, height) + 2;
                if (config.widthInCells < size || config.heightInCells < size)
                {
                    config.widthInCells = Mathf.Max(config.widthInCells, size);
                    config.heightInCells = Mathf.Max(config.heightInCells, size);
                    changed = true;
                }
                summary.Add($"  ボス階: 道のり {config.bossPathRooms} 部屋・脇道 {config.bossSideRooms} 部屋（シード {config.bossLayoutSeed}）→ {width}x{height}" +
                            (config.bossPathEnemies ? "、道のりに敵" : ""));
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                errors.Add($"{row.Line} 行目: 層 {row.Layer} のボス階を組めない: {e.Message}");
            }

            if (changed) EditorUtility.SetDirty(config);
            return changed;
        }

        /// <summary>そのタグの部屋の面積の、抽選の重みでの平均。無ければ 1x1 とみなす。</summary>
        private static float MeanArea(List<RoomTemplateData> templates, RoomTag tag)
        {
            float weighted = 0f, total = 0f;
            foreach (RoomTemplateData template in templates)
            {
                if (template.Weight <= 0f || (template.Tags & tag) == 0) continue;
                weighted += template.Weight * template.Width * template.Height;
                total += template.Weight;
            }
            return total > 0f ? weighted / total : 1f;
        }
    }
}
