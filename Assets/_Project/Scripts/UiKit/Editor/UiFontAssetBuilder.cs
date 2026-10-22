using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace TpsDungeon.UiKit.Editor
{
    /// <summary>
    /// UI Toolkit 用のフォントアセット（TextCore の FontAsset）を TTF から作り直す。
    /// どれも Dynamic なので、必要な文字は実行時にアトラスへ足される。保存時にアトラスは空に戻す。
    /// 書体は IBM Plex Sans JP（和文・欧文とも入っているのでフォールバックは要らない）。本文は Regular、太字は Bold、飾りの見出し・数字は Medium と Bold。
    /// Plex は上の余白（ascent）が下より広く、枠の中で文字が上に寄るので、行の高さは変えずに ascent / descent を字形の中心で上下対称にする（<see cref="CenterLine"/>）。
    ///
    /// フォントは外部素材なので Assets/ThirdParty/Fonts に置き、リポジトリには入れていない（ThirdParty は管理外）。
    /// 新しい環境では、google/fonts の ofl/ibmplexsansjp から次の TTF と OFL.txt を取ってきて Assets/ThirdParty/Fonts/IBM_Plex_Sans_JP に置き、
    /// このメニューを実行する。SIL Open Font License。
    /// - IBMPlexSansJP-Regular.ttf / IBMPlexSansJP-Medium.ttf / IBMPlexSansJP-Bold.ttf
    /// </summary>
    public static class UiFontAssetBuilder
    {
        private const string Folder = "Assets/ThirdParty/Fonts/IBM_Plex_Sans_JP/";

        public const string Regular = Folder + "IBMPlexSansJP-Regular SDF.asset";
        public const string Medium = Folder + "IBMPlexSansJP-Medium SDF.asset";
        public const string Bold = Folder + "IBMPlexSansJP-Bold SDF.asset";

        private const int SamplingPointSize = 64;
        private const int Padding = 8;
        private const int AtlasSize = 1024;

        [MenuItem("Tools/TPS Dungeon/素材・プレハブ/UI のフォントアセットを作る", false, 530)]
        public static void Build()
        {
            Create(Folder + "IBMPlexSansJP-Regular.ttf", Regular, null);
            Create(Folder + "IBMPlexSansJP-Medium.ttf", Medium, null);
            Create(Folder + "IBMPlexSansJP-Bold.ttf", Bold, null);

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
            CenterLine(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// 行の上下（ascent / descent）を、大文字の高さの半分（数字・大文字・漢字のおおよその真ん中）を中心に上下対称にする。
        /// 行の高さ（ascent − descent）は変えないので、枠の大きさはそのままで文字が真ん中に来る。
        /// </summary>
        private static void CenterLine(FontAsset asset)
        {
            FaceInfo face = asset.faceInfo;
            float half = (face.ascentLine - face.descentLine) * 0.5f;
            float center = face.capLine * 0.5f;
            face.ascentLine = center + half;
            face.descentLine = center - half;
            asset.faceInfo = face;
        }
    }
}
