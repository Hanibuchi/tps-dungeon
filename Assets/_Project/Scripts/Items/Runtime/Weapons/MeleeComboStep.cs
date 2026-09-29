using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>近接コンボの 1 段。時間は速射の補正前（秒）。</summary>
    [Serializable]
    public struct MeleeComboStep
    {
        [Tooltip("当て方。Swing は箱、Lunge は走りながら箱、Slam は着弾点の円。")]
        public MeleeStepMotion motion;

        [Tooltip("1 周のダメージのうち、この段が受け持つ比重。")]
        public float damageWeight;

        [Tooltip("この段のモーション時間（秒）。終わるまで次の段に移らない。")]
        public float duration;

        [Tooltip("振り始めから判定が出るまで（秒）。これ以降に押すと次の段を先行入力できる。")]
        public float hitTime;

        [Tooltip("判定の箱の大きさ（幅・高さ・奥行き、m）。")]
        public Vector3 hitboxSize;

        [Tooltip("判定の箱の中心。キャラの足元から見たローカル位置（m）。")]
        public Vector3 hitboxCenter;

        [Tooltip("当てた敵を押し出す速さ（m/s）。")]
        public float knockback;

        [Tooltip("スタン・気絶の判定の上乗せ（1 で一撃の重さを 2 倍として判定）。0 で素のまま。締めの段を効きやすくするとき用。")]
        public float reactionBonus;

        [Header("Lunge（走る段）")]
        [Tooltip("走る距離（m、持続時間のエンチャントの補正前）。走る間は hitbox で当て続ける。")]
        public float lungeDistance;

        [Tooltip("走る時間（秒、速射の補正前）。hitTime から走り出す。duration はこれより長くしておくこと。")]
        public float lungeDuration;

        [Header("Slam（叩きつける段）")]
        [Tooltip("着弾の円の半径（m、サイズのエンチャントの補正前）。中心は hitboxCenter。")]
        public float slamRadius;

        [Tooltip("着弾点からこの高さ（m）までの上下にいる敵に当てる。")]
        public float slamHeight;

        [Tooltip("振りのエフェクトを出す位置。キャラの足元から見たローカル位置（m）。刃の通り道に合わせる。")]
        public Vector3 swingEffectOffset;

        [Tooltip("振りのエフェクトの向き（キャラから見たローカルの回転、度）。Z で振りの傾きに合わせる。")]
        public Vector3 swingEffectEuler;

        [Tooltip("この段だけ振りの音を替える。未設定なら武器種の swingSound。")]
        public AudioClip swingSound;

        [Tooltip("この段だけ命中の音を替える。未設定なら武器種の hitSound。")]
        public AudioClip hitSound;
    }
}
