using TpsDungeon.Items;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 手に持った武器の見た目に付け、武器種の持ち位置（WeaponTypeDefinition.GripPosition / GripEuler。持つ手で変わる）を当てる。
    /// 武器種の値が変わったら当て直すので、Play 中にインスペクタで数値をいじるとその場で手の武器が動く。
    /// 逆向き（Scene ビューで手の武器を動かしたら武器種に書き戻す）はエディタの HeldWeaponGripSync がやる。
    /// MeleeAttacker（手の物）と HeldShieldView（オフハンドの盾）が付ける。シーンに置く物ではない。
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class HeldWeaponGrip : MonoBehaviour
    {
        private WeaponTypeDefinition type;
        private bool leftHand;
        private bool offHand;
        private Vector3 appliedPosition;
        private Vector3 appliedEuler;

        /// <summary>持ち位置を持っている武器種。無ければ null（原点に置く）。</summary>
        public WeaponTypeDefinition Type => type;

        /// <summary>左手に持っているか。</summary>
        public bool LeftHand => leftHand;

        /// <summary>オフハンドに持っている盾か（HeldShieldView が付けた）。偽ならメインハンドの手の物（MeleeAttacker が付けた）。</summary>
        public bool OffHand => offHand;

        /// <summary>武器種のいつもの手と逆の手に持っているか（書き戻す先が otherHand〜になる）。</summary>
        public bool OtherHand => type != null && leftHand != type.HeldInLeftHand;

        /// <summary>最後に当てた位置と向き。transform がここから動いていたら、誰かが手で動かした。</summary>
        public Vector3 AppliedPosition => appliedPosition;
        public Vector3 AppliedEuler => appliedEuler;

        public void Init(WeaponTypeDefinition weaponType, bool inLeftHand, bool isOffHand = false)
        {
            type = weaponType;
            leftHand = inLeftHand;
            offHand = isOffHand;
            Apply();
        }

        /// <summary>武器種の今の値を transform に当てる。</summary>
        public void Apply()
        {
            appliedPosition = type != null ? type.GripPosition(leftHand) : Vector3.zero;
            appliedEuler = type != null ? type.GripEuler(leftHand) : Vector3.zero;
            transform.SetLocalPositionAndRotation(appliedPosition, Quaternion.Euler(appliedEuler));
        }

        private void LateUpdate()
        {
            if (type == null) return;
            if (type.GripPosition(leftHand) != appliedPosition || type.GripEuler(leftHand) != appliedEuler) Apply();
        }
    }
}
