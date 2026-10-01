namespace TpsDungeon.Items
{
    /// <summary>
    /// 遠距離の武器種の撃ち方。値はアセットに焼き込まれるので並べ替えず、足すときは末尾に。
    /// </summary>
    public enum RangedAttackKind
    {
        /// <summary>遠距離の武器ではない（近接や素手）。</summary>
        None = 0,

        /// <summary>照準の先へ矢をまっすぐ放つ（弓）。数で扇に増え、多重で遅れてもう一斉射。</summary>
        Bow = 1,

        /// <summary>照準が当たった地面へ上から矢を降らせ、その範囲に刻みでダメージを与え続ける（持続弓）。</summary>
        Rain = 2,
    }
}
