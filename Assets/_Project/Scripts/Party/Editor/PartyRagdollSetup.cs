using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Enemies.Editor;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// パーティーのキャラ（Character Variant.prefab）の骨に、倒れ込み（<see cref="EnemyRagdoll"/>）用の体を焼く。
    /// 人型なので骨は Animator の HumanBodyBones で引く。焼き直すときは前の体を外してから付け直す。
    /// 倒れるのは PartyMember が HP 0 を見て呼ぶ。
    /// </summary>
    public static class PartyRagdollSetup
    {
        /// <summary>体を付ける骨。End までをカプセルにする（null なら骨の上に球）。Radius は見た目の大きさ（m）。</summary>
        private static readonly (HumanBodyBones Bone, HumanBodyBones? Parent, HumanBodyBones? End, float Radius, float Mass)[] Bones =
        {
            (HumanBodyBones.Hips, null, HumanBodyBones.Spine, 0.14f, 3f),
            (HumanBodyBones.Spine, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.15f, 2f),
            (HumanBodyBones.Head, HumanBodyBones.Spine, null, 0.12f, 1f),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.Spine, HumanBodyBones.LeftLowerArm, 0.055f, 1f),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, 0.05f, 1f),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.Spine, HumanBodyBones.RightLowerArm, 0.055f, 1f),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, 0.05f, 1f),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips, HumanBodyBones.LeftLowerLeg, 0.075f, 1f),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, 0.06f, 1f),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips, HumanBodyBones.RightLowerLeg, 0.075f, 1f),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, 0.06f, 1f),
        };

        [MenuItem("Tools/TPS Dungeon/組み込み/倒れ込みの骨を焼く", false, 321)]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PartySetup.CharacterPrefabPath);
            try
            {
                if (!Bake(root)) return;
                PrefabUtility.SaveAsPrefabAsset(root, PartySetup.CharacterPrefabPath);
                Debug.Log($"倒れ込みの骨を焼いた: {PartySetup.CharacterPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>root（キャラのルート）に体を焼く。人型の Animator が無ければ false。</summary>
        public static bool Bake(GameObject root)
        {
            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError($"{root.name}: 人型の Animator が無いので倒れ込みの骨を焼けない", root);
                return false;
            }

            foreach (var spec in Bones) RagdollBaker.RemoveBone(animator.GetBoneTransform(spec.Bone));
            if (root.TryGetComponent(out EnemyRagdoll old)) Object.DestroyImmediate(old, true);

            var bodies = new List<Rigidbody>();
            var byBone = new Dictionary<HumanBodyBones, Rigidbody>();
            foreach (var spec in Bones)
            {
                Transform bone = animator.GetBoneTransform(spec.Bone);
                if (bone == null)
                {
                    Debug.LogError($"{root.name}: 骨 {spec.Bone} が無い", root);
                    return false;
                }

                Transform end = spec.End.HasValue ? animator.GetBoneTransform(spec.End.Value) : null;
                Rigidbody parent = spec.Parent.HasValue ? byBone[spec.Parent.Value] : null;
                Rigidbody body = RagdollBaker.AddBone(bone, end, parent, spec.Radius, spec.Mass);
                bodies.Add(body);
                byBone[spec.Bone] = body;
            }

            root.AddComponent<EnemyRagdoll>().Configure(animator, bodies.ToArray(), new Transform[0], new Transform[0]);
            return true;
        }
    }
}
