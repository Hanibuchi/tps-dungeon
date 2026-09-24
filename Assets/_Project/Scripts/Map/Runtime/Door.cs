using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// インタラクトで開け閉めする扉。動きは Animator が持つ（Closed / Open の 2 ステートを bool の Open で切り替え、
    /// 各ステートのクリップが Hinge を回して扉板を振る）。開き方や速さはクリップ（Animation/Door/）を直せばよい。
    /// 開いている途中・閉じている途中は触れない（開く用と閉じる用でクリップが別なので、途中で反転すると位置が飛ぶ）。
    /// 扉板の当たり判定は閉じきっているときだけ効かせる。動いている扉板に押されたり引っかかったりしないように。
    ///
    /// 壁は FloorBuilder がルートを (cellSize, wallHeight, 1) に非一様スケールして大きさを合わせるが、
    /// ドアのルートをそうすると、その下で回す扉板が歪む（親の非一様スケールは子の回転の後に掛かるので、
    /// 子側で逆数を掛けても角度 0 のときしか打ち消せない）。
    /// そこでドアはルートをスケールせず、FloorBuilder が Fit を呼ぶ。枠（Frame）だけを 1x1 単位のまま
    /// スケールし、Hinge と扉板はスケールの掛からないところにメートルで置く。
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

        [SerializeField, Tooltip("柱・鴨居・照準用トリガーをまとめた子。1x1 単位で作ってあり、Fit で (幅, 高さ, 1) にスケールする。")]
        private Transform frame;

        [SerializeField, Tooltip("回す軸。開口の左端に置く。スケールしない。クリップはこれの回転を動かす。")]
        private Transform hinge;

        [SerializeField, Tooltip("扉板。Hinge の子。")]
        private Transform leaf;

        [SerializeField, Tooltip("開閉のアニメーションを流す Animator。未設定ならこの GameObject から探す。")]
        private Animator animator;

        [SerializeField, Tooltip("開口の幅。ドア全体の幅に対する比（枠と同じ比）。")]
        private float openingWidth = 0.4f;

        [SerializeField, Tooltip("開口の高さ。ドア全体の高さに対する比（枠と同じ比）。")]
        private float openingHeight = 0.75f;

        [SerializeField, Tooltip("扉板の厚み（メートル）。枠より薄くして、閉めたときに枠と重ならないようにする。")]
        private float leafThickness = 0.08f;

        [SerializeField, Tooltip("扉板と枠の隙間（メートル）。")]
        private float leafGap = 0.02f;

        [SerializeField, Tooltip("起動時に開けておく。")]
        private bool startOpen;

        [SerializeField] private string openLabel = "開ける";
        [SerializeField] private string closeLabel = "閉める";

        // 閉じたときに扉板の位置に誰かいないか調べる用。自分の枠や扉板も拾うので少し余裕を持たせる。
        private readonly Collider[] overlaps = new Collider[8];

        private Collider leafCollider;
        private bool isOpen;

        /// <summary>開けた（開けている途中も含む）か。</summary>
        public bool IsOpen => isOpen;

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
            if (leaf != null) leafCollider = leaf.GetComponent<Collider>();
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
        }

        public bool CanInteract(GameObject interactor) => animator != null && !IsSwinging;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            isOpen = !isOpen;
            animator.SetBool(OpenParamHash, isOpen);
            UpdateLeafCollider();
        }

        /// <summary>
        /// ドア全体を幅 width・高さ height（メートル）に合わせる。FloorBuilder が置いた直後に呼ぶ。
        /// 生成器がプレハブを作るときにも呼んで、エディタ上の見た目を揃える。
        /// </summary>
        public void Fit(float width, float height)
        {
            if (frame != null) frame.localScale = new Vector3(width, height, 1f);
            if (hinge == null || leaf == null) return;

            float leafWidth = openingWidth * width;
            float leafHeight = openingHeight * height;

            hinge.localPosition = new Vector3(-leafWidth * 0.5f, 0f, 0f);
            hinge.localScale = Vector3.one;

            leaf.localPosition = new Vector3(leafWidth * 0.5f, leafHeight * 0.5f, 0f);
            leaf.localRotation = Quaternion.identity;
            leaf.localScale = new Vector3(
                Mathf.Max(0.01f, leafWidth - leafGap * 2f),
                Mathf.Max(0.01f, leafHeight - leafGap),
                leafThickness);
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
            int count = Physics.OverlapBoxNonAlloc(
                leaf.position, leaf.lossyScale * 0.5f, overlaps, leaf.rotation, ~0, QueryTriggerInteraction.Ignore);
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
