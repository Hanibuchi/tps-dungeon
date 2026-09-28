using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 倒したときにパーティがもらえる経験値。敵のプレハブのルートに付ける（MonsterBuilder が付ける）。
    /// 倒れたときに EnemyDeath が <see cref="Grant()"/> を呼ぶ。
    /// 経験値倍率は PartyProgression 側で掛かる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Exp Reward")]
    public sealed class EnemyExpReward : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("倒したときの経験値（倍率を掛ける前）。値は仮。")]
        private int exp = 5;

        private bool granted;

        public int Exp => exp;

        /// <summary>もう配ったか。同じ敵で 2 回もらわないため。</summary>
        public bool Granted => granted;

        /// <summary>今のパーティに経験値を配る。配った量（倍率込み）を返す。2 回目以降は 0。</summary>
        public int Grant() => Grant(PartyProgression.Current);

        public int Grant(PartyProgression party)
        {
            if (granted || party == null) return 0;

            granted = true;
            return party.GrantExp(exp);
        }

        /// <summary>使い回す（プール）ときに、もう一度もらえるようにする。</summary>
        public void ResetGrant()
        {
            granted = false;
        }
    }
}
