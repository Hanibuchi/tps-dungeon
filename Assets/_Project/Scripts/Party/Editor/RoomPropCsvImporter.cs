using System;
using UnityEditor;

namespace TpsDungeon.Party.Editor
{
    /// <summary>層ごとの置き物の重み（Settings/Tables/RoomProps.csv）を保存したら、層ごとの置き物の一覧を作り直す（BreakablePrefabBuilder.BuildPropSets）。</summary>
    public sealed class RoomPropCsvImporter : AssetPostprocessor
    {
        [MenuItem("Tools/TPS Dungeon/CSV の取り込み/層ごとの置き物", false, 105)]
        public static void Import() => BreakablePrefabBuilder.BuildPropSets();

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, BreakablePrefabBuilder.RoomPropCsvPath) < 0) return;

            // 取り込み中はほかのアセット（プレハブ・道順）がまだ読めないことがあるので、落ち着いてから。
            EditorApplication.delayCall -= Import;
            EditorApplication.delayCall += Import;
        }
    }
}
