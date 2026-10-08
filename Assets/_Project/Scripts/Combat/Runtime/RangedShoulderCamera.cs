using TpsDungeon.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 遠距離武器を持っている間だけ、TPS カメラ（CinemachineThirdPersonFollow）を右へ寄せる。
    /// 普段のカメラは体のほぼ真後ろ（右へ 0.2 m）にあり、右肩が照準のすぐ手前に来るので、手から照準へ飛ぶ弾の出だしが体に隠れる。
    /// 寄せる量は ShoulderOffset と CameraSide から出る横のずれ（m）で決め、CameraSide を書き換えてなめらかに移す。
    /// 持ち替えて遠距離武器でなくなったら、シーンに置いたときの CameraSide へ戻す。
    /// キャラのルート（RangedAttacker と同じ GameObject）に付ける。カメラはシーンから探す。
    /// パーティーでは全員が持つが、カメラを動かすのは操作しているキャラ（先頭）だけ。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedAttacker))]
    [AddComponentMenu("TPS Dungeon/Ranged Shoulder Camera")]
    public sealed class RangedShoulderCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("遠距離武器を持っている間の、カメラの右へのずれ（m）。体に隠れない程度に。")]
        private float rangedSideOffset = 0.6f;

        [SerializeField, Min(0.01f), Tooltip("ずれを移す速さ（m/秒）。")]
        private float sideChangeSpeed = 3f;

        private RangedAttacker ranged;
        private CinemachineThirdPersonFollow follow;
        private float baseCameraSide;

        private void Awake() => ranged = GetComponent<RangedAttacker>();

        private void OnDisable()
        {
            if (follow != null && PartyRoster.IsLeader(gameObject)) follow.CameraSide = baseCameraSide;
        }

        private void LateUpdate()
        {
            if (!PartyRoster.IsLeader(gameObject)) return;
            if (follow == null && !FindFollow()) return;

            // CinemachineThirdPersonFollow は横のずれを Lerp(-ShoulderOffset.x, ShoulderOffset.x, CameraSide) で出す。
            float shoulder = follow.ShoulderOffset.x;
            if (Mathf.Approximately(shoulder, 0f)) return;

            float current = Mathf.Lerp(-shoulder, shoulder, follow.CameraSide);
            float target = ranged.HeldWeapon != null ? rangedSideOffset : Mathf.Lerp(-shoulder, shoulder, baseCameraSide);
            float next = Mathf.MoveTowards(current, target, sideChangeSpeed * Time.deltaTime);
            follow.CameraSide = Mathf.InverseLerp(-shoulder, shoulder, next);
        }

        private bool FindFollow()
        {
            follow = FindAnyObjectByType<CinemachineThirdPersonFollow>();
            if (follow == null) return false;

            baseCameraSide = follow.CameraSide;
            return true;
        }
    }
}
