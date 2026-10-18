namespace TpsDungeon.Map.Data
{
    /// <summary>部屋の敵の予算の上限を、部屋の何に比例させるか（EnemySpawnLayers.csv の budget_basis）。</summary>
    public enum RoomBudgetBasis
    {
        /// <summary>セル数（1x1 なら 1、2x2 なら 4）。CSV では area。</summary>
        Area,

        /// <summary>辺の平均 (幅 + 奥行き) ÷ 2（1x1 なら 1、2x2 なら 2、1x3 なら 2）。CSV では side。</summary>
        MeanSide,
    }
}
