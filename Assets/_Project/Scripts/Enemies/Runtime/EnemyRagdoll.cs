using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 気絶したときに体を物理に任せて倒れ込ませ、起き上がるときにアニメーションへ戻す。
    ///
    /// 骨の Rigidbody・コライダー・CharacterJoint は MonsterBuilder がプレハブに焼き込む（<see cref="bodies"/> の先頭が腰）。
    /// 普段は全部 kinematic でコライダーも切っておき、当たり判定はルートの CapsuleCollider が受け持つ。
    ///
    /// 起き上がりは、腰の真下の床へルートを移してから Animator を戻し、
    /// 倒れていた姿勢から Animator の姿勢へ <see cref="getUpBlendTime"/> 秒かけて寄せる（いきなり立った姿勢に飛ばないように）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Ragdoll")]
    public sealed class EnemyRagdoll : MonoBehaviour
    {
        [SerializeField, Tooltip("体を動かしている Animator（子の Model）。")]
        private Animator animator;

        [SerializeField, Tooltip("骨の Rigidbody。先頭が腰（押し出しと起き上がりの基準）。")]
        private Rigidbody[] bodies = new Rigidbody[0];

        [SerializeField, Tooltip("倒れている間、followTargets の同じ番号の骨にくっついていく骨。" +
            "IK 用に脚の外に置かれた足首の骨など、物理の骨の子になっていないものを置き去りにしないため。")]
        private Transform[] followers = new Transform[0];

        [SerializeField]
        private Transform[] followTargets = new Transform[0];

        [SerializeField, Min(0f), Tooltip("起き上がるとき、倒れた姿勢から立った姿勢へ寄せる時間（秒）。")]
        private float getUpBlendTime = 0.4f;

        [SerializeField, Tooltip("起き上がるときに床を探す対象のレイヤー。")]
        private LayerMask groundMask = ~0;

        private Collider[] rootColliders;
        private Collider[] boneColliders;
        private Vector3[] followLocalPositions;
        private Quaternion[] followLocalRotations;

        private Transform[] bones;
        private Transform[] moved;
        private Vector3[] movedPositions;
        private Quaternion[] movedRotations;
        private Vector3[] blendFromPositions;
        private Quaternion[] blendFromRotations;
        private float blendElapsed = -1f;

        private Vector3 standingPelvisOffset;
        private bool initialized;

        /// <summary>今、物理に任せて倒れているか。</summary>
        public bool IsRagdoll { get; private set; }

        public IReadOnlyList<Rigidbody> Bodies => bodies;

        /// <summary>MonsterBuilder から骨を渡す。</summary>
        public void Configure(Animator animator, Rigidbody[] bodies, Transform[] followers, Transform[] followTargets)
        {
            this.animator = animator;
            this.bodies = bodies;
            this.followers = followers;
            this.followTargets = followTargets;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            rootColliders = GetComponents<Collider>();

            var colliders = new List<Collider>();
            foreach (Rigidbody body in bodies)
            {
                if (body != null) colliders.AddRange(body.GetComponents<Collider>());
            }
            boneColliders = colliders.ToArray();

            // つながっていない骨同士（腕と胴など）がぶつかって震えないように。
            for (int i = 0; i < boneColliders.Length; i++)
            {
                for (int j = i + 1; j < boneColliders.Length; j++)
                {
                    Physics.IgnoreCollision(boneColliders[i], boneColliders[j]);
                }
            }

            int count = Mathf.Min(followers.Length, followTargets.Length);
            followLocalPositions = new Vector3[count];
            followLocalRotations = new Quaternion[count];

            // Model 自体はルートと一緒に動かすので、姿勢を扱うのはその下の骨だけ。
            var boneList = new List<Transform>();
            if (animator != null)
            {
                foreach (Transform t in animator.GetComponentsInChildren<Transform>())
                {
                    if (t != animator.transform) boneList.Add(t);
                }
            }
            bones = boneList.ToArray();

            // 物理で動いた骨と、それにくっついていった骨。親から先に並べる（親を戻すと子も動くため）。
            var movedList = new List<Transform>();
            foreach (Rigidbody body in bodies)
            {
                if (body != null) movedList.Add(body.transform);
            }
            for (int i = 0; i < count; i++)
            {
                if (followers[i] != null) movedList.Add(followers[i]);
            }
            moved = movedList.ToArray();
            movedPositions = new Vector3[moved.Length];
            movedRotations = new Quaternion[moved.Length];
            blendFromPositions = new Vector3[bones.Length];
            blendFromRotations = new Quaternion[bones.Length];

            // 立っているときの腰の位置（ルートから見て、水平だけ）。起き上がるとき腰の真下からこの分だけ戻す。
            if (bodies.Length > 0 && bodies[0] != null)
            {
                standingPelvisOffset = transform.InverseTransformPoint(bodies[0].position);
                standingPelvisOffset.y = 0f;
            }

            SetPhysical(false);
        }

        /// <summary>物理に任せて倒れ込む。push は腰から上に加える速さ（m/s、ワールド）。</summary>
        public void Fall(Vector3 push)
        {
            Initialize();
            if (IsRagdoll || bodies.Length == 0) return;

            IsRagdoll = true;
            blendElapsed = -1f;

            for (int i = 0; i < followLocalPositions.Length; i++)
            {
                if (followers[i] == null || followTargets[i] == null) continue;
                followLocalPositions[i] = followTargets[i].InverseTransformPoint(followers[i].position);
                followLocalRotations[i] = Quaternion.Inverse(followTargets[i].rotation) * followers[i].rotation;
            }

            if (animator != null) animator.enabled = false;
            foreach (Collider c in rootColliders) c.enabled = false;
            SetPhysical(true);

            // 腰は半分だけ押し、上半身を多めに押してのけぞるように倒す。
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null) continue;
                bodies[i].AddForce(i == 0 ? push * 0.5f : push, ForceMode.VelocityChange);
            }
        }

        /// <summary>腰の真下の床で起き上がり、Animator の動きに戻る。</summary>
        public void GetUp()
        {
            Initialize();
            if (!IsRagdoll) return;

            IsRagdoll = false;
            SetPhysical(false);

            // ルートを腰の真下へ移す。骨は子なので一緒に動いてしまうから、物理で動いた骨だけ見た目の位置に戻す
            // （それ以外はルートと一緒に動かないと、アニメーションが触らない骨にずれが残る）。
            for (int i = 0; i < moved.Length; i++)
            {
                movedPositions[i] = moved[i].position;
                movedRotations[i] = moved[i].rotation;
            }
            transform.position = GroundBelow(bodies[0].position) - transform.TransformVector(standingPelvisOffset);
            for (int i = 0; i < moved.Length; i++)
            {
                moved[i].SetPositionAndRotation(movedPositions[i], movedRotations[i]);
            }

            // 移したあとの（ルートから見た）倒れた姿勢を覚えて、そこから寄せる。
            for (int i = 0; i < bones.Length; i++)
            {
                blendFromPositions[i] = bones[i].localPosition;
                blendFromRotations[i] = bones[i].localRotation;
            }
            blendElapsed = 0f;

            foreach (Collider c in rootColliders) c.enabled = true;
            if (animator != null) animator.enabled = true;
        }

        private Vector3 GroundBelow(Vector3 point)
        {
            Vector3 fallback = new Vector3(point.x, transform.position.y, point.z);
            var hits = Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.down, 5f, groundMask, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            Vector3 result = fallback;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    result = hit.point;
                }
            }
            return result;
        }

        private void SetPhysical(bool physical)
        {
            foreach (Rigidbody body in bodies)
            {
                if (body == null) continue;
                body.isKinematic = !physical;
                body.interpolation = physical ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }
            foreach (Collider c in boneColliders) c.enabled = physical;
        }

        private void LateUpdate()
        {
            if (IsRagdoll)
            {
                for (int i = 0; i < followLocalPositions.Length; i++)
                {
                    if (followers[i] == null || followTargets[i] == null) continue;
                    followers[i].SetPositionAndRotation(
                        followTargets[i].TransformPoint(followLocalPositions[i]),
                        followTargets[i].rotation * followLocalRotations[i]);
                }
                return;
            }

            if (blendElapsed < 0f) return;

            // Animator がこのフレームの姿勢を書いたあとなので、そこへ倒れた姿勢から寄せる。
            blendElapsed += Time.deltaTime;
            float w = getUpBlendTime > 0f ? Mathf.SmoothStep(0f, 1f, blendElapsed / getUpBlendTime) : 1f;
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(blendFromPositions[i], bones[i].localPosition, w);
                bones[i].localRotation = Quaternion.Slerp(blendFromRotations[i], bones[i].localRotation, w);
            }
            if (w >= 1f) blendElapsed = -1f;
        }
    }
}
