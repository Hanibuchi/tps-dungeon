namespace TpsDungeon.Items
{
    /// <summary>
    /// 近接コンボ 1 段の当て方。値はアセットに焼き込まれるので並べ替えず、足すときは末尾に。
    /// </summary>
    public enum MeleeStepMotion
    {
        /// <summary>判定の瞬間に、前方の箱の中の敵へ当てる（片手・両手・素手）。</summary>
        Swing = 0,

        /// <summary>判定の瞬間から前へ走り、走っている間ずっと前方の箱で当てる（ダッシュ突き）。</summary>
        Lunge = 1,

        /// <summary>判定の瞬間に、前方の着弾点を中心とした円の中の敵へ当てる（叩きつけ）。数で増える叩きつけと、追撃はここから出る。</summary>
        Slam = 2,
    }
}
