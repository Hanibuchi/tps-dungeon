using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// インタラクトで開け閉めする扉。開口の片端にある Hinge を Y 軸で回し、その子の Leaf（扉板）を振る。
    /// 開けるときは触った相手と反対側へ開くので、自分に扉がぶつからない。
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

        [SerializeField, Tooltip("開いたときの角度（度）。")]
        private float openAngle = 100f;

        [SerializeField, Tooltip("開閉の速さ（度/秒）。")]
        private float swingSpeed = 300f;

        [SerializeField, Tooltip("起動時に開けておく。")]
        private bool startOpen;

        [SerializeField] private string openLabel = "開ける";
        [SerializeField] private string closeLabel = "閉める";

        private float currentAngle;
        private float targetAngle;

        /// <summary>開いているか（開きかけも含む）。</summary>
        public bool IsOpen => !Mathf.Approximately(targetAngle, 0f);

        public string PromptLabel => IsOpen ? closeLabel : openLabel;

        private void Start()
        {
            targetAngle = startOpen ? openAngle : 0f;
            currentAngle = targetAngle;
            ApplyAngle();
        }

        private void Update()
        {
            if (Mathf.Approximately(currentAngle, targetAngle)) return;

            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
            ApplyAngle();
        }

        public bool CanInteract(GameObject interactor) => hinge != null;

        public void Interact(GameObject interactor)
        {
            targetAngle = IsOpen
                ? 0f
                : OpenAngleAwayFrom(transform.position, transform.forward, interactor.transform.position, openAngle);
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
        /// 相手と反対側へ開くための Hinge の角度。
        /// 扉板は Hinge から +x に伸びているので、+Y 回転で -z（ドアの裏）側、-Y 回転で +z（表）側へ振れる。
        /// 相手が表側（forward 側）にいれば裏へ、裏側にいれば表へ開く。
        /// </summary>
        public static float OpenAngleAwayFrom(Vector3 doorPosition, Vector3 doorForward, Vector3 interactorPosition, float openAngle)
        {
            float side = Vector3.Dot(doorForward, interactorPosition - doorPosition);
            return side >= 0f ? openAngle : -openAngle;
        }
    }
}
