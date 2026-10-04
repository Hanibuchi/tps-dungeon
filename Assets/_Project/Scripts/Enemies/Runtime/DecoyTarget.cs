using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵が主人公より先に狙う的（召喚した置物など）。有効な間だけ <see cref="All"/> に載り、体力を持つ。
    /// 敵の AI と攻撃はまだ無いので、今は誰も読まない。敵が狙う相手を選ぶときに <see cref="All"/> から近い物を探し、
    /// 攻撃が当たったら <see cref="TakeDamage"/> を呼ぶ想定。体力が尽きたら <see cref="Broken"/> を知らせる（消すのは持ち主）。
    /// 召喚した置物（Combat の SummonedDecoy）が付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DecoyTarget : MonoBehaviour
    {
        private static readonly List<DecoyTarget> all = new List<DecoyTarget>();

        private int maxHp = 1;
        private int currentHp = 1;

        /// <summary>今居る的。</summary>
        public static IReadOnlyList<DecoyTarget> All => all;

        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public bool IsBroken => currentHp <= 0;

        /// <summary>体力が尽きた。1 回だけ。</summary>
        public event Action<DecoyTarget> Broken;

        /// <summary>体力を value（最低 1）にして満タンにする。</summary>
        public void SetMaxHp(int value)
        {
            maxHp = Mathf.Max(1, value);
            currentHp = maxHp;
        }

        /// <summary>amount だけ体力を減らす。実際に減った量を返す。尽きたあとや 0 以下の量では何もしない。</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0 || IsBroken) return 0;

            int dealt = Mathf.Min(amount, currentHp);
            currentHp -= dealt;
            if (currentHp <= 0) Broken?.Invoke(this);
            return dealt;
        }

        private void OnEnable() => all.Add(this);

        private void OnDisable() => all.Remove(this);
    }
}
