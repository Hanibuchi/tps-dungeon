using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// パーティーのメンバー（先頭も後ろの仲間も）を入れるレイヤー。
    /// 攻撃の当たり・照準・突きの壁の判定はこのレイヤーを見ない（味方に矢が刺さらない、味方で照準や突きが止まらない）。
    /// メンバーどうしは物理で当たらない（Project Settings の衝突マトリクス）ので、先頭が列に詰まらない。
    /// レイヤーが無い（Project Settings に名前が無い）ときは何も外さない。
    /// </summary>
    public static class AllyLayer
    {
        public const string Name = "Ally";

        /// <summary>レイヤー番号。無ければ -1。</summary>
        public static int Index => LayerMask.NameToLayer(Name);

        /// <summary>このレイヤーだけのマスク。無ければ 0。</summary>
        public static int Mask
        {
            get
            {
                int index = Index;
                return index >= 0 ? 1 << index : 0;
            }
        }

        /// <summary>mask から味方のレイヤーを外したもの。</summary>
        public static int Exclude(int mask) => mask & ~Mask;
    }
}
