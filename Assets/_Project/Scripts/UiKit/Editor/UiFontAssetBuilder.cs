using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace TpsDungeon.UiKit.Editor
{
    /// <summary>
    /// UI Toolkit 用のフォントアセット（TextCore の FontAsset）を TTF から作り直す。
    /// どれも Dynamic なので、必要な文字は実行時にアトラスへ足される。保存時にアトラスは空に戻す。
    /// 欧文の飾り書体（Cinzel Decorative）には、和文が来たときのフォールバックとして明朝体を付ける。
    /// </summary>
    public static class UiFontAssetBuilder
    {
        private const string Folder = "Assets/_Project/UI/Fonts/";

        public const string MinchoRegular = Folder + "ShipporiMinchoB1-Regular SDF.asset";
        public const string MinchoBold = Folder + "ShipporiMinchoB1-Bold SDF.asset";
        public const string DecoRegular = Folder + "CinzelDecorative-Regular SDF.asset";
        public const string DecoBold = Folder + "CinzelDecorative-Bold SDF.asset";

        private const int SamplingPointSize = 64;
        private const int Padding = 8;
        private const int AtlasSize = 1024;

        [MenuItem("Tools/TPS Dungeon/UI/フォントアセットを作る")]
        public static void Build()
        {
            FontAsset minchoRegular = Create(Folder + "ShipporiMinchoB1-Regular.ttf", MinchoRegular, null);
            FontAsset minchoBold = Create(Folder + "ShipporiMinchoB1-Bold.ttf", MinchoBold, null);
            Create(Folder + "CinzelDecorative-Regular.ttf", DecoRegular, minchoRegular);
            Create(Folder + "CinzelDecorative-Bold.ttf", DecoBold, minchoBold);

            AssetDatabase.SaveAssets();
            Debug.Log("UI のフォントアセットを作った: " + Folder);
        }

        private static FontAsset Create(string fontPath, string assetPath, FontAsset fallback)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError("フォントが見つからない: " + fontPath);
                return null;
            }

            // 作り直すときは古いものを消す。サブアセット（アトラスとマテリアル）が残って増えていかないように。
            AssetDatabase.DeleteAsset(assetPath);

            FontAsset asset = FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic);
            if (asset == null)
            {
                Debug.LogError("フォントアセットを作れなかった: " + fontPath);
                return null;
            }

            asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (fallback != null) asset.fallbackFontAssetTable = new List<FontAsset> { fallback };

            AssetDatabase.CreateAsset(asset, assetPath);

            // アトラスとマテリアルは FontAsset の中に持たせる。別に置くと参照が切れる。
            foreach (Texture2D texture in asset.atlasTextures)
            {
                if (texture == null) continue;
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }

            if (asset.material != null)
            {
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            // 生成中に拾った字形は捨て、空のアトラスで保存する。リポジトリに無駄な画像を積まないため。
            asset.ClearFontAssetData(true);
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
