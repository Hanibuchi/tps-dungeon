namespace TpsDungeon.Player
{
    /// <summary>
    /// 加護（武器種 15 ダメージ軽減の杖で張る膜）の種類。杖ごとに 1 つで、膜の色で見分ける。
    /// 値はアセットに焼き込まれるので並べ替えず、足すときは末尾に。
    /// </summary>
    public enum BlessingKind
    {
        /// <summary>受けるダメージを減らす（守護の杖・不壊の聖杖。青い膜）。</summary>
        DamageReduction = 0,

        /// <summary>クリティカル倍率を上げる（血走りの杖。桃色の膜）。</summary>
        CritMultiplier = 1,

        /// <summary>状態異常にかかりにくくする（聖印の錫杖。黄色い膜）。値だけで、読む側は仲間の状態異常ができたときに繋ぐ。</summary>
        AilmentResistance = 2,
    }
}
