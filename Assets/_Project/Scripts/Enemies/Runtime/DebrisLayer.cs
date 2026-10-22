using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の死体（ラグドールの骨）と、敵が落とした武器の物理の箱を入れるレイヤー。
    /// 物理では床や壁（Default・Ignore Raycast）とだけ当たり、パーティー（Ally）・同じレイヤー・壊せる草花とは当たらない（歩いてすり抜けられる）。
    /// 攻撃の当たり・照準・見通しの判定からも外す（死体で弾が止まったり、下に落ちた物を拾えなくなったりしないように）。
    /// レイヤーと衝突の設定はメニュー「Tools/TPS Dungeon/敵/死体・落とし物のレイヤーを用意する」で作る。無ければ何も外さない。
    /// </summary>
    public static class DebrisLayer
    {
        public const string Name = "Debris";

        /// <summary>レイヤーの番号。プロジェクトに無ければ -1。</summary>
        public static int Index => LayerMask.NameToLayer(Name);

        /// <summary>mask から死体・落とし物のレイヤーを外す。</summary>
        public static int Exclude(int mask)
        {
            int index = Index;
            return index >= 0 ? mask & ~(1 << index) : mask;
        }
    }
}
