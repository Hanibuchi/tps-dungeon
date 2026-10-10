using System;
using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// キャラに掛かっている加護（武器種 15 ダメージ軽減の杖で張る膜）。種類（<see cref="BlessingKind"/>）ごとに値と残り時間を持ち、時間が来たら外れる。
    /// 違う種類は同時に付く。同じ種類は重ねては掛からず、掛け直すと値は大きい方を、残り時間は長い方を取る。
    /// 被ダメージは <see cref="PlayerHealth"/> が、クリティカル倍率は攻撃の役が読む。状態異常耐性は値だけで、読む側は仲間の状態異常ができたときに繋ぐ。
    /// 掛ける側（RangedAttacker）が初めて掛けるときに付ける。プレハブに置いておかなくてよい。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Buffs")]
    public sealed class CharacterBuffs : MonoBehaviour
    {
        private static readonly int KindCount = Enum.GetValues(typeof(BlessingKind)).Length;

        private readonly float[] amounts = new float[KindCount];
        private readonly float[] remaining = new float[KindCount];

        /// <summary>受けるダメージに掛ける倍率（1 − ダメージ軽減）。付いていなければ 1。</summary>
        public float DamageTakenScale => Mathf.Clamp01(1f - Amount(BlessingKind.DamageReduction));

        /// <summary>クリティカル倍率の上げ幅（割合）。クリティカル倍率に (1 ＋ この値) を掛ける（1 で ×3 が ×6 に）。付いていなければ 0。</summary>
        public float CritMultiplierBonus => Amount(BlessingKind.CritMultiplier);

        /// <summary>状態異常耐性（割合、1 で無効）。付いていなければ 0。</summary>
        public float AilmentResistance => Amount(BlessingKind.AilmentResistance);

        /// <summary>ある種類の加護が付いた・掛け直された・外れた。</summary>
        public event Action<CharacterBuffs, BlessingKind> Changed;

        /// <summary>go の加護。付いていなければ付ける。</summary>
        public static CharacterBuffs On(GameObject go)
        {
            if (go == null) return null;
            return go.TryGetComponent(out CharacterBuffs buffs) ? buffs : go.AddComponent<CharacterBuffs>();
        }

        /// <summary>その種類の加護が付いているか。</summary>
        public bool Has(BlessingKind kind) => remaining[(int)kind] > 0f;

        /// <summary>その種類の加護の残り時間（秒）。付いていなければ 0。</summary>
        public float Remaining(BlessingKind kind) => Mathf.Max(0f, remaining[(int)kind]);

        /// <summary>その種類の加護の値。付いていなければ 0。</summary>
        public float Amount(BlessingKind kind) => Has(kind) ? amounts[(int)kind] : 0f;

        /// <summary>その種類の加護を duration 秒張る。もう付いていれば、値は大きい方を、残り時間は長い方を取る。</summary>
        public void Apply(BlessingKind kind, float amount, float duration)
        {
            if (duration <= 0f) return;

            int i = (int)kind;
            bool had = Has(kind);
            amounts[i] = had ? Mathf.Max(amounts[i], amount) : amount;
            remaining[i] = Mathf.Max(had ? remaining[i] : 0f, duration);
            Changed?.Invoke(this, kind);
        }

        /// <summary>その種類の加護をすぐ外す。</summary>
        public void Clear(BlessingKind kind)
        {
            if (!Has(kind)) return;
            remaining[(int)kind] = 0f;
            Changed?.Invoke(this, kind);
        }

        private void Update()
        {
            for (int i = 0; i < KindCount; i++)
            {
                if (remaining[i] <= 0f) continue;

                remaining[i] -= Time.deltaTime;
                if (remaining[i] > 0f) continue;

                remaining[i] = 0f;
                Changed?.Invoke(this, (BlessingKind)i);
            }
        }
    }
}
