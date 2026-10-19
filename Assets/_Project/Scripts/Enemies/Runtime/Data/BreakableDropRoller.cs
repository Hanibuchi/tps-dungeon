using System;
using System.Collections.Generic;

namespace TpsDungeon.Enemies
{
    /// <summary>壊せる箱の種類。Breakables.csv の kind 列（小文字）で書く。値はアセットに焼き込まれるので並べ替えないこと。</summary>
    public enum BreakableDropKind
    {
        Crate = 0,
        Chest = 1,
    }

    /// <summary>箱を壊したときに出るもの。</summary>
    public enum BreakableOutcome
    {
        Nothing = 0,
        Enemy = 1,
        Weapon = 2,
    }

    /// <summary>
    /// 箱を壊したときの抽選。UnityEngine に依存しない。
    /// 敵・武器・何も出ないの 3 つの重みを合計で割って 1 つ選び、敵ならその層の敵の重みで、武器ならランクの重みでさらに 1 つ選ぶ。
    /// 負の重みは 0 とみなす。全部 0 なら何も出ない。
    /// </summary>
    public static class BreakableDropRoller
    {
        /// <summary>ランクの数（E・D・C・B・A・S。WeaponRank の 0〜5）。</summary>
        public const int RankCount = 6;

        /// <summary>enemy・weapon・nothing の重みから、roll（0 以上 1 未満）で出るものを選ぶ。</summary>
        public static BreakableOutcome RollOutcome(float enemy, float weapon, float nothing, double roll)
        {
            switch (PickWeighted(new[] { enemy, weapon, nothing }, roll))
            {
                case 0: return BreakableOutcome.Enemy;
                case 1: return BreakableOutcome.Weapon;
                default: return BreakableOutcome.Nothing;
            }
        }

        /// <summary>重みの並びから、roll（0 以上 1 未満）で当たった番号を返す。重みが全部 0 以下なら -1。</summary>
        public static int PickWeighted(IReadOnlyList<float> weights, double roll)
        {
            double total = 0d;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0d) return -1;

            double target = roll * total;
            double cumulative = 0d;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                float weight = Math.Max(0f, weights[i]);
                if (weight <= 0f) continue;
                cumulative += weight;
                last = i;
                if (target < cumulative) return i;
            }
            // roll が 1 に限りなく近いときの丸めの誤差。
            return last;
        }
    }
}
