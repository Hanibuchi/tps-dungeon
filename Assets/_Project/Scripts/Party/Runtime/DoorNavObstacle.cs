using TpsDungeon.Map.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 閉じている扉の所で NavMesh の道を切る。開いたら道をつなぐ。
    /// 扉（Door）の GameObject に FloorNavMesh が付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Door))]
    [AddComponentMenu("TPS Dungeon/Door Nav Obstacle")]
    public sealed class DoorNavObstacle : MonoBehaviour
    {
        private Door door;
        private NavMeshObstacle obstacle;

        private void Awake()
        {
            door = GetComponent<Door>();
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            FitToOpening();
        }

        private void Update()
        {
            bool closed = !door.IsOpen;
            if (obstacle.enabled != closed) obstacle.enabled = closed;
        }

        /// <summary>
        /// 扉の開口に合わせる。開口は扉を触る範囲（トリガーの箱）か、扉の板（Leaf）の箱で測る。柱や鴨居は壁と同じく焼いてあるので見ない。
        /// 厚みは道を切れる程度にする。
        /// </summary>
        private void FitToOpening()
        {
            BoxCollider opening = null;
            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || box.name == "Leaf")
                {
                    opening = box;
                    if (box.isTrigger) break;
                }
            }

            if (opening == null)
            {
                obstacle.center = new Vector3(0f, 1f, 0f);
                obstacle.size = new Vector3(2f, 2f, 0.4f);
                return;
            }

            // 箱の中心と大きさを、扉の GameObject から見た値にする。
            Transform t = opening.transform;
            Vector3 center = transform.InverseTransformPoint(t.TransformPoint(opening.center));
            Vector3 size = transform.InverseTransformVector(t.TransformVector(opening.size));
            obstacle.center = center;
            obstacle.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Max(0.4f, Mathf.Abs(size.z)));
        }
    }
}
