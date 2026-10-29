using TpsDungeon.Audio.Runtime;
using TpsDungeon.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// インタラクトで開け閉めする扉。動きは Animator が持つ（Closed / Open の 2 ステートを bool の Open で切り替え、
    /// 各ステートのクリップが Hinge を回して扉板を振る）。開き方や速さはクリップ（Animation/Door/）を直せばよい。
    /// 開いている途中・閉じている途中は触れない（開く用と閉じる用でクリップが別なので、途中で反転すると位置が飛ぶ）。
    /// 扉板の当たり判定は閉じきっているときだけ効かせる。動いている扉板に押されたり引っかかったりしないように。
    /// 閉ざす（<see cref="Seal"/>）と、閉まりきるまでの間は閉じた扉板の位置に見えない壁を立てる。開いている途中から閉め終えるまで開口が素通しになり、
    /// モンスターハウスに閉じ込めた直後に外へ戻れてしまうため。
    /// 開いている扉板は当たり判定が無くカメラがよけないので、カメラと操作キャラの間に入ったら半透明にする（影は残す。<see cref="HideLeavesBetween"/>）。
    /// 音は開け始めに開ける音、閉まりきったときに閉める音（「バタン」が頭にあるので、閉め始めではなく当たったときに鳴らす）。
    ///
    /// 枠も扉板も実寸（メートル）で作ってあり、どこもスケールしない。素材の模様の密度を壁と揃えるためと、
    /// 親を非一様スケールすると回した扉板が歪むため（親の非一様スケールは子の回転の後に掛かる）。
    /// 大きさを変えるときは生成器（壁・床・天井のパーツを生成）で作り直す。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Door")]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        // AnimatorController（DoorAnimatorBuilder が作る）と揃える名前。
        public const string OpenParam = "Open";
        public const string ClosedState = "Closed";
        public const string OpenState = "Open";

        private static readonly int OpenParamHash = Animator.StringToHash(OpenParam);
        private static readonly int ClosedStateHash = Animator.StringToHash(ClosedState);
        private static readonly int OpenStateHash = Animator.StringToHash(OpenState);

        [SerializeField, Tooltip("回す軸。開口の左端に置く。スケールしない。クリップはこれの回転を動かす。")]
        private Transform hinge;

        [SerializeField, Tooltip("扉板。Hinge の子。当たり判定は BoxCollider（閉じたときに人が居ないかもこの箱で見る）。")]
        private Transform leaf;

        [SerializeField, Tooltip("両開きの扉の右の軸（開口の右端）。片開きなら空。クリップはこれも動かす。")]
        private Transform hingeRight;

        [SerializeField, Tooltip("両開きの扉の右の扉板。hingeRight の子。片開きなら空。")]
        private Transform leafRight;

        [SerializeField, Tooltip("開閉のアニメーションを流す Animator。未設定ならこの GameObject から探す。")]
        private Animator animator;

        [SerializeField, Tooltip("起動時に開けておく。")]
        private bool startOpen;

        [SerializeField, Tooltip("開け始めに鳴らす音。")]
        private AudioClip openSound;

        [SerializeField, Tooltip("閉まりきったときに鳴らす音。")]
        private AudioClip closeSound;

        [SerializeField, Range(0f, 1f), Tooltip("開け閉めの音の大きさ。")]
        private float soundVolume = 0.8f;

        [SerializeField] private string openLabel = "開ける";
        [SerializeField] private string closeLabel = "閉める";

        [SerializeField, Tooltip("開けたあと閉められるか。外すと一度開けたら開いたまま（ボス部屋の大扉など）で、開いたあとは案内も出さない。")]
        private bool canClose = true;

        [SerializeField, Tooltip("閉ざされているか。閉ざされている間は調べても開かない（案内だけ出す）。Unlock で開けられるようになる。")]
        private bool locked;

        [SerializeField] private string lockedLabel = "閉ざされている";

        [SerializeField, Range(0f, 1f), Tooltip("開いた扉板がカメラと操作キャラの間に入ったときの不透明度。")]
        private float occludedAlpha = 0.25f;

        [SerializeField, Min(0.01f), Tooltip("半透明になるまで・戻るまでにかける時間（秒）。")]
        private float occludeFadeDuration = 0.2f;

        // 閉じたときに扉板の位置に誰かいないか調べる用。自分の枠や扉板も拾うので少し余裕を持たせる。
        private readonly Collider[] overlaps = new Collider[8];

        private BoxCollider[] leafColliders = System.Array.Empty<BoxCollider>();
        // 閉ざしてから閉まりきるまで、閉じた扉板の位置を塞ぐ見えない壁（扉板ごと）。Hinge の外に置くので回らない。
        private BoxCollider[] sealColliders = System.Array.Empty<BoxCollider>();
        // 扉板の見た目。カメラとの間に入ったら元の材質を半透明の写し（初めて要るときに作る）へ差し替えて薄くし、戻りきったら元へ戻す。
        private Renderer[] leafRenderers = System.Array.Empty<Renderer>();
        private Material[][] opaqueMaterials = System.Array.Empty<Material[]>();
        private Material[][] fadeMaterials;
        private Color[][] fadeColors;
        private float leafAlpha = 1f;
        private bool usingFadeMaterials;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly System.Collections.Generic.List<Door> Active = new System.Collections.Generic.List<Door>();

        // カメラの半径と near clip の分だけ扉板の箱を広げて、かすめたときも消す（m）。
        private const float CameraClearance = 0.25f;

        private bool isOpen;
        private bool closeSoundPending;
        private bool closePending;

        /// <summary>開けた（開けている途中も含む）か。</summary>
        public bool IsOpen => isOpen;

        /// <summary>開け閉めで回る軸（扉板はこの子）。NavMesh を焼くとき、動く部分だけを外すのに使う。</summary>
        public Transform Hinge => hinge;

        /// <summary>開け閉めで回る軸を全部（両開きなら左右の 2 つ）。</summary>
        public System.Collections.Generic.IEnumerable<Transform> Hinges
        {
            get
            {
                if (hinge != null) yield return hinge;
                if (hingeRight != null) yield return hingeRight;
            }
        }

        /// <summary>開閉のアニメーションが終わっていないか。</summary>
        public bool IsSwinging => animator != null && IsAnimating(animator, isOpen ? OpenStateHash : ClosedStateHash);

        public string PromptLabel => locked ? lockedLabel : isOpen ? closeLabel : openLabel;

        /// <summary>閉ざされているか。ボスを倒すまで開かない扉など。</summary>
        public bool IsLocked
        {
            get => locked;
            set => locked = value;
        }

        /// <summary>閉ざされた扉を開けられるようにする。</summary>
        public void Unlock()
        {
            locked = false;
            SetSealCollidersEnabled(false);
        }

        /// <summary>調べて開けた（開け始めた）。</summary>
        public event System.Action<Door> Opened;

        /// <summary>
        /// 閉ざして閉める（モンスターハウスに閉じ込めるときなど）。開いていれば閉め、開いている途中なら開ききってから閉める
        /// （開く用と閉じる用でクリップが別なので、途中で反転すると位置が飛ぶ）。開けられるようにするのは Unlock。
        /// </summary>
        public void Seal()
        {
            locked = true;
            closePending = isOpen;
            if (isOpen) SetSealCollidersEnabled(true);
            TryClosePending();
        }

        private void TryClosePending()
        {
            if (!closePending || animator == null || IsSwinging) return;
            closePending = false;
            if (!isOpen) return;
            isOpen = false;
            animator.SetBool(OpenParamHash, false);
            UpdateLeafCollider();
            closeSoundPending = true;
        }

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            var colliders = new System.Collections.Generic.List<BoxCollider>(2);
            var seals = new System.Collections.Generic.List<BoxCollider>(2);
            foreach (var (t, axis) in new[] { (leaf, hinge), (leafRight, hingeRight) })
            {
                if (t == null || !t.TryGetComponent(out BoxCollider box)) continue;
                colliders.Add(box);
                if (axis != null) seals.Add(CreateSealCollider(box, axis));
            }
            leafColliders = colliders.ToArray();
            sealColliders = seals.ToArray();

            var renderers = new System.Collections.Generic.List<Renderer>();
            if (leaf != null) renderers.AddRange(leaf.GetComponentsInChildren<Renderer>(true));
            if (leafRight != null) renderers.AddRange(leafRight.GetComponentsInChildren<Renderer>(true));
            leafRenderers = renderers.ToArray();
            opaqueMaterials = System.Array.ConvertAll(leafRenderers, r => r.sharedMaterials);
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable()
        {
            Active.Remove(this);
            leafAlpha = 1f;
            UseFadeMaterials(false);
        }

        private void OnDestroy()
        {
            if (fadeMaterials == null) return;
            foreach (var materials in fadeMaterials)
            {
                foreach (var m in materials)
                {
                    if (m != null) Destroy(m);
                }
            }
        }

        /// <summary>
        /// 閉じきっていない扉板のうち、target（カメラが追う点）からカメラまでの間に入っている物、カメラがめり込んでいる物を半透明へ寄せ、外れた物は不透明へ戻す。
        /// カメラが動いたあと毎フレーム呼ぶ。閉じきった扉板は当たり判定がありカメラがよけるので薄くしない。
        /// </summary>
        public static void HideLeavesBetween(Vector3 camera, Vector3 target)
        {
            Vector3 toCamera = camera - target;
            float distance = toCamera.magnitude;
            var ray = new Ray(target, distance > 0.0001f ? toCamera / distance : Vector3.forward);
            foreach (var door in Active)
            {
                bool hide = false;
                if (door.isOpen || door.IsSwinging)
                {
                    foreach (var renderer in door.leafRenderers)
                    {
                        if (renderer == null) continue;
                        Bounds bounds = renderer.bounds;
                        bounds.Expand(CameraClearance * 2f);
                        if (bounds.Contains(camera) || bounds.IntersectRay(ray, out float hit) && hit <= distance)
                        {
                            hide = true;
                            break;
                        }
                    }
                }

                door.FadeLeaves(hide ? door.occludedAlpha : 1f);
            }
        }

        private void FadeLeaves(float targetAlpha)
        {
            if (Mathf.Approximately(leafAlpha, targetAlpha)) return;

            // 不透明 ⇔ 半透明を occludeFadeDuration 秒で動かす速さ。
            float speed = Mathf.Max(1f - occludedAlpha, 0.01f) / occludeFadeDuration;
            leafAlpha = Mathf.MoveTowards(leafAlpha, targetAlpha, speed * Time.deltaTime);
            if (leafAlpha >= 1f)
            {
                UseFadeMaterials(false);
                return;
            }

            UseFadeMaterials(true);
            for (int i = 0; i < fadeMaterials.Length; i++)
            {
                for (int j = 0; j < fadeMaterials[i].Length; j++)
                {
                    if (fadeMaterials[i][j] == null) continue;
                    Color c = fadeColors[i][j];
                    c.a *= leafAlpha;
                    fadeMaterials[i][j].SetColor(BaseColorId, c);
                }
            }
        }

        private void UseFadeMaterials(bool value)
        {
            if (usingFadeMaterials == value) return;
            if (value && fadeMaterials == null) CreateFadeMaterials();
            usingFadeMaterials = value;
            for (int i = 0; i < leafRenderers.Length; i++)
            {
                if (leafRenderers[i] != null) leafRenderers[i].sharedMaterials = value ? fadeMaterials[i] : opaqueMaterials[i];
            }
        }

        private void CreateFadeMaterials()
        {
            fadeMaterials = new Material[opaqueMaterials.Length][];
            fadeColors = new Color[opaqueMaterials.Length][];
            for (int i = 0; i < opaqueMaterials.Length; i++)
            {
                Material[] source = opaqueMaterials[i];
                fadeMaterials[i] = new Material[source.Length];
                fadeColors[i] = new Color[source.Length];
                for (int j = 0; j < source.Length; j++)
                {
                    if (source[j] == null) continue;
                    var m = new Material(source[j]) { name = source[j].name + " (Fade)" };
                    MakeTransparent(m);
                    fadeMaterials[i][j] = m;
                    fadeColors[i][j] = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                }
            }
        }

        /// <summary>URP Lit の材質を半透明（アルファで混ぜる）に切り替える。EnemyDeath.MakeTransparent と同じ（Map は Enemies を参照しないので写した）。</summary>
        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// 扉板の当たり判定と同じ箱を、閉じた姿勢（Closed のクリップは軸の回転が 0）の位置に、この GameObject の直下へ作る。最初は切っておく。
        /// 扉はどこもスケールしない前提なので、箱の大きさはそのまま写す。
        /// </summary>
        private BoxCollider CreateSealCollider(BoxCollider box, Transform axis)
        {
            Matrix4x4 closedAxis = axis.parent.localToWorldMatrix * Matrix4x4.TRS(axis.localPosition, Quaternion.identity, Vector3.one);
            Matrix4x4 closedLeaf = closedAxis * (axis.worldToLocalMatrix * box.transform.localToWorldMatrix);

            var go = new GameObject(box.name + " (Seal)") { layer = box.gameObject.layer };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(closedLeaf.GetColumn(3), closedLeaf.rotation);

            var seal = go.AddComponent<BoxCollider>();
            seal.center = box.center;
            seal.size = box.size;
            seal.enabled = false;
            return seal;
        }

        private void SetSealCollidersEnabled(bool value)
        {
            foreach (var seal in sealColliders)
            {
                if (seal != null) seal.enabled = value;
            }
        }

        // ドメインリロードを切っているので、再生のたびに前回の扉を捨てる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void Start()
        {
            // 起動直後に開閉の動きを見せないよう、行き先のステートの最後から始める。
            isOpen = startOpen;
            if (animator != null)
            {
                animator.SetBool(OpenParamHash, isOpen);
                animator.Play(isOpen ? OpenStateHash : ClosedStateHash, 0, 1f);
            }

            UpdateLeafCollider();
        }

        private void Update()
        {
            TryClosePending();
            UpdateLeafCollider();

            if (closeSoundPending && !isOpen && !IsSwinging)
            {
                closeSoundPending = false;
                PlaySound(closeSound);
            }
        }

        public bool CanInteract(GameObject interactor) => animator != null && !IsSwinging && CanToggle(isOpen, canClose);

        /// <summary>今の開き具合から開け閉めできるか。閉められない扉は開いたあと触れない。</summary>
        public static bool CanToggle(bool isOpen, bool canClose) => !isOpen || canClose;

        /// <summary>開けたあと閉められるか。</summary>
        public bool CanClose => canClose;

        public void Interact(GameObject interactor)
        {
            if (locked || !CanInteract(interactor)) return;

            isOpen = !isOpen;
            animator.SetBool(OpenParamHash, isOpen);
            UpdateLeafCollider();

            if (isOpen) PlaySound(openSound);
            else closeSoundPending = true;

            if (isOpen) Opened?.Invoke(this);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            Vector3 at = leaf != null ? leaf.position : transform.position;
            GameAudio.Instance?.PlaySeAt(clip, at, soundVolume);
        }

        /// <summary>
        /// 扉板の当たり判定を状態に合わせる。閉じきっていなければ切る。
        /// 閉じきっても、開口に誰か立っているうちは入れない（閉じ込めて押し出せなくなるので）。いなくなったら入れる。
        /// 両開きなら扉板ごとに見る。
        /// </summary>
        private void UpdateLeafCollider()
        {
            bool solid = IsLeafSolid(isOpen, IsSwinging);
            foreach (var leafCollider in leafColliders)
            {
                if (!solid)
                {
                    if (leafCollider.enabled) leafCollider.enabled = false;
                    continue;
                }

                if (leafCollider.enabled || IsDoorwayOccupied(leafCollider)) continue;
                leafCollider.enabled = true;
            }

            // 閉まりきって扉板が塞いだら、見えない壁はもう要らない。
            if (solid) SetSealCollidersEnabled(false);
        }

        private bool IsDoorwayOccupied(BoxCollider leafCollider)
        {
            Transform t = leafCollider.transform;
            int count = Physics.OverlapBoxNonAlloc(
                t.TransformPoint(leafCollider.center), Vector3.Scale(leafCollider.size, t.lossyScale) * 0.5f,
                overlaps, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!overlaps[i].transform.IsChildOf(transform)) return true;
            }

            return false;
        }

        /// <summary>
        /// 行き先のステートに入りきって、そのクリップを最後まで流し終えていなければ動いている。
        /// SetBool した直後は遷移がまだ始まっておらず前のステートにいるので、それも動いている扱いにする。
        /// </summary>
        private static bool IsAnimating(Animator animator, int destinationStateHash)
        {
            if (animator.IsInTransition(0)) return true;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash != destinationStateHash || state.normalizedTime < 1f;
        }

        /// <summary>扉板に当たり判定を持たせてよいか。閉じる途中や開いている間は持たせない。</summary>
        public static bool IsLeafSolid(bool isOpen, bool isSwinging)
        {
            return !isOpen && !isSwinging;
        }
    }
}
