using System;

namespace TpsDungeon.Enemies
{
    /// <summary>遠距離の敵が今どう動くか（<see cref="EnemyAttackRules.Stance"/>）。</summary>
    public enum RangedStance
    {
        /// <summary>見えていないか遠いので寄る。</summary>
        Approach,
        /// <summary>止まって撃つ。</summary>
        Hold,
        /// <summary>近すぎるので下がる（見えていれば撃ってもよい）。</summary>
        Retreat,
    }

    /// <summary>
    /// 敵の攻撃の数の決まり。UnityEngine に依存しない。
    ///   飛びつき … 放物線の高さと、飛ぶ時間
    ///   自爆     … 爆心からの距離で減るダメージ
    ///   遠距離   … 距離と見えているかから、寄る・止まる・下がるを決める
    /// </summary>
    public static class EnemyAttackRules
    {
        /// <summary>飛びつきの進み具合 t（0〜1）での、まっすぐ結んだ線からの高さ。t = 0.5 で apex、両端で 0。</summary>
        public static float LeapHeight(float t, float apex)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return 4f * apex * t * (1f - t);
        }

        /// <summary>水平に distance 飛ぶのにかける時間（秒）。speed（m/秒）で割り、[minTime, maxTime] に収める。</summary>
        public static float LeapDuration(float distance, float speed, float minTime, float maxTime)
        {
            float time = speed > 0f ? Math.Max(0f, distance) / speed : maxTime;
            return Math.Max(minTime, Math.Min(maxTime, time));
        }

        /// <summary>
        /// 爆発のダメージ。爆心（distance = 0）で damage、半径の縁で damage × edgeScale まで直線で減り、半径の外は 0。
        /// 範囲内なら最低 1 は入る。
        /// </summary>
        public static int ExplosionDamage(int damage, float distance, float radius, float edgeScale)
        {
            if (damage <= 0 || radius <= 0f || distance > radius) return 0;

            float t = Math.Max(0f, distance) / radius;
            float scale = 1f + (Math.Max(0f, Math.Min(1f, edgeScale)) - 1f) * t;
            return Math.Max(1, (int)Math.Round(damage * scale, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// 遠距離の敵の立ち位置。見えていなければ寄る（経路で回り込めば見える所へ出る）。
        /// 見えていて minRange より近ければ下がり、maxRange より遠ければ寄り、間なら止まる。
        /// </summary>
        public static RangedStance Stance(float distance, bool visible, float minRange, float maxRange)
        {
            if (!visible) return RangedStance.Approach;
            if (distance < minRange) return RangedStance.Retreat;
            if (distance > maxRange) return RangedStance.Approach;
            return RangedStance.Hold;
        }
    }
}
