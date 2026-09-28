using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// パーティ全員に掛かる成長の補正（経験値倍率・MaxHP・基礎攻撃力）。
    /// 永続アップグレードのタスクがここに値を入れる。ほかの補正が要るようになったらここに足す。
    /// </summary>
    public sealed class ProgressionModifiers
    {
        public float ExpMultiplier { get; private set; } = 1f;
        public StatModifier MaxHp { get; private set; }
        public StatModifier BaseAttack { get; private set; }

        /// <summary>どれかの補正が変わった。メンバーはこれを受けてステータスを出し直す。</summary>
        public event Action<ProgressionModifiers> Changed;

        /// <summary>経験値倍率。負の値は 0 として扱う。</summary>
        public void SetExpMultiplier(float value)
        {
            value = Math.Max(0f, value);
            if (value.Equals(ExpMultiplier)) return;
            ExpMultiplier = value;
            Changed?.Invoke(this);
        }

        public void SetMaxHp(StatModifier value)
        {
            if (value.Equals(MaxHp)) return;
            MaxHp = value;
            Changed?.Invoke(this);
        }

        public void SetBaseAttack(StatModifier value)
        {
            if (value.Equals(BaseAttack)) return;
            BaseAttack = value;
            Changed?.Invoke(this);
        }

        /// <summary>倍率を掛けた獲得量。四捨五入し、倍率が正なら最低 1 は入る。</summary>
        public int ApplyExp(int amount)
        {
            if (amount <= 0 || ExpMultiplier <= 0f) return 0;
            double value = Math.Round(amount * (double)ExpMultiplier, MidpointRounding.AwayFromZero);
            if (value >= int.MaxValue) return int.MaxValue;
            return Math.Max(1, (int)value);
        }
    }
}
