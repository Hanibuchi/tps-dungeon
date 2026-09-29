namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器のランク（レア度）。値はアセットに焼き込まれるので並べ替えないこと。
    /// 数値が大きいほどレア。
    /// </summary>
    public enum WeaponRank
    {
        E = 0,
        D = 1,
        C = 2,
        B = 3,
        A = 4,
        S = 5,
        Unique = 6,
    }

    public static class WeaponRanks
    {
        /// <summary>画面に出す名前。</summary>
        public static string Label(WeaponRank rank) => rank == WeaponRank.Unique ? "ユニーク" : rank.ToString();
    }
}
