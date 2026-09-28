namespace TpsDungeon.Enemies
{
    /// <summary>1 回の被弾で起きる状態異常。</summary>
    public enum DamageReaction
    {
        None,
        /// <summary>短いひるみ（0.5 秒ほど）。</summary>
        Stun,
        /// <summary>倒れ込む（3 秒ほど）。</summary>
        Faint,
    }
}
