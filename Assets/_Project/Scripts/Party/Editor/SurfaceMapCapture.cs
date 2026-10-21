using System.Collections.Generic;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上の地図の絵を撮る。歩ける範囲（<see cref="SurfaceWalkArea"/>）を真上から正射影で撮り、PNG にして [Surface] の SurfaceMapSource に差す。
    /// 城（上へ行くほど張り出して村を覆う）・ショートカット（解放したものだけ印で出す）・パーティー・もやは、撮る間だけ隠す。城は地図に土台の四角で描く。
    /// NavMesh を焼くたびに（歩ける範囲を揃えたあとに）撮り直すので、景色・塔・ショートカットのどれを置き直しても地図が揃う。
    /// </summary>
    public static class SurfaceMapCapture
    {
        public const string ImagePath = "Assets/_Project/UI/Hud/SurfaceMap.png";

        /// <summary>絵の細かさ（1 m あたりの画素）。長い辺は MaxSize に収める。</summary>
        private const float PixelsPerMeter = 8f;
        private const int MaxSize = 2048;
        private const float CameraHeight = 60f;

        [MenuItem("Tools/TPS Dungeon/地上の地図を撮り直す")]
        public static void CaptureFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SurfaceSceneBuilder.ScenePath)
            {
                Debug.LogError($"{SurfaceSceneBuilder.ScenePath} を開いてから回すこと。");
                return;
            }
            GameObject surface = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "[Surface]") surface = root;
            }
            if (surface == null)
            {
                Debug.LogError($"{scene.path} に [Surface] が無い。");
                return;
            }
            Capture(surface, SurfaceWalkArea.Area(surface.transform));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>area（x・z、メートル）を真上から撮って地図の元に差す（シーンの保存はしない）。</summary>
        public static void Capture(GameObject surfaceRoot, Rect area) =>
            Capture(surfaceRoot, area, ImagePath, SurfaceTower.BaseFoot(), null, new Color(0.08f, 0.07f, 0.06f),
                new[] { "Castle", "Shortcuts" }, new[] { "[Party Rig]", MistBuilder.RootName });

        /// <summary>
        /// area（x・z、メートル）を真上から撮って imagePath に書き、mapRoot の地図の元（SurfaceMapSource）に差す（シーンの保存はしない）。
        /// hiddenChildren（mapRoot の直下の名前）と hiddenRoots（シーンの一番上の名前）は撮る間だけ隠す。castle は地図に四角で描く城の土台（大きさ 0 なら描かない）。
        /// </summary>
        public static void Capture(GameObject mapRoot, Rect area, string imagePath, Rect castle, string placeName, Color background,
            IEnumerable<string> hiddenChildren, IEnumerable<string> hiddenRoots)
        {
            Scene scene = mapRoot.scene;
            int width = Mathf.Clamp(Mathf.RoundToInt(area.width * PixelsPerMeter), 16, MaxSize);
            int height = Mathf.Clamp(Mathf.RoundToInt(area.height * PixelsPerMeter), 16, MaxSize);

            var hidden = new List<GameObject>();
            foreach (string child in hiddenChildren) Hide(mapRoot.transform.Find(child)?.gameObject);
            var rootNames = new HashSet<string>(hiddenRoots);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (rootNames.Contains(root.name)) Hide(root);
            }

            var holder = new GameObject("[Map Camera]");
            SceneManager.MoveGameObjectToScene(holder, scene);
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = area.height * 0.5f;
            camera.aspect = area.width / area.height;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = CameraHeight + 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            holder.transform.SetPositionAndRotation(new Vector3(area.center.x, CameraHeight, area.center.y), Quaternion.Euler(90f, 0f, 0f));

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(imagePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(holder);
                foreach (var go in hidden) go.SetActive(true);
            }

            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(imagePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = MaxSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            var source = mapRoot.GetComponent<SurfaceMapSource>() ?? mapRoot.AddComponent<SurfaceMapSource>();
            source.Configure(image, area, castle, placeName);
            EditorUtility.SetDirty(source);

            void Hide(GameObject go)
            {
                if (go == null || !go.activeSelf) return;
                go.SetActive(false);
                hidden.Add(go);
            }
        }
    }
}
