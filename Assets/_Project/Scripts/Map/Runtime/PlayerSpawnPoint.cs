using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// シーンに入ったパーティーの先頭が立つ所。向きはこの Transform の前方。
    /// 地上はシーンに置き、階は FloorBuilder が入口の部屋に生成のたびに置く。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Spawn Point")]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        private static readonly List<PlayerSpawnPoint> Active = new List<PlayerSpawnPoint>();

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>scene にある、最後に有効になったスポーン地点。無ければ null。</summary>
        public static PlayerSpawnPoint In(Scene scene)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] != null && Active[i].gameObject.scene == scene) return Active[i];
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
