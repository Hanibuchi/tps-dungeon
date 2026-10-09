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
    ///
    /// エディタでは Play していなくても、開いているシーンのキャラに小物を出す（置き方を見ながら合わせるため）。
    /// このときの小物はシーンに保存しない。カタログの値を変えると、その場で全員が付け直す。
    /// </summary>
    [ExecuteAlways]
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
                if (look.horn > 0) return catalog.HornColor(look.horn);
                if (look.ear > 0) return catalog.EarColor(look.ear);
                if (look.tail > 0) return catalog.TailColor(look.tail);
                return null;
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            CaptureBindPose(HumanBodyBones.Head);
            CaptureBindPose(HumanBodyBones.Hips);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            CharacterAccessoryCatalog.Edited += OnCatalogEdited;
#endif
            Apply();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            CharacterAccessoryCatalog.Edited -= OnCatalogEdited;
#endif
            Clear();
        }

        /// <summary>今の見た目で小物を付け直す。</summary>
        [ContextMenu("見た目を付け直す")]
        public void Apply()
        {
            applied = true;
            Clear();
            if (catalog == null || !ShowsAccessories) return;

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

#if UNITY_EDITOR
            // Play していないときに出す小物は、シーンにもプレハブの差分にも残さない。
            if (!Application.isPlaying) SetHideFlags(instance.transform, HideFlags.HideAndDontSave);
#endif

            // ルートから見た置き方を、覚えておいた骨の初めの姿勢から見た値に直して骨の下に置く。
            BoneLocal(placement, bone, out Vector3 localPosition, out Quaternion localRotation);

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
            foreach (GameObject go in spawned) DestroyAccessory(go);
            spawned.Clear();

            // スクリプトの読み込み直しで一覧だけ消えて、Play していないときに出した小物が骨の下に残っていることがある
            // （シーンを読み直さずに Play に入る設定でも残る）。保存しない印の付いた物だけ消す。
            RemoveLeftovers(Bone(HumanBodyBones.Head));
            RemoveLeftovers(Bone(HumanBodyBones.Hips));
        }

        private static void RemoveLeftovers(Transform bone)
        {
            if (bone == null) return;
            for (int i = bone.childCount - 1; i >= 0; i--)
            {
                Transform child = bone.GetChild(i);
                if (child.name.StartsWith(AccessoryPrefix) && (child.hideFlags & HideFlags.DontSave) != 0) DestroyAccessory(child.gameObject);
            }
        }

        private static void DestroyAccessory(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>
        /// 小物を出すか。Play 中は出す。Play していないときは、開いているシーン（とプレハブの編集画面）だけで出し、
        /// 組み込みがプレハブを裏で開いているとき（ほかのプレビュー用のシーン）には出さない（プレハブに混ざらないように）。
        /// </summary>
        private bool ShowsAccessories
        {
            get
            {
                if (Application.isPlaying) return true;
#if UNITY_EDITOR
                if (!gameObject.scene.IsValid()) return false;
                if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return true;
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                return stage != null && stage.scene == gameObject.scene;
#else
                return false;
#endif
            }
        }

        // ---- 置き方と骨の上の位置の行き来（エディタの調整の道具も使う） ----

        /// <summary>置き方 placement の小物が、今の骨の姿勢でどこにどの向きで付くか（ワールド）。骨が無ければ false。</summary>
        public bool TryGetWorldPose(CharacterAccessoryCatalog.Placement placement, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            Transform bone = Bone(placement.bone);
            if (bone == null) return false;

            BoneLocal(placement, bone, out Vector3 localPosition, out Quaternion localRotation);
            position = bone.TransformPoint(localPosition);
            rotation = bone.rotation * localRotation;
            return true;
        }

        /// <summary>
        /// ワールドの位置と向きに小物を置いたときの置き方（ルートから見た値）。骨と大きさは current のまま。骨が無ければ current を返す。
        /// </summary>
        public CharacterAccessoryCatalog.Placement ToPlacement(CharacterAccessoryCatalog.Placement current, Vector3 position, Quaternion rotation)
        {
            Transform bone = Bone(current.bone);
            if (bone == null) return current;

            Matrix4x4 bind = BindPose(current.bone, bone);
            Vector3 localPosition = bone.InverseTransformPoint(position);
            Quaternion localRotation = Quaternion.Inverse(bone.rotation) * rotation;
            Vector3 euler = (bind.rotation * localRotation).eulerAngles;
            euler = new Vector3(Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));
            return new CharacterAccessoryCatalog.Placement(current.bone, bind.MultiplyPoint3x4(localPosition), euler, current.scale);
        }

        private void BoneLocal(CharacterAccessoryCatalog.Placement placement, Transform bone, out Vector3 localPosition, out Quaternion localRotation)
        {
            Matrix4x4 rootToBone = BindPose(placement.bone, bone).inverse;
            localPosition = rootToBone.MultiplyPoint3x4(placement.position);
            localRotation = rootToBone.rotation * Quaternion.Euler(placement.euler);
        }

#if UNITY_EDITOR
        private bool reapplyQueued;

        private void OnValidate()
        {
            // インスペクタで見た目を変えたら付け直す（OnValidate の中では壊せないので後で）。
            if (isActiveAndEnabled) QueueApply();
        }

        private void OnCatalogEdited(CharacterAccessoryCatalog edited)
        {
            if (edited == catalog) QueueApply();
        }

        private void QueueApply()
        {
            if (reapplyQueued) return;
            reapplyQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                reapplyQueued = false;
                if (this != null && isActiveAndEnabled) Apply();
            };
        }

        private static void SetHideFlags(Transform t, HideFlags flags)
        {
            t.gameObject.hideFlags = flags;
            foreach (Transform child in t) SetHideFlags(child, flags);
        }
#endif

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
