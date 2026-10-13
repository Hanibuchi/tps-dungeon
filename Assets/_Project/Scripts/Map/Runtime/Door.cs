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

        // 閉じたときに扉板の位置に誰かいないか調べる用。自分の枠や扉板も拾うので少し余裕を持たせる。
        private readonly Collider[] overlaps = new Collider[8];

        private BoxCollider leafCollider;
        private bool isOpen;
        private bool closeSoundPending;

        /// <summary>開けた（開けている途中も含む）か。</summary>
        public bool IsOpen => isOpen;

        /// <summary>開け閉めで回る軸（扉板はこの子）。NavMesh を焼くとき、動く部分だけを外すのに使う。</summary>
        public Transform Hinge => hinge;

        /// <summary>開閉のアニメーションが終わっていないか。</summary>
        public bool IsSwinging => animator != null && IsAnimating(animator, isOpen ? OpenStateHash : ClosedStateHash);

        public string PromptLabel => isOpen ? closeLabel : openLabel;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (leaf != null) leafCollider = leaf.GetComponent<BoxCollider>();
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
            UpdateLeafCollider();

            if (closeSoundPending && !isOpen && !IsSwinging)
            {
                closeSoundPending = false;
                PlaySound(closeSound);
            }
        }

        public bool CanInteract(GameObject interactor) => animator != null && !IsSwinging;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            isOpen = !isOpen;
            animator.SetBool(OpenParamHash, isOpen);
            UpdateLeafCollider();

            if (isOpen) PlaySound(openSound);
            else closeSoundPending = true;
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
        /// </summary>
        private void UpdateLeafCollider()
        {
            if (leafCollider == null) return;

            if (!IsLeafSolid(isOpen, IsSwinging))
            {
                if (leafCollider.enabled) leafCollider.enabled = false;
                return;
            }

            if (leafCollider.enabled || IsDoorwayOccupied()) return;
            leafCollider.enabled = true;
        }

        private bool IsDoorwayOccupied()
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
