using System;
using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// プレイヤーの体力。値を持って増減させ、変わったら知らせるだけ。
    /// 死亡やダメージ演出はまだ無く、HUD がこれを読んで表示する。
    /// プレイヤーのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Health")]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("最大 HP。")]
        private int maxHp = 100;

        [SerializeField, Min(0), Tooltip("開始時の HP。最大を超えた分は切り捨てる。")]
        private int currentHp = 100;

        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;

        /// <summary>残りの割合（0〜1）。</summary>
        public float Fraction => maxHp > 0 ? (float)currentHp / maxHp : 0f;

        public bool IsDead => currentHp <= 0;

        public event Action<PlayerHealth> Changed;

        private void Awake()
        {
            maxHp = Mathf.Max(1, maxHp);
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        }

        /// <summary>amount だけ減らす。負の値は無視する（回復は Heal で）。</summary>
        public void Damage(int amount)
        {
            if (amount <= 0) return;
            SetCurrent(currentHp - amount);
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

        public void SetCurrent(int value)
        {
            value = Mathf.Clamp(value, 0, maxHp);
            if (value == currentHp) return;

            currentHp = value;
            Changed?.Invoke(this);
        }
    }
}
