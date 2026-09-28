namespace TpsDungeon.Progression
{
    /// <summary>レベルアップで MaxHP が増えたときの現在 HP の扱い。</summary>
    public enum LevelUpHpMode
    {
        /// <summary>MaxHP の増えた分だけ現在 HP も足す。</summary>
        AddIncrease,

        /// <summary>全回復する。</summary>
        FullHeal,
    }
}
