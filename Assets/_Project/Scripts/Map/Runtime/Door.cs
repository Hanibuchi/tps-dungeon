using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// インタラクトで開け閉めする扉。開口の片端にある Hinge を Y 軸で回し、その子の Leaf（扉板）を振る。
    /// 開けるときは触った相手と反対側へ開くので、自分に扉がぶつからない。
    ///
    /// FloorBuilder はドアのルートを (cellSize, wallHeight, 1) に非一様スケールする。
    /// そのまま子を回すと扉板が歪むので、Start で Hinge にスケールの逆数を掛けて世界スケールを 1 に戻し、
    /// 扉板の大きさはメートルで入れ直す。開口の寸法はルートの 1x1 単位（枠を作る生成器と同じ比）で持つ。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Door")]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("回す軸。開口の左端に置く。")]
        private Transform hinge;

        [SerializeField, Tooltip("扉板。Hinge の子。")]
        private Transform leaf;

        [SerializeField, Tooltip("開口の幅。ルートの x スケールに対する比。")]
        private float openingWidth = 0.4f;

        [SerializeField, Tooltip("開口の高さ。ルートの y スケールに対する比。")]
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
            // FloorBuilder は Instantiate の後でスケールを入れるので、Awake ではなくここで合わせる。
            FitLeafToOpening();

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
        /// ルートの今のスケールに合わせて Hinge と扉板を置き直す。
        /// 生成器がプレハブを作るとき（スケール 1）にも呼んで、エディタ上の見た目を揃える。
        /// </summary>
        public void FitLeafToOpening()
        {
            if (hinge == null || leaf == null) return;

            Vector3 scale = transform.lossyScale;
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f)) return;

            hinge.localPosition = new Vector3(-openingWidth * 0.5f, 0f, 0f);
            hinge.localRotation = Quaternion.identity;
            hinge.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            // ここから下は Hinge の中＝メートル単位。
            float width = openingWidth * scale.x;
            float height = openingHeight * scale.y;
            leaf.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
            leaf.localRotation = Quaternion.identity;
            leaf.localScale = new Vector3(
                Mathf.Max(0.01f, width - leafGap * 2f),
                Mathf.Max(0.01f, height - leafGap),
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
