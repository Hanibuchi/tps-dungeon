using System;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// HP が 0 になった敵の始末。倒れるアニメーション（気絶で倒れ込んでいたらそのまま）、
    /// 当たり判定を切る、経験値を配る（<see cref="EnemyExpReward"/> があれば）。
    /// 死体を消す・落とし物を出すなどはまだ無い。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("TPS Dungeon/Enemy Death")]
    public sealed class EnemyDeath : MonoBehaviour
    {
        // MonsterBuilder（エディタ専用アセンブリ）が焼き込んだ Animator のパラメータ名。向こうを変えたらここも揃えること。
        private const string DeadParam = "Dead";

        private EnemyHealth health;

        /// <summary>始末を終えた。</summary>
        public event Action<EnemyDeath> Finished;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<EnemyHealth>();
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(EnemyHealth _)
        {
            var ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll == null || !ragdoll.IsRagdoll)
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null) animator.SetBool(DeadParam, true);
            }

            foreach (Collider c in GetComponents<Collider>()) c.enabled = false;

            var reward = GetComponent<EnemyExpReward>();
            if (reward != null) reward.Grant();

            Finished?.Invoke(this);
        }
    }
}
