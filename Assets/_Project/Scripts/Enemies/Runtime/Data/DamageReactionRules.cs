using System;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 被弾でスタン・気絶が起きるかを決める。UnityEngine に依存しない。
    ///
    /// 相対ダメージ量（その一撃 ÷ 最大 HP）に係数を掛けたものが確率になる（1 で頭打ち）。
    /// 先に気絶を振り、外れたらスタンを振る。その一撃で倒れるなら何も起こさない。
    /// </summary>
    public readonly struct DamageReactionRules
    {
        /// <summary>相対ダメージ量 1 あたりのスタン確率。2 なら最大 HP の半分を削る一撃で必ずスタンする。</summary>
        public readonly float StunPerRelativeDamage;

        /// <summary>相対ダメージ量 1 あたりの気絶確率。0.5 なら最大 HP の 1 割で 5%。</summary>
        public readonly float FaintPerRelativeDamage;

        public DamageReactionRules(float stunPerRelativeDamage, float faintPerRelativeDamage)
        {
            StunPerRelativeDamage = Math.Max(0f, stunPerRelativeDamage);
            FaintPerRelativeDamage = Math.Max(0f, faintPerRelativeDamage);
        }

        /// <summary>相対ダメージ量（0〜1）。</summary>
        public static float RelativeDamage(int damage, int maxHp)
        {
            if (damage <= 0 || maxHp <= 0) return 0f;
            return Math.Min(1f, (float)damage / maxHp);
        }

        public float StunChance(int damage, int maxHp) =>
            Math.Min(1f, RelativeDamage(damage, maxHp) * StunPerRelativeDamage);

        public float FaintChance(int damage, int maxHp) =>
            Math.Min(1f, RelativeDamage(damage, maxHp) * FaintPerRelativeDamage);

        /// <summary>
        /// 起きる状態異常を決める。faintRoll と stunRoll は 0 以上 1 未満の一様乱数（別々に振ったもの）。
        /// </summary>
        public DamageReaction Roll(int damage, int maxHp, bool killed, float faintRoll, float stunRoll)
        {
            if (killed || damage <= 0) return DamageReaction.None;
            if (faintRoll < FaintChance(damage, maxHp)) return DamageReaction.Faint;
            if (stunRoll < StunChance(damage, maxHp)) return DamageReaction.Stun;
            return DamageReaction.None;
        }
    }
}
