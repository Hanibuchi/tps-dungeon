using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// Quaternius の Ultimate Monsters（Assets/ThirdParty/3D Model/quaternius/ 以下の FBX）の取り込み設定。
    ///
    /// ThirdParty は git に載らず .meta も共有されないので、Inspector で直した設定は他の環境へ持ち越せない。
    /// そこで取り込むたびにここで同じ設定をかける:
    ///   - Rig は Generic、Avatar はこのモデルから作る（ルートに Animator が付く）
    ///   - クリップ名から "CharacterArmature|" を外す（Idle / Walk / Punch …）
    ///   - 繰り返す動き（<see cref="LoopClips"/>）だけ Loop Time を付ける
    /// Inspector の Animation タブでクリップを手で設定した FBX には触らない（clipAnimations が空のときだけ埋める）。
    /// </summary>
    public class QuaterniusModelPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/3D Model/quaternius/";

        private const string TakePrefix = "CharacterArmature|";

        /// <summary>ループさせるクリップ。それ以外（攻撃・被弾・死亡など）は 1 回で止まる。</summary>
        private static readonly string[] LoopClips =
        {
            "Idle", "Walk", "Run", "Jump_Idle", "Flying_Idle", "Fast_Flying", "Dance",
        };

        private bool IsTarget =>
            assetPath.StartsWith(Root) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsTarget) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsTarget) return;
            var importer = (ModelImporter)assetImporter;
            if (importer.clipAnimations.Length > 0) return;

            importer.clipAnimations = importer.defaultClipAnimations.Select(clip =>
            {
                if (clip.name.StartsWith(TakePrefix)) clip.name = clip.name.Substring(TakePrefix.Length);
                clip.loopTime = LoopClips.Contains(clip.name);
                return clip;
            }).ToArray();
        }

        [MenuItem("Tools/TPS Dungeon/Enemies/Quaternius のモデルを取り込み直す")]
        public static void ReimportAll()
        {
            // .blend や .obj も Model として引っかかる（.blend は Blender を起こすので重い）。ここで扱う FBX だけに絞る。
            string[] paths = AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"Quaternius のモデルを {paths.Length} 個取り込み直した");
        }
    }
}
