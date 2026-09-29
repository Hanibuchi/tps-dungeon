using TpsDungeon.Items;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 手に持った武器の見た目に付け、武器種の持ち位置（WeaponTypeDefinition.HeldLocalPosition / HeldLocalEuler）を当てる。
    /// 武器種の値が変わったら当て直すので、Play 中にインスペクタで数値をいじるとその場で手の武器が動く。
    /// 逆向き（Scene ビューで手の武器を動かしたら武器種に書き戻す）はエディタの HeldWeaponGripSync がやる。
    /// MeleeAttacker が持ち替えのたびに付ける。シーンに置く物ではない。
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class HeldWeaponGrip : MonoBehaviour
    {
        private WeaponTypeDefinition type;
        private Vector3 appliedPosition;
        private Vector3 appliedEuler;

        /// <summary>持ち位置を持っている武器種。無ければ null（原点に置く）。</summary>
        public WeaponTypeDefinition Type => type;

        /// <summary>最後に当てた位置と向き。transform がここから動いていたら、誰かが手で動かした。</summary>
        public Vector3 AppliedPosition => appliedPosition;
        public Vector3 AppliedEuler => appliedEuler;

        public void Init(WeaponTypeDefinition weaponType)
        {
            type = weaponType;
            Apply();
        }

        /// <summary>武器種の今の値を transform に当てる。</summary>
        public void Apply()
        {
            appliedPosition = type != null ? type.HeldLocalPosition : Vector3.zero;
            appliedEuler = type != null ? type.HeldLocalEuler : Vector3.zero;
            transform.SetLocalPositionAndRotation(appliedPosition, Quaternion.Euler(appliedEuler));
        }

        private void LateUpdate()
        {
            if (type == null) return;
            if (type.HeldLocalPosition != appliedPosition || type.HeldLocalEuler != appliedEuler) Apply();
        }
    }
}
