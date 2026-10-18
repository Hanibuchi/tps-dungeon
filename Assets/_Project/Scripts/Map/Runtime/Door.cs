using TpsDungeon.Audio.Runtime;
using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// インタラクトで開け閉めする扉。動きは Animator が持つ（Closed / Open の 2 ステートを bool の Open で切り替え、
    /// 各ステートのクリップが Hinge を回して扉板を振る）。開き方や速さはクリップ（Animation/Door/）を直せばよい。
    /// 開いている途中・閉じている途中は触れない（開く用と閉じる用でクリップが別なので、途中で反転すると位置が飛ぶ）。
    /// 扉板の当たり判定は閉じきっているときだけ効かせる。動いている扉板に押されたり引っかかったりしないように。
    /// 閉ざす（<see cref="Seal"/>）と、閉まりきるまでの間は閉じた扉板の位置に見えない壁を立てる。開いている途中から閉め終えるまで開口が素通しになり、
    /// モンスターハウスに閉じ込めた直後に外へ戻れてしまうため。
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

        // 閉じたときに扉板の位置に誰かいないか調べる用。自分の枠や扉板も拾うので少し余裕を持たせる。
        private readonly Collider[] overlaps = new Collider[8];

        private BoxCollider[] leafColliders = System.Array.Empty<BoxCollider>();
        // 閉ざしてから閉まりきるまで、閉じた扉板の位置を塞ぐ見えない壁（扉板ごと）。Hinge の外に置くので回らない。
        private BoxCollider[] sealColliders = System.Array.Empty<BoxCollider>();
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
