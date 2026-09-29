using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// パーティ全員に掛かる補正（経験値倍率・MaxHP・基礎攻撃力・クリティカル・ドロップ）。
    /// 永続アップグレードのタスクがここに値を入れる。ほかの補正が要るようになったらここに足す。
    /// クリティカルとドロップは武器・敵の側が計算のときに読む（ここでは値を持つだけ）。
    /// </summary>
    public sealed class ProgressionModifiers
    {
        public float ExpMultiplier { get; private set; } = 1f;
        public StatModifier MaxHp { get; private set; }
        public StatModifier BaseAttack { get; private set; }

        /// <summary>クリティカル率（0〜1）への補正。武器の率に掛かる。</summary>
        public StatModifier CritChance { get; private set; }

        /// <summary>クリティカル倍率への補正。武器の倍率に掛かる。</summary>
        public StatModifier CritMultiplier { get; private set; }

        /// <summary>武器のドロップ率への補正。</summary>
        public StatModifier DropRate { get; private set; }

        /// <summary>ユニーク武器のドロップ率への補正。</summary>
        public StatModifier UniqueDropRate { get; private set; }

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

        public void SetCritChance(StatModifier value)
        {
            if (value.Equals(CritChance)) return;
            CritChance = value;
            Changed?.Invoke(this);
        }

        public void SetCritMultiplier(StatModifier value)
        {
            if (value.Equals(CritMultiplier)) return;
            CritMultiplier = value;
            Changed?.Invoke(this);
        }

        public void SetDropRate(StatModifier value)
        {
            if (value.Equals(DropRate)) return;
            DropRate = value;
            Changed?.Invoke(this);
        }

        public void SetUniqueDropRate(StatModifier value)
        {
            if (value.Equals(UniqueDropRate)) return;
            UniqueDropRate = value;
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
