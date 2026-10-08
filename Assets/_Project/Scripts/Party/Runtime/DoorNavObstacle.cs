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

        /// <summary>扉の板の大きさに合わせる（開口いっぱい、厚みは道を切れる程度に）。</summary>
        private void FitToOpening()
        {
            Collider leaf = null;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                leaf = c;
                break;
            }

            if (leaf == null)
            {
                obstacle.center = new Vector3(0f, 1f, 0f);
                obstacle.size = new Vector3(1.6f, 2f, 0.4f);
                return;
            }

            Transform t = leaf.transform;
            Vector3 lossy = t.lossyScale;
            Vector3 size = transform.InverseTransformVector(t.TransformVector(Vector3.one));
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Max(0.4f, Mathf.Abs(size.z)));
            obstacle.center = transform.InverseTransformPoint(t.position);
            obstacle.size = lossy.sqrMagnitude > 0f ? size : new Vector3(1.6f, 2f, 0.4f);
        }
    }
}
