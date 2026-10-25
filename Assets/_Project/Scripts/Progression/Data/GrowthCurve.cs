using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// レベルから MaxHP・基礎攻撃力・必要経験値を出す式。値を持つだけの不変の型で、UnityEngine に依存しない。
    ///
    ///   MaxHP(L)       = round(HpBase + HpCoefficient * (L - 1)^HpExponent)
    ///   基礎攻撃力(L)   = AttackBase + AttackCoefficient * (L - 1)^AttackExponent
    ///
    /// 必要経験値と最大レベルはキャラごとに変えず、<see cref="LevelBalance"/> の式と定数を使う。
    /// </summary>
    public readonly struct GrowthCurve
    {
        public const int MinLevel = LevelBalance.MinLevel;

        /// <summary>仮の初期値。CharacterGrowthProfile の既定値もこれに揃えてある。</summary>
        public static readonly GrowthCurve Default = new GrowthCurve(
            hpBase: 100, hpCoefficient: 2.6, hpExponent: 1.1,
            attackBase: 10, attackCoefficient: 0.162, attackExponent: 1.05);

        public GrowthCurve(double hpBase, double hpCoefficient, double hpExponent,
            double attackBase, double attackCoefficient, double attackExponent)
        {
            HpBase = hpBase;
            HpCoefficient = hpCoefficient;
            HpExponent = hpExponent;
            AttackBase = attackBase;
            AttackCoefficient = attackCoefficient;
            AttackExponent = attackExponent;
        }

        public double HpBase { get; }
        public double HpCoefficient { get; }
        public double HpExponent { get; }
        public double AttackBase { get; }
        public double AttackCoefficient { get; }
        public double AttackExponent { get; }
        public int MaxLevel => LevelBalance.MaxLevel;

        public int ClampLevel(int level) => Math.Min(Math.Max(level, MinLevel), MaxLevel);

        /// <summary>そのレベルの MaxHP（補正前）。1 を下回らない。</summary>
        public int MaxHp(int level)
        {
            level = ClampLevel(level);
            double value = HpBase + HpCoefficient * Math.Pow(level - 1, HpExponent);
            return Math.Max(1, RoundToInt(value));
        }

        /// <summary>そのレベルの基礎攻撃力（補正前）。倍率の補正で端数が意味を持つので丸めない。</summary>
        public float BaseAttack(int level)
        {
            level = ClampLevel(level);
            return (float)(AttackBase + AttackCoefficient * Math.Pow(level - 1, AttackExponent));
        }

        /// <summary>level から level+1 に上がるのに要る経験値（<see cref="LevelBalance.RequiredExp"/>）。最大レベルでは 0。</summary>
        public int ExpToNext(int level) => LevelBalance.RequiredExp(level);

        /// <summary>Lv1・経験値 0 から level に到達するまでに要る経験値の合計。</summary>
        public long TotalExpToReach(int level) => LevelBalance.TotalExpToReach(level);

        private static int RoundToInt(double value)
        {
            double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded >= int.MaxValue) return int.MaxValue;
            if (rounded <= int.MinValue) return int.MinValue;
            return (int)rounded;
        }
    }
}
