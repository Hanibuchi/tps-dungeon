using System;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 被弾でスタン・気絶が起きるかを決める。UnityEngine に依存しない。
    ///
    /// 相対ダメージ量（その一撃 ÷ 最大 HP）で決める。
    ///   気絶 … 相対ダメージ量に係数を掛けたものが確率（1 で頭打ち）
    ///   スタン … 相対ダメージ量がしきい値以上なら必ず
    /// 先に気絶を振り、外れたらスタンを見る。その一撃で倒れるなら何も起こさない。
    /// scale は攻撃側の倍率（スタンのエンチャント）で、相対ダメージ量に掛けてから判定する。
    /// </summary>
    public readonly struct DamageReactionRules
    {
        /// <summary>これ以上の相対ダメージ量の一撃で必ずスタンする。0.1 なら最大 HP の 1 割以上。1 を超えるとスタンしない。</summary>
        public readonly float StunThreshold;

        /// <summary>相対ダメージ量 1 あたりの気絶確率。0.5 なら最大 HP の 1 割で 5%。</summary>
        public readonly float FaintPerRelativeDamage;

        public DamageReactionRules(float stunThreshold, float faintPerRelativeDamage)
        {
            StunThreshold = Math.Max(0f, stunThreshold);
            FaintPerRelativeDamage = Math.Max(0f, faintPerRelativeDamage);
        }

        /// <summary>相対ダメージ量（0〜1）。scale を掛けてから 1 で頭打ちにする。</summary>
        public static float RelativeDamage(int damage, int maxHp, float scale = 1f)
        {
            if (damage <= 0 || maxHp <= 0 || scale <= 0f) return 0f;
            return Math.Min(1f, (float)damage / maxHp * scale);
        }

        public bool Stuns(int damage, int maxHp, float scale = 1f) =>
            damage > 0 && RelativeDamage(damage, maxHp, scale) >= StunThreshold;

        public float FaintChance(int damage, int maxHp, float scale = 1f) =>
            Math.Min(1f, RelativeDamage(damage, maxHp, scale) * FaintPerRelativeDamage);

        /// <summary>起きる状態異常を決める。faintRoll は 0 以上 1 未満の一様乱数。</summary>
        public DamageReaction Roll(int damage, int maxHp, bool killed, float faintRoll, float scale = 1f)
        {
            if (killed || damage <= 0) return DamageReaction.None;
            if (faintRoll < FaintChance(damage, maxHp, scale)) return DamageReaction.Faint;
            if (Stuns(damage, maxHp, scale)) return DamageReaction.Stun;
            return DamageReaction.None;
        }
    }
}
