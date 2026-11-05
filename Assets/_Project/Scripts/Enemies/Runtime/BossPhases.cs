using System;
using TpsDungeon.Audio.Runtime;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// ボスの段階（<see cref="BossPhaseRules"/>）。残り HP が減るほど段階が上がり、動きが速く・激しくなる。
    /// 段階はダメージを受けるたびに決め直し、上がったら <see cref="StageChanged"/> を出して咆哮の音と効果を出す。
    /// 頭（EnemyBrain）は <see cref="Stage"/> と <see cref="Tempo"/> を読んで、段階ごとの動きの変化・攻撃の間と溜めの短縮に使う。
    /// 歩く速さは <see cref="EnemyMotor.TempoScale"/> に入れる。
    /// 段階の境目とテンポは Bosses.csv から入る（BossCsvImporter）。ボスのプレハブのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("TPS Dungeon/Boss Phases")]
    public sealed class BossPhases : MonoBehaviour
    {
        [SerializeField, Tooltip("HUD に出す名前。")]
        private string displayName = "ボス";

        [SerializeField, Range(0f, 1f), Tooltip("残り HP の割合がこれ以下で段階 2。")]
        private float phase2Hp = 0.66f;

        [SerializeField, Range(0f, 1f), Tooltip("残り HP の割合がこれ以下で段階 3。")]
        private float phase3Hp = 0.33f;

        [SerializeField, Min(0.1f), Tooltip("段階 2 のテンポ（攻撃の間・溜めをこれで割り、歩く速さに掛ける）。")]
        private float tempo2 = 1.2f;

        [SerializeField, Min(0.1f), Tooltip("段階 3 のテンポ。")]
        private float tempo3 = 1.45f;

        [Header("段階が上がったとき（未設定なら出さない）")]
        [SerializeField, Tooltip("咆哮の音。")]
        private AudioClip roarClip;

        [SerializeField, Range(0f, 1f)]
        private float roarVolume = 1f;

        [SerializeField, Tooltip("体の中心に出す効果。")]
        private GameObject roarEffect;

        [SerializeField, Min(0.01f)]
        private float roarEffectScale = 1.5f;

        private EnemyHealth health;
        private EnemyMotor motor;

        /// <summary>今の段階（1〜3）。</summary>
        public int Stage { get; private set; } = 1;

        /// <summary>今の段階のテンポ。</summary>
        public float Tempo => BossPhaseRules.TempoFor(Stage, tempo2, tempo3);

        public string DisplayName => displayName;
        public float Phase2Hp => phase2Hp;
        public float Phase3Hp => phase3Hp;
        public EnemyHealth Health => health != null ? health : health = GetComponent<EnemyHealth>();

        /// <summary>段階が上がった（新しい段階）。</summary>
        public event Action<int> StageChanged;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            motor = GetComponent<EnemyMotor>();
        }

        private void OnEnable()
        {
            Health.Damaged += OnDamaged;
            ApplyTempo();
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(EnemyHealth _, DamageInfo __, int ___)
        {
            if (health.IsDead) return;
            float fraction = (float)health.CurrentHp / health.MaxHp;
            int next = Mathf.Max(Stage, BossPhaseRules.StageFor(fraction, phase2Hp, phase3Hp));
            if (next == Stage) return;

            Stage = next;
            ApplyTempo();

            Vector3 center = TryGetComponent(out Collider body) && body.enabled ? body.bounds.center : transform.position + Vector3.up;
            if (roarClip != null) GameAudio.Instance?.PlaySeAt(roarClip, center, roarVolume);
            EnemyEffects.Spawn(roarEffect, center, Quaternion.identity, roarEffectScale, 3f);
            StageChanged?.Invoke(Stage);
        }

        private void ApplyTempo()
        {
            if (motor != null) motor.TempoScale = Tempo;
        }

        /// <summary>段階を強制する（確認用の窓から）。</summary>
        public void ForceStage(int stage)
        {
            Stage = Mathf.Clamp(stage, 1, BossPhaseRules.MaxStage);
            ApplyTempo();
            StageChanged?.Invoke(Stage);
        }
    }
}
