using System.Collections;
using TpsDungeon.Map.Runtime;
using Unity.Cinemachine;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 地上から来ずに直接 Play したシーン（Player_Debug などの確認用）で、パーティーの先頭をそのシーンのスポーン地点へ移す。
    /// 階はレイアウトを毎回生成するので、シーンに置いた位置は部屋のテンプレートを変えると床の無い所（空中）になりうる。
    /// 生成されたスポーン地点（入口の階段の脇）へ移せば、いつも床の上から始まる。
    /// GameFlow がある（地上から来た、または地上で始めた）ときは GameFlow が並べるので何もしない。Party と同じ物に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Party))]
    [AddComponentMenu("TPS Dungeon/Direct Play Spawn")]
    public sealed class DirectPlaySpawn : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("スポーン地点ができる（階が生成される）のを待つ長さ（秒）。過ぎたらその場のまま。")]
        private float waitTimeout = 5f;

        private IEnumerator Start()
        {
            if (GameFlow.Instance != null) yield break;

            PlayerSpawnPoint spawn = null;
            float until = Time.unscaledTime + waitTimeout;
            while ((spawn = PlayerSpawnPoint.In(gameObject.scene)) == null && Time.unscaledTime < until) yield return null;
            if (spawn == null) yield break;

            GetComponent<Party>().PlaceLeaderAt(spawn.transform);

            // カメラが元の位置から滑ってこないように。
            foreach (CinemachineCamera camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                camera.PreviousStateIsValid = false;
            }
        }
    }
}
