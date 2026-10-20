using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// シーンに入ったパーティーの先頭が立つ所。向きはこの Transform の前方。
    /// 地上はシーンに置き、階は FloorBuilder が入口の部屋（上の階から降りてきたときは出口の部屋）に生成のたびに置く。ボス階はショートカットの部屋にも置く。
    /// 名前（pointId）を付けておくと、移るときにその名前を頼まれたらそこに立つ（地上の城の門の前など）。名前の無い地点が既定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Spawn Point")]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        /// <summary>地上の城の門の前。ダンジョンから地上へ降りてきたときに立つ。</summary>
        public const string CastleGate = "CastleGate";

        /// <summary>ボス階のショートカットの部屋。地上のロープを登ってきたときに立つ。</summary>
        public const string Shortcut = "Shortcut";

        private static readonly List<PlayerSpawnPoint> Active = new List<PlayerSpawnPoint>();

        [SerializeField, Tooltip("この地点の名前。空なら既定の地点。移るときにこの名前を頼まれたらここに立つ。")]
        private string pointId;

        public string PointId
        {
            get => pointId;
            set => pointId = value;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>scene にある既定の（名前の無い）スポーン地点。最後に有効になったもの。無ければ名前付きのもの。無ければ null。</summary>
        public static PlayerSpawnPoint In(Scene scene) => In(scene, null);

        /// <summary>
        /// scene にある、名前が pointId のスポーン地点。pointId が空か、その名前の地点が無ければ既定の地点。
        /// どれも最後に有効になったものを選ぶ（階は作り直すたびに置き直すので）。
        /// </summary>
        public static PlayerSpawnPoint In(Scene scene, string pointId)
        {
            if (!string.IsNullOrEmpty(pointId))
            {
                var named = Find(scene, p => p.pointId == pointId);
                if (named != null) return named;
            }
            return Find(scene, p => string.IsNullOrEmpty(p.pointId)) ?? Find(scene, p => true);
        }

        private static PlayerSpawnPoint Find(Scene scene, System.Predicate<PlayerSpawnPoint> match)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var point = Active[i];
                if (point != null && point.gameObject.scene == scene && match(point)) return point;
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.1f, 0.4f);
            Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.up * 0.1f + transform.forward * 1f);
        }
    }
}
