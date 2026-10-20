using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上の歩ける範囲。村の四角（<see cref="BaseArea"/>）に、地上のショートカット（[Surface]/Shortcuts の Shortcut_L{n}）のまわりを足した四角にする。
    /// ショートカットを森の中や城の裏に置いても、歩いてたどり着けるように広げる。
    /// - 見えない壁（[Bounds]）をその四角のまわりに立て直す。
    /// - 四角の中の森の木と岩に当たりを付ける（森は歩ける範囲の外の飾りとして当たり無しで置いてあるので、すり抜けないように）。
    /// - NavMesh はその四角の中の当たりから焼く（森の地面・木も入る。仲間がショートカットまで付いてこられるように）。
    /// - 地上の地図の絵をその四角で撮り直す（<see cref="SurfaceMapCapture"/>）。
    /// NavMesh を焼く前に毎回呼ぶ（<see cref="SurfaceScenery.BakeNavMesh"/>）。景色・塔・ショートカットのどれを置き直しても揃う。
    /// </summary>
    public static class SurfaceWalkArea
    {
        /// <summary>村の四角（x の最小・最大、z の最小・最大）。地上シーンの地面の範囲と同じ。</summary>
        public static readonly Rect BaseArea = Rect.MinMaxRect(-50f, -31f, 50f, 69f);

        /// <summary>ショートカットのまわりに空けておく広さ（m）。</summary>
        private const float ShortcutMargin = 7f;

        private const float BoundsHeight = 30f;
        private const float BoundsThickness = 2f;
        // NavMesh を焼く高さの範囲。塔の上の方の床を拾わないよう低く抑える。
        private const float NavMeshBottom = -3f;
        private const float NavMeshTop = 8f;

        /// <summary>歩ける範囲を決め、見えない壁・森の当たり・NavMesh の焼く範囲を揃える。surfaceRoot は [Surface]。</summary>
        public static Rect Apply(GameObject surfaceRoot)
        {
            Rect area = Area(surfaceRoot.transform);
            RebuildBounds(surfaceRoot.scene, area);
            int solid = SolidifyForest(surfaceRoot.scene, area);
            if (surfaceRoot.TryGetComponent(out NavMeshSurface surface))
            {
                surface.collectObjects = CollectObjects.Volume;
                surface.center = new Vector3(area.center.x, (NavMeshBottom + NavMeshTop) * 0.5f, area.center.y);
                surface.size = new Vector3(area.width, NavMeshTop - NavMeshBottom, area.height);
                EditorUtility.SetDirty(surface);
            }
            // 地図の絵も、この範囲で撮り直す。
            SurfaceMapCapture.Capture(surfaceRoot, area);
            Debug.Log($"地上の歩ける範囲: x {area.xMin:0}〜{area.xMax:0}、z {area.yMin:0}〜{area.yMax:0}（森の木と岩 {solid} 個に当たりを付けた。地図を撮り直した）");
            return area;
        }

        /// <summary>村の四角と、ショートカットのまわりを全部含む四角。</summary>
        public static Rect Area(Transform surface)
        {
            Rect area = BaseArea;
            var shortcuts = surface.Find("Shortcuts");
            if (shortcuts == null) return area;
            foreach (Transform marker in shortcuts)
            {
                if (!marker.name.StartsWith("Shortcut_L")) continue;
                Vector3 p = marker.position;
                area.xMin = Mathf.Min(area.xMin, p.x - ShortcutMargin);
                area.xMax = Mathf.Max(area.xMax, p.x + ShortcutMargin);
                area.yMin = Mathf.Min(area.yMin, p.z - ShortcutMargin);
                area.yMax = Mathf.Max(area.yMax, p.z + ShortcutMargin);
            }
            return area;
        }

        /// <summary>
        /// 四角の 4 辺のすぐ外に見えない壁（当たりだけの箱）を立て直す。
        /// [Surface] の外に置き、レイヤーは Ignore Raycast（キャラは止めるが、照準やカメラの壁よけには掛からない）。
        /// </summary>
        private static void RebuildBounds(UnityEngine.SceneManagement.Scene scene, Rect area)
        {
            foreach (GameObject old in scene.GetRootGameObjects())
            {
                if (old.name == "[Bounds]") Object.DestroyImmediate(old);
            }
            var root = new GameObject("[Bounds]");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

            float width = area.width + BoundsThickness * 2f;
            float y = BoundsHeight * 0.5f;
            float half = BoundsThickness * 0.5f;
            Wall("Bound_S", new Vector3(area.center.x, y, area.yMin - half), new Vector3(width, BoundsHeight, BoundsThickness));
            Wall("Bound_N", new Vector3(area.center.x, y, area.yMax + half), new Vector3(width, BoundsHeight, BoundsThickness));
            Wall("Bound_W", new Vector3(area.xMin - half, y, area.center.y), new Vector3(BoundsThickness, BoundsHeight, area.height));
            Wall("Bound_E", new Vector3(area.xMax + half, y, area.center.y), new Vector3(BoundsThickness, BoundsHeight, area.height));

            void Wall(string name, Vector3 center, Vector3 size)
            {
                var wall = new GameObject(name) { layer = 2 };
                wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = center;
                wall.AddComponent<BoxCollider>().size = size;
                GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>
        /// 四角の中にある森（[Scenery]/Forest）の木に幹の当たり、岩に形どおりの当たりを付ける。藪はすり抜けてよいので付けない。
        /// もう当たりがある物は飛ばす。付けた数を返す。
        /// </summary>
        private static int SolidifyForest(UnityEngine.SceneManagement.Scene scene, Rect area)
        {
            Transform forest = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "[Scenery]") forest = root.transform.Find("Forest");
            }
            if (forest == null) return 0;

            int count = 0;
            foreach (Transform item in forest)
            {
                Vector3 p = item.position;
                if (!area.Contains(new Vector2(p.x, p.z))) continue;
                if (item.GetComponentInChildren<Collider>() != null) continue;

                if (item.name.Contains("_tree_"))
                {
                    // 幹だけ。枝葉の下はくぐれる。大きさは木の拡大に付いてくる。
                    var trunk = item.gameObject.AddComponent<CapsuleCollider>();
                    trunk.center = new Vector3(0f, 2f, 0f);
                    trunk.height = 4f;
                    trunk.radius = 0.35f;
                    count++;
                }
                else if (item.name.Contains("_rock_"))
                {
                    foreach (var filter in item.GetComponentsInChildren<MeshFilter>())
                    {
                        if (filter.sharedMesh != null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                    count++;
                }
            }
            return count;
        }
    }
}
