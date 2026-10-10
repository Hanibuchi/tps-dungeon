using System;
using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// キャラに掛かっている加護（武器種 15 ダメージ軽減の杖で張る膜）。被ダメージ軽減・クリティカル率の加算・状態異常耐性を
    /// 1 つにまとめて持ち、時間が来たら外れる。重ねては掛からず、掛け直すと値は大きい方を、残り時間は長い方を取る。
    /// 被ダメージは <see cref="PlayerHealth"/> が、クリティカル率は攻撃の役が読む。状態異常耐性は値だけで、読む側は仲間の状態異常ができたときに繋ぐ。
    /// 掛ける側（RangedAttacker）が初めて掛けるときに付ける。プレハブに置いておかなくてよい。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Buffs")]
    public sealed class CharacterBuffs : MonoBehaviour
    {
        private float damageReduction;
        private float critChance;
        private float ailmentResistance;
        private float remaining;

        /// <summary>加護が付いているか。</summary>
        public bool HasBlessing => remaining > 0f;

        /// <summary>加護の残り時間（秒）。付いていなければ 0。</summary>
        public float Remaining => Mathf.Max(0f, remaining);

        /// <summary>受けるダメージに掛ける倍率（1 − 被ダメージ軽減）。加護が無ければ 1。</summary>
        public float DamageTakenScale => HasBlessing ? Mathf.Clamp01(1f - damageReduction) : 1f;

        /// <summary>クリティカル率の加算（0.1 で +10%）。加護が無ければ 0。</summary>
        public float CritChanceBonus => HasBlessing ? critChance : 0f;

        /// <summary>状態異常耐性（割合、1 で無効）。加護が無ければ 0。</summary>
        public float AilmentResistance => HasBlessing ? ailmentResistance : 0f;

        /// <summary>加護が付いた・掛け直された・外れた。</summary>
        public event Action<CharacterBuffs> Changed;

        /// <summary>go の加護。付いていなければ付ける。</summary>
        public static CharacterBuffs On(GameObject go)
        {
            if (go == null) return null;
            return go.TryGetComponent(out CharacterBuffs buffs) ? buffs : go.AddComponent<CharacterBuffs>();
        }

        /// <summary>加護を duration 秒張る。もう付いていれば、値は大きい方を、残り時間は長い方を取る。</summary>
        public void ApplyBlessing(float damageReduction, float critChance, float ailmentResistance, float duration)
        {
            if (duration <= 0f) return;

            bool had = HasBlessing;
            this.damageReduction = had ? Mathf.Max(this.damageReduction, damageReduction) : damageReduction;
            this.critChance = had ? Mathf.Max(this.critChance, critChance) : critChance;
            this.ailmentResistance = had ? Mathf.Max(this.ailmentResistance, ailmentResistance) : ailmentResistance;
            remaining = Mathf.Max(had ? remaining : 0f, duration);
            Changed?.Invoke(this);
        }

        /// <summary>加護をすぐ外す。</summary>
        public void ClearBlessing()
        {
            if (!HasBlessing) return;
            remaining = 0f;
            Changed?.Invoke(this);
        }

        private void Update()
        {
            if (remaining <= 0f) return;

            remaining -= Time.deltaTime;
            if (remaining > 0f) return;

            remaining = 0f;
            Changed?.Invoke(this);
        }
    }
}
