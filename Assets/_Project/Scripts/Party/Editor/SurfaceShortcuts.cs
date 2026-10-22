using System.Collections.Generic;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Editor;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上のショートカット（層ごとに 1 本、空から垂れたロープ）を村のあちこちに置く。見た目は <see cref="ShortcutArt.BuildSurfaceRope"/>。
    /// ロープを登るとその層のボス階のショートカットの部屋へ出て、ボス階の井戸から降りると、その層のロープの所（PlayerSpawnPoint "Shortcut_L{n}"）に立つ。
    /// [Surface]/Shortcuts にまとめ、置くたびに作り直す。最初の置き場所は <see cref="Spots"/>。
    /// シーンで Shortcut_L{n} を動かしたり回したりしたら、置き直すときにその位置と向きを引き継ぐ（地面の高さ・塔の下のロープの長さ・着く所・NavMesh はそこに合わせ直す）。
    /// </summary>
    public static class SurfaceShortcuts
    {
        private const string GroupName = "Shortcuts";
        private const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";

        /// <summary>層ごとの置き場所（x, z）。村の建物・木・柵の無い所から、あちこちに散らして選んである。正面（石板の側）は広場の真ん中へ向ける。</summary>
        private static readonly (float x, float z)[] Spots =
        {
            (-37f, -9f),   // 第 1 層: 西の銀行の裏
            (12f, -14f),   // 第 2 層: 広場の南東、民家の手前
            (-22f, 19f),   // 第 3 層: 北西の民家と城のあいだ
            (35f, -2f),    // 第 4 層: 東の民家の裏
            (24f, 26f),    // 第 5 層: 城の脇
        };

        /// <summary>着いたパーティーが立つ所（ロープからの向きの前方へ）。</summary>
        private const float SpawnDistance = 2.8f;

        [MenuItem("Tools/TPS Dungeon/シーン/地上のショートカットを置く", false, 216)]
        public static void RebuildFromMenu() => Debug.Log(Rebuild());

        /// <summary>開いている地上のシーンにショートカットを置き直し、NavMesh を焼き直して保存する。</summary>
        public static string Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SurfaceSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "シーンを開き直せなかった。";
                scene = EditorSceneManager.OpenScene(SurfaceSceneBuilder.ScenePath, OpenSceneMode.Single);
            }

            GameObject surface = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "[Surface]") surface = root;
            }
            if (surface == null) return $"{scene.path} に [Surface] が無い。先に「地上シーンを作る」を実行すること。";

            string result = Build(surface.transform);
            SurfaceScenery.BakeNavMesh(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return result;
        }

        /// <summary>world（[Surface]）の下にショートカットを置く（保存も NavMesh もしない）。生成器からも呼ぶ。</summary>
        internal static string Build(Transform world)
        {
            // シーンで動かした位置と向きは引き継ぐ（無い層は Spots の既定の所）。
            var kept = new Dictionary<string, (float x, float z, float yaw)>();
            var old = world.Find(GroupName);
            if (old != null)
            {
                foreach (Transform marker in old)
                {
                    if (!marker.name.StartsWith("Shortcut_L")) continue;
                    var p = marker.localPosition;
                    kept[marker.name] = (p.x, p.z, marker.localEulerAngles.y);
                }
                Object.DestroyImmediate(old.gameObject);
            }

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route == null) return $"{RoutePath} が無いので、地上のショートカットを置かなかった。";

            var group = new GameObject(GroupName).transform;
            group.SetParent(world, false);
            Physics.SyncTransforms();

            int placed = 0;
            for (int layer = 0; layer < route.Layers.Count && layer < Spots.Length; layer++)
            {
                if (!route.Layers[layer].HasBoss) continue;
                string name = $"Shortcut_L{layer + 1}";
                var (x, z) = Spots[layer];
                // 正面（-Z）が広場の真ん中（原点）を向く角度。
                float yaw = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
                if (kept.TryGetValue(name, out var moved)) (x, z, yaw) = moved;
                float y = GroundHeight(x, z);

                // 真上に塔が張り出していれば、ロープはその下面から垂らす。
                float top = Physics.Raycast(new Vector3(x, y + 3f, z), Vector3.up, out var above, ShortcutArt.SkyRopeTop, ~0, QueryTriggerInteraction.Ignore)
                    ? above.point.y - y
                    : ShortcutArt.SkyRopeTop;
                var marker = ShortcutArt.BuildSurfaceRope(group, name, layer, route, top);
                marker.transform.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));

                // ロープから正面（-Z）の方へ数歩の所に、ボス階から降りてきたパーティーが立つ。ロープに背を向ける。
                // 目印の子にして、シーンで目印を動かせば一緒に動くようにする。
                var spawn = new GameObject("ArrivalSpawn");
                spawn.transform.SetParent(marker.transform, false);
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
                Vector3 at = new Vector3(x, 0f, z) + forward * SpawnDistance;
                at.y = GroundHeight(at.x, at.z) + 0.05f;
                spawn.transform.SetPositionAndRotation(group.TransformPoint(at), group.rotation * Quaternion.LookRotation(forward));
                spawn.AddComponent<PlayerSpawnPoint>().PointId = ShortcutPoint.SurfaceArrivalPoint(layer);
                placed++;
            }
            return $"地上のショートカットを {placed} 本置いた。";
        }

        /// <summary>(x, z) の地面の高さ。上から当たりを探す（トリガーと、置いたばかりのショートカットは見ない）。無ければ 0。</summary>
        private static float GroundHeight(float x, float z)
        {
            var hits = Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<ShortcutPoint>() != null) continue;
                if (hit.collider.transform.parent != null && hit.collider.transform.parent.name.StartsWith("Shortcut_L")) continue;
                // 建物や木の上に載らないよう、地面の高さの範囲（-1〜1.5 m）だけを見る。
                if (hit.point.y < -1f || hit.point.y > 1.5f) continue;
                best = Mathf.Max(best, hit.point.y);
            }
            return float.IsNegativeInfinity(best) ? 0f : best;
        }
    }
}
