using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// インタラクトで開け閉めする扉。開口の片端にある Hinge を Y 軸で回し、その子の Leaf（扉板）を振る。
    /// 開く向きは openAngle の符号で決まり、誰が開けても同じ側へ開く。
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
        [SerializeField, Tooltip("柱・鴨居・照準用トリガーをまとめた子。1x1 単位で作ってあり、Fit で (幅, 高さ, 1) にスケールする。")]
        private Transform frame;

        [SerializeField, Tooltip("回す軸。開口の左端に置く。スケールしない。")]
        private Transform hinge;

        [SerializeField, Tooltip("扉板。Hinge の子。")]
        private Transform leaf;

        [SerializeField, Tooltip("開口の幅。ドア全体の幅に対する比（枠と同じ比）。")]
        private float openingWidth = 0.4f;

        [SerializeField, Tooltip("開口の高さ。ドア全体の高さに対する比（枠と同じ比）。")]
        private float openingHeight = 0.75f;

        [SerializeField, Tooltip("扉板の厚み（メートル）。枠より薄くして、閉めたときに枠と重ならないようにする。")]
        private float leafThickness = 0.08f;

        [SerializeField, Tooltip("扉板と枠の隙間（メートル）。")]
        private float leafGap = 0.02f;

        [SerializeField, Tooltip("開いたときの角度（度）。正なら裏（-z）側、負なら表（+z）側へ開く。")]
        private float openAngle = 100f;

        [SerializeField, Tooltip("開閉の速さ（度/秒）。")]
        private float swingSpeed = 300f;

        [SerializeField, Tooltip("起動時に開けておく。")]
        private bool startOpen;

        [SerializeField] private string openLabel = "開ける";
        [SerializeField] private string closeLabel = "閉める";

        // 閉じたときに扉板の位置に誰かいないか調べる用。自分の枠や扉板も拾うので少し余裕を持たせる。
        private readonly Collider[] overlaps = new Collider[8];

        private Collider leafCollider;
        private float currentAngle;
        private float targetAngle;

        /// <summary>開いているか（開きかけも含む）。</summary>
        public bool IsOpen => !Mathf.Approximately(targetAngle, 0f);

        public string PromptLabel => IsOpen ? closeLabel : openLabel;

        private void Awake()
        {
            if (leaf != null) leafCollider = leaf.GetComponent<Collider>();
        }

        private void Start()
        {
            targetAngle = startOpen ? openAngle : 0f;
            currentAngle = targetAngle;
            ApplyAngle();
            UpdateLeafCollider();
        }

        private void Update()
        {
            if (!Mathf.Approximately(currentAngle, targetAngle))
            {
                currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
                ApplyAngle();
            }

            UpdateLeafCollider();
        }

        public bool CanInteract(GameObject interactor) => hinge != null;

        public void Interact(GameObject interactor)
        {
            targetAngle = IsOpen ? 0f : openAngle;
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

        private void ApplyAngle()
        {
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        }

        /// <summary>
        /// 扉板の当たり判定を状態に合わせる。閉じきっていなければ切る。
        /// 閉じきっても、開口に誰か立っているうちは入れない（閉じ込めて押し出せなくなるので）。いなくなったら入れる。
        /// </summary>
        private void UpdateLeafCollider()
        {
            if (leafCollider == null) return;

            if (!IsLeafSolid(currentAngle, targetAngle))
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

        /// <summary>扉板に当たり判定を持たせてよいか。閉じる途中や開いている間は持たせない。</summary>
        public static bool IsLeafSolid(float currentAngle, float targetAngle)
        {
            return Mathf.Approximately(targetAngle, 0f) && Mathf.Approximately(currentAngle, 0f);
        }
    }
}
