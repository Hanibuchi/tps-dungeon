using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 近づくと毒霧を出す敵（05 毒霧）。
    ///
    ///   ゆっくり追う → 相手が <see cref="emitRange"/> に入ったら止まって溜める（<see cref="windup"/> 秒）
    ///   → 足元に毒霧（<see cref="PoisonCloud"/>）を出す → <see cref="cooldown"/> 秒は出さずに追い続ける
    /// 霧はその場に残るので、本体を追って霧の中へ入ると削られる。霧の 1 刻みのダメージは <see cref="EnemyBrain.damage"/>。
    /// 溜めの間のスタン・気絶で取り消す。
    /// MonsterBuilder がプレハブのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Poison Gas)")]
    public sealed class PoisonGasEnemyBrain : EnemyBrain
    {
        // 霧を出すときに流すクリップ（Blob の見本にある、跳ねる動き）。無いグループなら Attack を流す。
        private const string EmitParam = "Jump";

        [Header("毒霧")]
        [SerializeField, Min(0.5f), Tooltip("相手の足元までこの距離に入ったら霧を出す（m）。霧の半径より短くして、出した霧の中に相手が入るようにする。")]
        private float emitRange = 2.5f;

        [SerializeField, Min(0f), Tooltip("止まってから霧が出るまで（秒）。")]
        private float windup = 0.6f;

        [SerializeField, Min(0.5f), Tooltip("霧の半径（m）。")]
        private float cloudRadius = 3f;

        [SerializeField, Min(0.5f), Tooltip("霧の高さ（m）。")]
        private float cloudHeight = 2.2f;

        [SerializeField, Min(0.1f), Tooltip("霧が残る時間（秒）。")]
        private float cloudDuration = 5f;

        [SerializeField, Min(0.1f), Tooltip("霧が削る間隔（秒）。")]
        private float tickInterval = 0.5f;

        [Header("見た目")]
        [SerializeField, Tooltip("霧のエフェクト（ループする煙）。")]
        private GameObject cloudEffect;

        [SerializeField, Min(0.01f), Tooltip("霧のエフェクトの素材そのままの半径（m、粒の大きさ込み）。Smoke hemisphere loop は 6 m ほど。")]
        private float cloudEffectRadius = 6f;

        [SerializeField, Tooltip("煙に掛ける色。")]
        private Color cloudTint = new Color(0.45f, 1f, 0.3f, 0.85f);

        private bool emitting;
        private float timer;

        protected override void OnEnable()
        {
            base.OnEnable();
            emitting = false;
        }

        protected override void Think(float dt)
        {
            if (emitting)
            {
                if (HasTarget) Motor.LookAt = Target.Position;
                timer -= dt;
                if (timer <= 0f) Emit();
                return;
            }

            Motor.LookAt = null;
            Motor.Destination = ChasePoint();
            Motor.StopDistance = 1f;

            if (!HasTarget || !TargetVisible || CooldownRemaining > 0f || TargetDistance > emitRange) return;

            emitting = true;
            timer = windup;
            Motor.Destination = null;
            Play(HasParam(EmitParam) ? EmitParam : AttackParam);
            PlaySound(windupClip, Center);
        }

        private void Emit()
        {
            emitting = false;
            Motor.LookAt = null;
            CooldownRemaining = cooldown;

            Vector3 ground = EnemyMotor.OnNavMesh(transform.position);
            PoisonCloud.Spawn(ground, new PoisonCloud.Settings
            {
                Radius = cloudRadius,
                Height = cloudHeight,
                Duration = cloudDuration,
                TickInterval = tickInterval,
                TickDamage = damage,
                Source = gameObject,
                Effect = cloudEffect,
                EffectRadius = cloudEffectRadius,
                Tint = cloudTint,
            });
            PlaySound(attackClip, Center);
        }

        public override string StateLabel => emitting ? $"溜め {timer:0.0}s"
            : CooldownRemaining > 0f ? $"次の霧まで {CooldownRemaining:0.0}s"
            : HasTarget ? "追う" : "待つ";

        protected override void DrawRanges()
        {
            Disc(transform.position, emitRange, new Color(1f, 0.3f, 0.2f, 0.9f));
            Disc(transform.position, cloudRadius, new Color(0.4f, 1f, 0.3f, 0.6f));
        }

        protected override void Interrupted()
        {
            if (!emitting) return;
            emitting = false;
            Motor.LookAt = null;
            CooldownRemaining = Mathf.Max(CooldownRemaining, cooldown * 0.3f);
        }
    }
}
