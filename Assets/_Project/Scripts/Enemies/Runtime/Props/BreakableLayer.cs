using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 攻撃で壊せる置物（草花など）を置くレイヤー。物理ではどのレイヤーとも当たらない（歩いてすり抜けられる）が、
    /// 攻撃の重なり判定（Overlap・Raycast）には映るので当てられる。インタラクトの照準や NavMesh の焼き込みからは外す。
    /// </summary>
    public static class BreakableLayer
    {
        public const string Name = "Breakable";

        /// <summary>レイヤーの番号。プロジェクトに無ければ -1。</summary>
        public static int Index => LayerMask.NameToLayer(Name);

        /// <summary>mask から壊せる置物のレイヤーを外す。</summary>
        public static int Exclude(int mask)
        {
            int index = Index;
            return index >= 0 ? mask & ~(1 << index) : mask;
        }
    }
}
