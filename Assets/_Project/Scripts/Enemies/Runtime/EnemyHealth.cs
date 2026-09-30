using System;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の体力。ダメージを受けて減らし、受けたことと倒れたことを知らせるだけ。
    /// ひるみ・気絶は EnemyDamageReaction、倒れたあとの始末は EnemyDeath が受け持つ。
    /// 敵のプレハブのルートに付ける（MonsterBuilder が付ける）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Health")]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("最大 HP。値は仮。")]
        private int maxHp = 30;

        [SerializeField, Tooltip("デバッグ用。倒れず、HP が尽きる一撃を受けたら満タンに戻る。スタン・気絶・ノックバックを好きな最大 HP で試すときに使う。")]
        private bool immortal;

        private int currentHp = -1;

        public int MaxHp => Mathf.Max(1, maxHp);

        public int CurrentHp
        {
            get
            {
                // EditMode のテストでは Awake が走らないので、初めて読まれたときに満タンにする。
                if (currentHp < 0) currentHp = MaxHp;
                return currentHp;
            }
        }

        public bool IsDead => CurrentHp <= 0;

        public bool Immortal => immortal;

        /// <summary>
        /// ダメージを受けた。dealt は実際に減った量（残り HP より大きな一撃でも残り HP まで）。
        /// 倒れた一撃でも Died より先に飛ぶ。
        /// </summary>
        public event Action<EnemyHealth, DamageInfo, int> Damaged;

        /// <summary>HP が 0 になった。1 体につき 1 回だけ。</summary>
        public event Action<EnemyHealth> Died;

        private void Awake()
        {
            currentHp = MaxHp;
        }

        /// <summary>ダメージを与える。実際に減った量を返す。倒れたあとや 0 以下の量では何もしない。</summary>
        public int TakeDamage(DamageInfo damage)
        {
            if (damage.Amount <= 0 || IsDead) return 0;

            // 死なないときは残り HP に関係なく満タンのときと同じだけ受けたことにする（スタン・気絶の起きやすさを揃えるため）。
            int dealt = Mathf.Min(damage.Amount, immortal ? MaxHp : currentHp);
            currentHp -= dealt;
            if (immortal && currentHp <= 0) currentHp = MaxHp;

            Damaged?.Invoke(this, damage, dealt);
            if (currentHp <= 0) Died?.Invoke(this);
            return dealt;
        }

        /// <summary>最大 HP を value（最低 1）にして満タンにする。試験用の的を硬くするときなどに使う。</summary>
        public void SetMaxHp(int value)
        {
            maxHp = Mathf.Max(1, value);
            currentHp = maxHp;
        }

        /// <summary>死なないようにする（デバッグ用）。外すとき HP が残っていればそのまま。</summary>
        public void SetImmortal(bool value)
        {
            immortal = value;
        }

        /// <summary>使い回す（プール）ときに満タンへ戻す。</summary>
        public void ResetHealth()
        {
            currentHp = MaxHp;
        }

        // 攻撃の仕組みができるまでの動作確認用（Play 中に Inspector の右上メニューから）。
        [ContextMenu("最大 HP の 10% を与える")]
        private void DebugDamageSmall() => DebugDamage(0.1f);

        [ContextMenu("最大 HP の 50% を与える")]
        private void DebugDamageLarge() => DebugDamage(0.5f);

        private void DebugDamage(float fraction)
        {
            int amount = Mathf.Max(1, Mathf.RoundToInt(MaxHp * fraction));
            TakeDamage(new DamageInfo(amount, null, -transform.forward));
        }
    }
}
