using Unity.Cinemachine;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 左手に持つ弓（武器種の heldInLeftHand）を構えている間、カメラを左肩の後ろへ寄せる。
    /// 弓は体の正面・左手に持つので、いつもの右肩寄りのカメラだと体と頭に隠れて、弦を引き絞っているのが見えない。
    /// 追従カメラ（CinemachineThirdPersonFollow）の CameraSide を、持ち替えに合わせて少しずつ動かす。弓を手放したら元の値へ戻す。
    /// カメラはシーン側にあり、フロアでは後から出ることもあるので、見つかるまでときどき探す。
    /// プレイヤーのルート（RangedAttacker と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedAttacker))]
    [AddComponentMenu("TPS Dungeon/Ranged Camera Shoulder")]
    public sealed class RangedCameraShoulder : MonoBehaviour
    {
        /// <summary>カメラが無いシーンで毎フレーム探さないための間隔（秒）。</summary>
        private const float SearchInterval = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("弓を構えている間の CameraSide（0 で左肩、1 で右肩）。")]
        private float bowCameraSide;

        [SerializeField, Min(0.01f), Tooltip("CameraSide を動かす速さ（1 秒あたり）。")]
        private float blendSpeed = 4f;

        private RangedAttacker ranged;
        private CinemachineThirdPersonFollow follow;
        private float normalSide;
        private float nextSearchTime;

        private void Awake() => ranged = GetComponent<RangedAttacker>();

        private void OnDisable()
        {
            if (follow != null) follow.CameraSide = normalSide;
            follow = null;
        }

        private void LateUpdate()
        {
            if (follow == null && !TryFindFollow()) return;

            float target = HoldingLeftHandBow ? bowCameraSide : normalSide;
            follow.CameraSide = Mathf.MoveTowards(follow.CameraSide, target, blendSpeed * Time.deltaTime);
        }

        /// <summary>このキャラを追っている追従カメラを探す。見つからなければ、シーンにあるものを使う。</summary>
        private bool TryFindFollow()
        {
            if (Time.unscaledTime < nextSearchTime) return false;
            nextSearchTime = Time.unscaledTime + SearchInterval;

            CinemachineThirdPersonFollow found = null;
            foreach (CinemachineThirdPersonFollow candidate in FindObjectsByType<CinemachineThirdPersonFollow>(FindObjectsSortMode.None))
            {
                var cam = candidate.GetComponent<CinemachineCamera>();
                if (cam == null || cam.Follow == null || !cam.Follow.IsChildOf(transform)) continue;

                found = candidate;
                break;
            }

            if (found == null) found = FindAnyObjectByType<CinemachineThirdPersonFollow>();
            if (found == null) return false;

            follow = found;
            normalSide = follow.CameraSide;
            return true;
        }

        /// <summary>今持っている遠距離武器が、左手に持つ弓か。</summary>
        private bool HoldingLeftHandBow =>
            ranged.isActiveAndEnabled && ranged.HeldWeapon != null && ranged.HeldWeapon.WeaponType != null
            && ranged.HeldWeapon.WeaponType.HeldInLeftHand;
    }
}
