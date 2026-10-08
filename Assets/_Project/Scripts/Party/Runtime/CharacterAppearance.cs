using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// キャラの見た目の個性（<see cref="CharacterLook"/>：角・耳・しっぽ）を、モデルの骨（頭・腰）に小物を付けて出す。
    /// 体（モデル）は全員同じなので、これで見分ける。小物の置き方は <see cref="CharacterAccessoryCatalog"/> が持つ。
    /// 置き方はキャラのルートから見た値で書いてあるので、起動したときの骨の姿勢（アニメーションが動く前）を覚えておき、それを基準に骨の下へ置く。
    /// 小物の当たりは外す（攻撃や照準を止めない）。レイヤーはキャラに揃える。
    /// キャラのルート（Animator と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Appearance")]
    public sealed class CharacterAppearance : MonoBehaviour
    {
        private const string AccessoryPrefix = "Accessory_";

        [SerializeField, Tooltip("小物のプレハブと置き方。")]
        private CharacterAccessoryCatalog catalog;

        [SerializeField, Tooltip("見た目。0 は付けない、1 以上は色の番号。")]
        private CharacterLook look;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly Dictionary<HumanBodyBones, Matrix4x4> bindPose = new Dictionary<HumanBodyBones, Matrix4x4>();
        private Animator animator;
        private bool applied;

        public CharacterLook Look
        {
            get => look;
            set
            {
                look = value.Clamped;
                if (applied) Apply();
            }
        }

        public CharacterAccessoryCatalog Catalog
        {
            get => catalog;
            set => catalog = value;
        }

        /// <summary>今の見た目の主な色（角 → 耳 → しっぽの順に最初に付いている物）。何も付けていなければ null。</summary>
        public Color? AccentColor
        {
            get
            {
                if (catalog == null) return null;
                if (look.horn > 0) return CharacterAccessoryCatalog.MainColor(catalog.HornLeft(look.horn));
                if (look.ear > 0) return CharacterAccessoryCatalog.MainColor(catalog.EarLeft(look.ear));
                if (look.tail > 0) return CharacterAccessoryCatalog.MainColor(catalog.Tail(look.tail));
                return null;
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            CaptureBindPose(HumanBodyBones.Head);
            CaptureBindPose(HumanBodyBones.Hips);
            Apply();
        }

        /// <summary>今の見た目で小物を付け直す。</summary>
        [ContextMenu("見た目を付け直す")]
        public void Apply()
        {
            applied = true;
            Clear();
            if (catalog == null) return;

            CharacterLook l = look.Clamped;
            if (l.horn > 0) SpawnPair(catalog.HornLeft(l.horn), catalog.HornRight(l.horn), catalog.HornPlacement, "Horn");
            if (l.ear > 0) SpawnPair(catalog.EarLeft(l.ear), catalog.EarRight(l.ear), catalog.EarPlacement, "Ear");
            if (l.tail > 0) Spawn(catalog.Tail(l.tail), catalog.TailPlacement, "Tail");
        }

        private void SpawnPair(GameObject left, GameObject right, CharacterAccessoryCatalog.Placement placement, string label)
        {
            Spawn(left, placement, label + "_L");
            Spawn(right, placement.Mirrored, label + "_R");
        }

        private void Spawn(GameObject prefab, CharacterAccessoryCatalog.Placement placement, string label)
        {
            if (prefab == null) return;

            Transform bone = Bone(placement.bone);
            if (bone == null) return;

            GameObject instance = Instantiate(prefab);
            instance.name = AccessoryPrefix + label;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            SetLayer(instance.transform, gameObject.layer);

            // ルートから見た置き方を、覚えておいた骨の初めの姿勢から見た値に直して骨の下に置く。
            Matrix4x4 rootToBone = BindPose(placement.bone, bone).inverse;
            Vector3 localPosition = rootToBone.MultiplyPoint3x4(placement.position);
            Quaternion localRotation = rootToBone.rotation * Quaternion.Euler(placement.euler);

            Transform t = instance.transform;
            t.SetParent(bone, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            float boneScale = Mathf.Max(1e-4f, bone.lossyScale.x / Mathf.Max(1e-4f, transform.lossyScale.x));
            t.localScale = Vector3.one * (placement.scale <= 0f ? 1f : placement.scale) / boneScale;
            spawned.Add(instance);
        }

        private void Clear()
        {
            foreach (GameObject go in spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            spawned.Clear();
        }

        private Transform Bone(HumanBodyBones bone)
        {
            if (animator == null) animator = GetComponent<Animator>();
            return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        }

        private void CaptureBindPose(HumanBodyBones bone)
        {
            Transform t = Bone(bone);
            if (t != null) bindPose[bone] = transform.worldToLocalMatrix * t.localToWorldMatrix;
        }

        /// <summary>骨の初めの姿勢（ルートから見た行列）。覚えていなければ今の姿勢。</summary>
        private Matrix4x4 BindPose(HumanBodyBones bone, Transform t)
        {
            if (bindPose.TryGetValue(bone, out Matrix4x4 m)) return m;
            m = transform.worldToLocalMatrix * t.localToWorldMatrix;
            bindPose[bone] = m;
            return m;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }
    }
}
