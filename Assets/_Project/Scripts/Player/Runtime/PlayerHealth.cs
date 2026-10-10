using System;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// プレイヤーの体力。値を持って増減させ、変わったら知らせるだけ。
    /// 死亡やダメージ演出はまだ無く、HUD がこれを読んで表示する。
    /// 最大 HP はレベルに応じて CharacterProgression が <see cref="SetMaxAndCurrent"/> で書き込む（Inspector の値は初期値）。
    /// 受けるダメージの倍率（盾）と毎秒の回復（自然回復）は装備（PlayerGear）が書き込む。加護（CharacterBuffs）が付いていれば、その軽減も掛け合わせる。
    /// プレイヤーのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Health")]
    public sealed class PlayerHealth : MonoBehaviour, IHealthPool
    {
        [SerializeField, Min(1), Tooltip("最大 HP。")]
        private int maxHp = 100;

        [SerializeField, Min(0), Tooltip("開始時の HP。最大を超えた分は切り捨てる。")]
        private int currentHp = 100;

        private float regenCarry;
        private CharacterBuffs buffs;

        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;

        /// <summary>残りの割合（0〜1）。</summary>
        public float Fraction => maxHp > 0 ? (float)currentHp / maxHp : 0f;

        public bool IsDead => currentHp <= 0;

        /// <summary>受けるダメージに掛ける倍率（1 で減らない）。盾が書き込む。</summary>
        public float DamageTaken { get; set; } = 1f;

        /// <summary>加護（<see cref="CharacterBuffs"/>）も掛けた、今受けるダメージの倍率。</summary>
        public float EffectiveDamageTaken => DamageTaken * (Buffs != null ? Buffs.DamageTakenScale : 1f);

        // 加護は掛ける側が後から付けるので、無ければ毎回探す。
        private CharacterBuffs Buffs => buffs != null ? buffs : buffs = GetComponent<CharacterBuffs>();

        /// <summary>毎秒の回復量。自然回復のエンチャントが書き込む。端数はためて 1 になったら回復する。</summary>
        public float RegenPerSecond { get; set; }

        public event Action<PlayerHealth> Changed;

        private void Awake()
        {
            maxHp = Mathf.Max(1, maxHp);
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        }

        private void Update()
        {
            if (RegenPerSecond <= 0f || IsDead || currentHp >= maxHp)
            {
                regenCarry = 0f;
                return;
            }

            regenCarry += RegenPerSecond * Time.deltaTime;
            int heal = Mathf.FloorToInt(regenCarry);
            if (heal <= 0) return;

            regenCarry -= heal;
            Heal(heal);
        }

        /// <summary>amount に <see cref="EffectiveDamageTaken"/>（盾と加護）を掛けて減らす（四捨五入、最低 1）。負の値は無視する（回復は Heal で）。</summary>
        public void Damage(int amount)
        {
            if (amount <= 0) return;
            SetCurrent(currentHp - ReducedDamage(amount, EffectiveDamageTaken));
        }

        /// <summary>amount に倍率 taken を掛けたダメージ。四捨五入し、最低 1 は入る。</summary>
        public static int ReducedDamage(int amount, float taken)
        {
            if (amount <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Clamp01(taken)));
        }

        /// <summary>amount だけ回復する。最大を超えない。</summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            SetCurrent(currentHp + amount);
        }

        /// <summary>最大 HP を変える。今の HP は新しい最大に収まるよう切り詰める。</summary>
        public void SetMax(int value)
        {
            value = Mathf.Max(1, value);
            if (value == maxHp) return;

            maxHp = value;
            currentHp = Mathf.Min(currentHp, maxHp);
            Changed?.Invoke(this);
        }

        /// <summary>最大と今の HP をまとめて変える。知らせるのは 1 回だけ（レベルアップで両方増やすとき用）。</summary>
        public void SetMaxAndCurrent(int max, int current)
        {
            max = Mathf.Max(1, max);
            current = Mathf.Clamp(current, 0, max);
            if (max == maxHp && current == currentHp) return;

            maxHp = max;
            currentHp = current;
            Changed?.Invoke(this);
        }

        public void SetCurrent(int value)
        {
            value = Mathf.Clamp(value, 0, maxHp);
            if (value == currentHp) return;

            currentHp = value;
            Changed?.Invoke(this);
        }
    }
}
