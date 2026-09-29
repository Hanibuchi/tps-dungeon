using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>近接コンボの 1 段。時間は速射の補正前（秒）。</summary>
    [Serializable]
    public struct MeleeComboStep
    {
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
