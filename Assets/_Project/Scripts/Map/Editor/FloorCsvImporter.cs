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
    /// - 木箱の数（crates_per_cell）と宝箱の確率（chest_chance）、ショップ・ショートカットの部屋を置く確率（shop_chance・shortcut_chance）は、
    ///   その層のランダムな階の FloorConfig に入れる。部屋数は、ショップ・ショートカットを確率の分だけ数えて見積もる。
    /// - 壁の高さ（wall_height）は、その層のランダムな階とボス階の両方の FloorConfig.wallHeight に入れ、変わった層のテーマの壁・窓・ドア・階段を作り直す。
    /// Floors.csv か Rooms.csv を保存すると自動で取り込む（部屋の大きさや重みを変えても平均面積が変わるので）。メニューからも呼べる。
    /// </summary>
    public sealed class FloorCsvImporter : AssetPostprocessor
    {
        public const string CsvPath = "Assets/_Project/Settings/Tables/Floors.csv";

        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/階の広さ", false, 102)]
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

                // 部屋数の見積もりに確率を使うので、先に入れる。
                bool chancesChanged = ApplyChances(config, row);
                List<RoomTemplateData> templates = config.roomCatalog.BuildTemplateData();
                // 入口・出口は必ず、ショップとショートカットは確率の分だけ出る（ショートカットは解放した層だけだが、解放してある前提で見積もる）。
                double specialCount = 2d + config.shopChance + config.shortcutChance;
                float specialArea = 2f * MeanArea(templates, RoomTag.Stair) + config.shopChance * MeanArea(templates, RoomTag.Shop)
                                    + config.shortcutChance * MeanArea(templates, RoomTag.Shortcut);

                // FloorGenerationParams.Validate と同じ下限（入口・出口・ショップ・ショートカットを別々の部屋に置く分）。
                int minimum = Generation.FloorGenerationParams.MinimumRoomCount(config.shopChance, config.shortcutChance);
                (int min, int max) = FloorLayerCsv.RoomCountRange(row.Area, meanRoomArea, specialCount, specialArea, minimum);
                if (min == minimum && specialCount + (row.Area - specialArea) / meanRoomArea < minimum * 1.5d)
                    warnings.Add($"{row.Line} 行目: 層 {layer} の面積 {row.Area} は狭すぎるので、部屋数を下限の {min}〜{max} にした");

                float gridArea = config.widthInCells * config.heightInCells;
                if (row.Area > gridArea / 2f)
                    warnings.Add($"{row.Line} 行目: 層 {layer} の面積 {row.Area} が {config.name} の枠 {config.widthInCells}x{config.heightInCells} の半分を超えている（伸びきれずに作り直しが増える）");

                summary.Add($"層 {layer}: 面積 {row.Area}、部屋 1 つあたり {meanRoomArea:0.##} → 部屋数 {min}〜{max}" + (row.HasElevation ? $"、高さ {row.Elevation} m" : "")
                            + (row.HasWallHeight ? $"、壁 {row.WallHeight} m" : "")
                            + $"、ショップ {config.shopChance:0.##}・ショートカット {config.shortcutChance:0.##}");
                bool propsChanged = ApplyProps(route.Layers[i], row) | chancesChanged;
                if (ApplyElevation(route.Layers[i], row)) elevationChanged.Add(layer);
                if (ApplyWallHeight(route.Layers[i], row, wallChanged)) wallChangedLayers.Add(layer);

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

        /// <summary>row にショップ・ショートカットの部屋を置く確率があれば、ランダムな階の設定に入れる（ボス階には置かない）。変わったら真。</summary>
        private static bool ApplyChances(FloorConfig config, FloorLayerCsv.Row row)
        {
            bool changed = false;
            if (row.HasShopChance && !Mathf.Approximately(config.shopChance, row.ShopChance))
            {
                config.shopChance = row.ShopChance;
                changed = true;
            }
            if (row.HasShortcutChance && !Mathf.Approximately(config.shortcutChance, row.ShortcutChance))
            {
                config.shortcutChance = row.ShortcutChance;
                changed = true;
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
