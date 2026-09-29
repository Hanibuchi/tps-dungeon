namespace TpsDungeon.Player
{
    /// <summary>
    /// キャラの全身で再生できる動き。Blink の Animations_Starter_Pack のクリップ 1 本ずつに対応する。
    /// 値は Animator の Action パラメータの条件に焼き込まれるので、並べ替えず、足すときは末尾に。
    /// 再生は <see cref="CharacterActions"/> から。Animator は CharacterAnimatorBuilder が組む。
    /// </summary>
    public enum CharacterAction
    {
        /// <summary>何も再生しない（下の層の移動・構えがそのまま見える）。</summary>
        None = 0,

        // ---- Combat ----
        BlockingLoop = 1,
        BowShot = 2,
        Buff = 3,
        CastingLoop = 4,
        Death = 5,
        GetHit = 6,
        IdleCombat = 7,
        MeleeAttackOneHanded = 8,
        MeleeAttackTwoHanded = 9,
        PunchLeft = 10,
        PunchRight = 11,
        SpellCast = 12,
        SpellCastStart = 13,
        SpellCastEnd = 14,
        StunnedLoop = 15,

        // ---- Gathering ----
        Gathering = 16,
        MiningLoop = 17,

        // ---- Movement ----
        FallingLoop = 18,
        Idle = 19,
        Jump = 20,
        JumpUp = 21,
        JumpDown = 22,
        JumpWhileRunning = 23,
        RollBackward = 24,
        RollForward = 25,
        RollLeft = 26,
        RollRight = 27,
        RunBackward = 28,
        RunBackwardLeft = 29,
        RunBackwardRight = 30,
        RunForward = 31,
        RunLeft = 32,
        RunRight = 33,
        Sprint = 34,
        StrafeLeft = 35,
        StrafeRight = 36,
    }
}
