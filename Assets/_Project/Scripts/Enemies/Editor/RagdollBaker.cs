using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 骨に Rigidbody・カプセル・関節を付ける（<see cref="EnemyRagdoll"/> が倒れ込みに使う）。敵（MonsterBuilder）とパーティーのキャラ（PartySetup）で使う。
    /// 普段は kinematic でコライダーも切っておき、倒れるときに EnemyRagdoll が物理へ切り替える。
    /// プレハブは原点・バインドポーズのまま測る。
    /// </summary>
    public static class RagdollBaker
    {
        /// <summary>
        /// bone に体を付ける。end の骨の位置までをカプセルにし、end が null なら骨の位置の上に radius の球を置く。
        /// radius は見た目の大きさ（m、プレハブに置いたときの）。parent があれば関節でつなぐ。
        /// </summary>
        public static Rigidbody AddBone(Transform bone, Transform end, Rigidbody parent, float radius, float mass)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // 骨は大きく拡縮されていることがある（Quaternius は FBX の 100 倍 × 背丈合わせ）ので、ワールドで決めてから骨の座標へ直す。
            float scale = bone.lossyScale.x;
            float localRadius = radius / scale;
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = localRadius;
            int twist = 1;
            if (end != null)
            {
                Vector3 toEnd = bone.InverseTransformPoint(end.position);
                twist = LongestAxis(toEnd);
                capsule.direction = twist;
                capsule.center = toEnd / 2f;
                capsule.height = Mathf.Max(Mathf.Abs(toEnd[twist]) + localRadius, localRadius * 2f);
            }
            else
            {
                capsule.direction = 1;
                capsule.center = bone.InverseTransformPoint(bone.position + Vector3.up * radius);
                capsule.height = localRadius * 2f;
            }
            capsule.enabled = false;

            if (parent == null) return body;

            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.axis = AxisVector(twist);
            joint.swingAxis = AxisVector((twist + 1) % 3);
            joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
            joint.highTwistLimit = new SoftJointLimit { limit = 20f };
            joint.swing1Limit = new SoftJointLimit { limit = 40f };
            joint.swing2Limit = new SoftJointLimit { limit = 30f };
            joint.enableProjection = true;
            return body;
        }

        /// <summary>AddBone で付けた物を外す（作り直す前に）。</summary>
        public static void RemoveBone(Transform bone)
        {
            if (bone == null) return;
            if (bone.TryGetComponent(out CharacterJoint joint)) Object.DestroyImmediate(joint, true);
            if (bone.TryGetComponent(out CapsuleCollider capsule)) Object.DestroyImmediate(capsule, true);
            if (bone.TryGetComponent(out Rigidbody body)) Object.DestroyImmediate(body, true);
        }

        private static int LongestAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }

        private static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
    }
}
