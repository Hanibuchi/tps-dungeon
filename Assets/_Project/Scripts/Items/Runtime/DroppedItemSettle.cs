using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 落とした物を物理で床へ落とし、止まったら（Rigidbody が眠ったら）その場に固める。固めたあとは押されて動かない。
    /// 落ちきらないまま <see cref="maxFallTime"/> 秒過ぎても固める。
    /// 固めたあとも、ときどき真下に乗っている物があるかを見て、無くなっていたら（乗っていた扉が開いた・宙で固めた）また落とす。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("TPS Dungeon/Dropped Item Settle")]
    public sealed class DroppedItemSettle : MonoBehaviour
    {
        /// <summary>真下を見る厚み（m）。箱の底からこの深さまでに乗る物が無ければ浮いている。</summary>
        private const float SupportDepth = 0.08f;

        [SerializeField, Min(0f), Tooltip("止まらなくてもこの秒数で固める。")]
        private float maxFallTime = 5f;

        [SerializeField, Min(0.05f), Tooltip("固めたあと、真下に乗る物があるかを見る間隔（秒）。")]
        private float supportCheckInterval = 0.5f;

        private Rigidbody body;
        private Collider solid;
        private float elapsed;
        private bool settled;
        private float nextCheck;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                solid = c;
                break;
            }
        }

        private void FixedUpdate()
        {
            if (settled)
            {
                if (Time.time < nextCheck) return;
                nextCheck = Time.time + supportCheckInterval;
                if (!HasSupport()) Release();
                return;
            }

            elapsed += Time.fixedDeltaTime;
            if (!body.IsSleeping() && elapsed < maxFallTime) return;

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            settled = true;
            nextCheck = Time.time + supportCheckInterval;
        }

        /// <summary>固めるのをやめて、また落とす。</summary>
        private void Release()
        {
            settled = false;
            elapsed = 0f;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.WakeUp();
        }

        /// <summary>物理の箱の底のすぐ下に、この箱が当たるレイヤーの物があるか。箱が無ければ乗っているとみなす。</summary>
        private bool HasSupport()
        {
            if (solid == null || !solid.enabled) return true;

            Bounds bounds = solid.bounds;
            var center = new Vector3(bounds.center.x, bounds.min.y - SupportDepth * 0.5f, bounds.center.z);
            // 横は少し内へ縮めて、壁にかすっているだけを乗っていると数えないように。
            var halfExtents = new Vector3(bounds.extents.x * 0.9f, SupportDepth * 0.5f + 0.02f, bounds.extents.z * 0.9f);
            return Physics.CheckBox(center, halfExtents, Quaternion.identity, CollisionMask(solid.gameObject.layer), QueryTriggerInteraction.Ignore);
        }

        /// <summary>layer が物理で当たるレイヤーの組（衝突の設定から作る）。</summary>
        private static int CollisionMask(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
            }

            return mask;
        }
    }
}
