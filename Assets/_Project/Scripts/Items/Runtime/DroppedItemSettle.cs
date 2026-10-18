using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 敵が落とした物を物理で床へ落とし、止まったら（Rigidbody が眠ったら）その場に固める。固めたあとは押されて動かない。
    /// 落ちきらないまま <see cref="maxFallTime"/> 秒過ぎても固める。固めたら自分を外す。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("TPS Dungeon/Dropped Item Settle")]
    public sealed class DroppedItemSettle : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("止まらなくてもこの秒数で固める。")]
        private float maxFallTime = 5f;

        private Rigidbody body;
        private float elapsed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            if (!body.IsSleeping() && elapsed < maxFallTime) return;

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            Destroy(this);
        }
    }
}
