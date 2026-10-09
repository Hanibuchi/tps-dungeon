using System.Collections.Generic;
using TpsDungeon.Items;

namespace TpsDungeon.Party
{
    /// <summary>仲間の手持ちの 1 枠を、武器の選び方から見たもの。</summary>
    public struct FollowerWeaponOption
    {
        /// <summary>近接の武器か。</summary>
        public bool IsMelee;

        /// <summary>遠距離の武器か。</summary>
        public bool IsRanged;

        /// <summary>届く距離（m）。近接なら振って当たる距離、遠距離なら狙える距離。</summary>
        public float Range;

        /// <summary>次に使える時刻（これ以前は待ち中）。</summary>
        public float ReadyAt;

        /// <summary>
        /// 今使ってよいか。治癒の場は誰も傷ついていなければ使わない、など武器の事情で外すときに偽。
        /// 攻撃に使える武器（近接・遠距離）でなければ見ない。
        /// </summary>
        public bool Usable;

        /// <summary>攻撃に使える武器か（お守り・盾・宝石・空き枠でない）。</summary>
        public bool IsAttack => IsMelee || IsRanged;

        /// <summary>武器を枠の中身から作る。お守り・盾は攻撃の候補にしない。</summary>
        public static FollowerWeaponOption From(ItemInstance item, float readyAt)
        {
            WeaponTypeDefinition type = item?.Weapon != null ? item.Weapon.WeaponType : null;
            if (type == null || type.IsPassiveGear) return default;

            return new FollowerWeaponOption
            {
                IsMelee = type.IsMelee,
                IsRanged = !type.IsMelee && type.IsRanged,
                Range = type.IsMelee ? MeleeReach(type) : RangedReach(type),
                ReadyAt = readyAt,
                Usable = true,
            };
        }

        /// <summary>1 段目の当たりの前の端までの距離。走る段は走る距離を足す。</summary>
        public static float MeleeReach(WeaponTypeDefinition type)
        {
            if (type == null || !type.IsMelee) return 0f;

            MeleeComboStep step = type.ComboSteps[0];
            float reach = step.hitboxCenter.z + step.hitboxSize.z * 0.5f;
            switch (step.motion)
            {
                case MeleeStepMotion.Lunge:
                    reach += step.lungeDistance;
                    break;
                case MeleeStepMotion.Slam:
                    reach = UnityEngine.Mathf.Max(reach, step.hitboxCenter.z + step.slamRadius);
                    break;
            }

            return UnityEngine.Mathf.Max(1f, reach);
        }

        /// <summary>狙える距離。炎は炎の届く距離、連鎖の雷は最初に飛ぶ距離、それ以外は照準の届く距離。</summary>
        public static float RangedReach(WeaponTypeDefinition type)
        {
            if (type == null || !type.IsRanged) return 0f;

            float reach = type.RangedKind switch
            {
                RangedAttackKind.Flame => type.FlameRange,
                RangedAttackKind.Chain => type.ChainRange,
                _ => type.AimMaxDistance,
            };
            return reach > 0f ? reach : 15f;
        }
    }

    /// <summary>
    /// 仲間が手持ちの 4 枠のどれで戦うかを決める規則。
    /// 1. 状況: 敵が近い（closeDistance 以内）なら近接、遠ければ遠距離を優先する。遠距離は届く距離の内の敵だけを狙える。
    /// 2. 使えるか: 優先する種類がどれも待ち中（か届かない）なら、もう一方の種類で使える物にする。
    ///    どちらも使えなければ、優先する種類で一番早く使える物を持って待つ。
    /// 3. 素手は、攻撃に使える武器が 1 つも無いときだけ（<see cref="Unarmed"/> を返す）。
    /// 同じ条件なら、今持っている枠 → 左の枠の順に選んで、無駄に持ち替えない。
    /// </summary>
    public static class FollowerWeaponPicker
    {
        /// <summary>攻撃に使える武器が無いので素手で殴る。</summary>
        public const int Unarmed = -1;

        public static int Pick(IReadOnlyList<FollowerWeaponOption> options, float distance, float closeDistance, float now, int current)
        {
            bool anyAttack = false;
            for (int i = 0; i < options.Count; i++) anyAttack |= options[i].IsAttack;
            if (!anyAttack) return Unarmed;

            bool preferMelee = distance <= closeDistance;

            int pick = Best(options, distance, now, current, preferMelee, readyOnly: true);
            if (pick >= 0) return pick;

            pick = Best(options, distance, now, current, !preferMelee, readyOnly: true);
            if (pick >= 0) return pick;

            pick = Best(options, distance, now, current, preferMelee, readyOnly: false);
            if (pick >= 0) return pick;

            pick = Best(options, distance, now, current, !preferMelee, readyOnly: false);
            if (pick >= 0) return pick;

            // 使ってよい武器が無い（治癒の場しか無く誰も傷ついていないなど）。攻撃の武器のどれかを持っておく。
            if (current >= 0 && current < options.Count && options[current].IsAttack) return current;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].IsAttack) return i;
            }

            return Unarmed;
        }

        private static int Best(IReadOnlyList<FollowerWeaponOption> options, float distance, float now, int current, bool melee, bool readyOnly)
        {
            int best = -1;
            float bestReady = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                FollowerWeaponOption option = options[i];
                if (!option.Usable || (melee ? !option.IsMelee : !option.IsRanged)) continue;
                // 遠距離は届かない敵を狙えない。近接は近づけばよいので距離で外さない。
                if (!melee && distance > option.Range) continue;

                bool ready = option.ReadyAt <= now;
                if (readyOnly && !ready) continue;

                float readyAt = ready ? now : option.ReadyAt;
                bool better = best < 0
                              || readyAt < bestReady - 1e-4f
                              || (readyAt <= bestReady + 1e-4f && i == current && best != current);
                if (!better) continue;

                best = i;
                bestReady = readyAt;
            }

            return best;
        }
    }
}
