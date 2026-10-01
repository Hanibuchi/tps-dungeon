using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種（全 30 種のうちの 1 つ）の定義。挙動の数値と、付けられるエンチャントの一覧を持つ。
    /// 武器（WeaponDefinition）はこれを参照し、強さとランクだけを持つ。
    /// 近接（コンボ）の武器種は comboSteps を持ち、段ごとの当て方（振る・走る・叩きつける）は MeleeComboStep.motion で選ぶ。
    /// 遠距離の武器種は rangedKind（弓・持続弓）を持ち、遠距離の欄の値で撃つ（RangedAttacker）。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponType", menuName = "TPS Dungeon/Weapons/Weapon Type")]
    public sealed class WeaponTypeDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("武器種の番号（Notion の武器種データの番号。例: 01）。")]
        private string id;

        [SerializeField] private string displayName;

        [SerializeField, Tooltip("キャラの Animator の WeaponType に入れる値（CharacterAnimatorBuilder.Weapon）。")]
        private int animatorWeaponType;

        [SerializeField, Tooltip("盾（武器種 27）の防御が効くか。片手武器なら真。盾はまだ無い。")]
        private bool canUseShield;

        [Header("エンチャント")]
        [SerializeField, Tooltip("この武器種に付けられるエンチャント。")]
        private List<EnchantmentDefinition> allowedEnchantments = new List<EnchantmentDefinition>();

        [SerializeField, Tooltip("エンチャントの付き方（全武器種で共通のアセット）。")]
        private EnchantmentRollSettings enchantmentRoll;

        [Header("ダメージ")]
        [SerializeField, Min(0f), Tooltip("持ち主の基礎攻撃力を武器の強さ（DPS）に足すときの係数。値は仮。")]
        private float characterAttackWeight = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("エンチャント・永続強化の前のクリティカル率。値は仮。")]
        private float baseCritChance = 0.05f;

        [SerializeField, Min(1f), Tooltip("エンチャント・永続強化の前のクリティカル倍率。値は仮。")]
        private float baseCritMultiplier = 1.5f;

        [Header("近接コンボ（値は仮）")]
        [SerializeField, Tooltip("段の並び。最後の段のあとは 1 段目に戻る。")]
        private MeleeComboStep[] comboSteps = Array.Empty<MeleeComboStep>();

        [SerializeField, Min(0f), Tooltip("段が終わってからこの秒数以内に押せば次の段に続く。過ぎたら 1 段目から。")]
        private float comboChainGrace = 0.25f;

        [SerializeField, Min(0f), Tooltip("コンボが切れて（最後の段を振り終えるか、猶予を過ぎて）から次に振れるまでの秒数。")]
        private float comboCooldown = 0.3f;

        [Header("ダッシュ（Lunge の段）")]
        [SerializeField, Tooltip("走っている間は敵をすり抜け、通り道の敵みんなに当てる。偽なら敵にぶつかって止まる。壁ではどちらでも止まる。")]
        private bool lungePassesThroughEnemies = true;

        [SerializeField, Tooltip("走っている間は無敵（MeleeAttacker.IsInvulnerable が真になる。被ダメージ側が読む）。")]
        private bool invulnerableDuringLunge = true;

        [Header("数（Slam の段）")]
        [SerializeField, Range(0f, 90f), Tooltip("「数」のエンチャント 1 つで叩きつけが 1 つ増える。本撃と合わせて、持ち主を中心に前から左右対称に、隣とこの角度（度）ずつ回して並べる。" +
            "それぞれ別の判定で、重なった所の敵は重なった数だけ当たる。")]
        private float slamSpreadAngle = 30f;

        [Header("追撃（Slam の段。「多重」のエンチャント 1 つで、叩きつけ 1 つにつき 1 回。ダメージは本撃と同じ。値は仮）")]
        [SerializeField, Min(0f), Tooltip("k 回目は着弾点から、その叩きつけの向きへ k × この距離（m）ずらして落とす。")]
        private float followUpSpacing = 1.5f;

        [SerializeField, Min(0f), Tooltip("k 回目は本撃から k × この秒数あとに落とす。")]
        private float followUpInterval = 0.18f;

        [SerializeField, Min(0.01f), Tooltip("追撃の着弾の見た目は、本撃の着弾（slamEffects）をこの倍率で出す。")]
        private float followUpEffectScale = 0.7f;

        [Header("カメラ揺れ（Slam の段）")]
        [SerializeField, Min(0f), Tooltip("着弾の瞬間にカメラを揺らす強さ。0 で揺らさない。")]
        private float slamShake;

        [SerializeField, Range(0f, 1f), Tooltip("追撃の着弾で揺らす強さ（本撃に対する割合）。")]
        private float followUpShakeRatio = 0.4f;

        [Header("遠距離（弓・持続弓。値は仮）")]
        [SerializeField, Tooltip("撃ち方。None なら遠距離の武器ではない。")]
        private RangedAttackKind rangedKind;

        [SerializeField, Min(0.05f), Tooltip("撃つ間隔（秒、速射の補正前）。1 発のダメージは 強さ × この秒数。")]
        private float fireInterval = 0.8f;

        [SerializeField, Tooltip("矢を放つ位置。キャラの足元から見たローカル位置（m）。")]
        private Vector3 muzzleOffset = new Vector3(0f, 1.4f, 0.5f);

        [SerializeField, Min(1f), Tooltip("照準を探す距離（m）。弓はこの先に何も無ければこの距離の点へ向けて撃つ。持続弓はこの水平距離までの地面に降らせる。")]
        private float aimMaxDistance = 40f;

        [SerializeField, Tooltip("飛ぶ矢の見た目（+Z が矢の先）。当たり判定はコードが持つので、見た目だけでよい。")]
        private GameObject projectilePrefab;

        [SerializeField, Min(0.1f), Tooltip("矢の速さ（m/s、弾速のエンチャントの補正前）。")]
        private float projectileSpeed = 35f;

        [SerializeField, Min(1f), Tooltip("矢が飛べる距離（m）。")]
        private float projectileRange = 45f;

        [SerializeField, Min(0.01f), Tooltip("矢の当たり判定の半径（m）。")]
        private float projectileRadius = 0.15f;

        [SerializeField, Min(0f), Tooltip("矢が当てた敵を押し出す速さ（m/s、ノックバックのエンチャントの補正前）。")]
        private float projectileKnockback = 2f;

        [SerializeField, Range(0f, 45f), Tooltip("「数」で増えた矢を、狙いを中心に左右対称に並べるときの隣との角度（度）。")]
        private float volleySpreadAngle = 8f;

        [SerializeField, Min(0f), Tooltip("「多重」の一斉射の遅れ。k 回目は本撃から k × この秒数あとに同じ向きへ放つ。")]
        private float multishotInterval = 0.12f;

        [SerializeField, Min(0f), Tooltip("ホーミングで矢が向きを変えられる速さ（度/秒）。")]
        private float homingTurnRate = 540f;

        [SerializeField, Min(0f), Tooltip("ホーミングで追う敵を探す距離（m）。矢の前にいる敵だけを追う。")]
        private float homingRange = 12f;

        [Header("矢の雨（持続弓。値は仮）")]
        [SerializeField, Min(0.1f), Tooltip("雨の範囲の半径（m、サイズのエンチャントの補正前）。")]
        private float rainRadius = 1.25f;

        [SerializeField, Min(0.1f), Tooltip("この高さ（m）までの上下にいる敵に当てる。")]
        private float rainHeight = 2f;

        [SerializeField, Min(0f), Tooltip("雨の続く時間（秒、持続時間のエンチャントの補正前）。")]
        private float rainDuration = 3f;

        [SerializeField, Min(0.05f), Tooltip("雨がダメージを与える間隔（秒）。")]
        private float rainTickInterval = 0.5f;

        [SerializeField, Min(0f), Tooltip("撃ってから雨が降り始めるまで（秒、弾速のエンチャントで縮む）。")]
        private float rainDelay = 0.7f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた雨を置く、狙った所からの距離の下限（m、サイズで伸びる）。向きと距離はランダム。")]
        private float rainScatterMin = 1.25f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた雨を置く、狙った所からの距離の上限（m、サイズで伸びる）。")]
        private float rainScatterMax = 3f;

        [SerializeField, Min(0f), Tooltip("「多重」で同じ所にもう一度降らせる遅れ。k 回目は本撃から k × この秒数あとに降り始める。")]
        private float rainRepeatInterval = 1f;

        [SerializeField, Tooltip("雨の見た目（任意）。降り始めに範囲の中心へ出し、雨の間だけ続ける。繰り返すバーストは雨の長さまで延ばす。")]
        private GameObject rainEffect;

        [SerializeField, Min(0.01f), Tooltip("rainEffect の素材そのままの範囲の半径（m）。雨の半径に合うよう水平だけ縮める（縦はそのまま）。")]
        private float rainEffectRadius = 4f;

        [SerializeField, Range(-30f, 80f), Tooltip("持続弓を構えている間、上半身を上へ反らせる角度（度）。空へ放つ構えに見せる。")]
        private float rainAimPitch = 35f;

        [Header("遠距離の効果音（未設定なら鳴らさない。放つ音は swingSound、敵に当たった音は hitSound）")]
        [SerializeField, Tooltip("弓を引き絞る音。弓のモーションが引き絞り（Bow Draw）に入るたびに鳴らす（持ち替えたときと、撃って引き直すとき）。")]
        private AudioClip drawSound;

        [SerializeField, Tooltip("矢が壁や床に刺さった音。")]
        private AudioClip stickSound;

        [SerializeField, Range(0f, 1f), Tooltip("壁や床に刺さった音の大きさ（武器種の音量に対して）。")]
        private float stickSoundVolume = 0.6f;

        [SerializeField, Tooltip("矢の雨が降る音。降り始めと、刻みごとに鳴らす。")]
        private AudioClip rainSound;

        [SerializeField, Range(0f, 1f), Tooltip("矢の雨が降る音の大きさ（武器種の音量に対して）。刻みごとの音はこの半分。")]
        private float rainSoundVolume = 0.8f;

        [SerializeField, Tooltip("範囲の円（照準の地面の円と、降っている雨の円）の線の材質。未設定なら円を出さない。")]
        private Material rangeRingMaterial;

        [SerializeField, Tooltip("降る雨の範囲の円の色。")]
        private Color rainRingColor = new Color(1f, 0.55f, 0.2f, 0.85f);

        [SerializeField, Tooltip("照準の地面の円の色。")]
        private Color aimRingColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("見た目")]
        [SerializeField, Tooltip("手に持つのを左手にする（弓）。偽なら右手。")]
        private bool heldInLeftHand;

        [SerializeField, Tooltip("手に持ったときの見た目の位置合わせ（持つ手の骨から見たローカル）。" +
            "Play 中に手の武器（Tools/TPS Dungeon/Player/手の武器を選ぶ）を Scene ビューで動かすと、ここに書き戻る。")]
        private Vector3 heldLocalPosition;

        [SerializeField, Tooltip("手に持ったときの見た目の向き（右手の骨から見たローカルのオイラー角）。")]
        private Vector3 heldLocalEuler;

        [Header("エフェクト")]
        [SerializeField, Tooltip("振りの判定の瞬間に出す（任意）。位置と向きは段ごとの swingEffectOffset / swingEffectEuler。")]
        private GameObject swingEffect;

        [SerializeField, Min(0.01f), Tooltip("振りのエフェクトの大きさの倍率。")]
        private float swingEffectScale = 1f;

        [SerializeField, Min(0f), Tooltip("振りのエフェクトの中で「放つ」時刻（秒）。溜めてから放つ素材（Charge slash など）用。" +
            "0 より大きいと、段の振り始めに体へ付けて出して溜めを見せ、この時刻より後にしか出ない粒（斬撃の本体）は判定の瞬間に出す。0 なら判定の瞬間に全部出す。")]
        private float swingEffectLeadTime;

        [SerializeField, Tooltip("敵に当たった所に出す（任意）。")]
        private GameObject hitEffect;

        [SerializeField, Min(0.01f), Tooltip("命中のエフェクトの大きさの倍率。")]
        private float hitEffectScale = 1f;

        [SerializeField, Tooltip("Slam の段で着弾点に重ねて出す（任意）。追撃の着弾にも縮めて出す。")]
        private EffectLayer[] slamEffects = Array.Empty<EffectLayer>();

        [Header("効果音（未設定なら鳴らさない）")]
        [SerializeField, Tooltip("振りの判定の瞬間（遠距離なら矢を放った瞬間）に、当たっても外れても鳴らす音（風切り）。段ごとに替えるなら MeleeComboStep.swingSound。")]
        private AudioClip swingSound;

        [SerializeField, Tooltip("敵に当たったときに鳴らす音。何体に当たっても 1 振りに 1 回。敵側の被弾音にも重なる。段ごとに替えるなら MeleeComboStep.hitSound。")]
        private AudioClip hitSound;

        [SerializeField, Range(0f, 1f), Tooltip("この武器種の音の音量（SE 音量に掛かる）。")]
        private float soundVolume = 1f;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public int AnimatorWeaponType => animatorWeaponType;
        public bool CanUseShield => canUseShield;
        public IReadOnlyList<EnchantmentDefinition> AllowedEnchantments => allowedEnchantments;
        public EnchantmentRollSettings EnchantmentRoll => enchantmentRoll;
        public float CharacterAttackWeight => characterAttackWeight;
        public float BaseCritChance => baseCritChance;
        public float BaseCritMultiplier => baseCritMultiplier;
        public IReadOnlyList<MeleeComboStep> ComboSteps => comboSteps;
        public float ComboChainGrace => comboChainGrace;
        public float ComboCooldown => comboCooldown;
        public bool IsMelee => comboSteps != null && comboSteps.Length > 0;
        public RangedAttackKind RangedKind => rangedKind;
        public bool IsRanged => rangedKind != RangedAttackKind.None;
        public float FireInterval => fireInterval;
        public Vector3 MuzzleOffset => muzzleOffset;
        public float AimMaxDistance => aimMaxDistance;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRange => projectileRange;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileKnockback => projectileKnockback;
        public float VolleySpreadAngle => volleySpreadAngle;
        public float MultishotInterval => multishotInterval;
        public float HomingTurnRate => homingTurnRate;
        public float HomingRange => homingRange;
        public float RainRadius => rainRadius;
        public float RainHeight => rainHeight;
        public float RainDuration => rangedKind == RangedAttackKind.Rain ? rainDuration : 0f;
        public float RainTickInterval => rainTickInterval;
        public float RainDelay => rainDelay;
        public float RainScatterMin => rainScatterMin;
        public float RainScatterMax => rainScatterMax;
        public float RainRepeatInterval => rainRepeatInterval;
        public GameObject RainEffect => rainEffect;
        public float RainEffectRadius => rainEffectRadius;
        public float RainAimPitch => rainAimPitch;
        public AudioClip DrawSound => drawSound;
        public AudioClip StickSound => stickSound;
        public float StickSoundVolume => stickSoundVolume;
        public AudioClip RainSound => rainSound;
        public float RainSoundVolume => rainSoundVolume;
        public Material RangeRingMaterial => rangeRingMaterial;
        public Color RainRingColor => rainRingColor;
        public Color AimRingColor => aimRingColor;
        public bool HeldInLeftHand => heldInLeftHand;
        public Vector3 HeldLocalPosition => heldLocalPosition;
        public Vector3 HeldLocalEuler => heldLocalEuler;
        public GameObject SwingEffect => swingEffect;
        public float SwingEffectScale => swingEffectScale;
        public float SwingEffectLeadTime => swingEffectLeadTime;
        public bool LungePassesThroughEnemies => lungePassesThroughEnemies;
        public bool InvulnerableDuringLunge => invulnerableDuringLunge;
        public float SlamSpreadAngle => slamSpreadAngle;
        public float FollowUpSpacing => followUpSpacing;
        public float FollowUpInterval => followUpInterval;
        public float FollowUpEffectScale => followUpEffectScale;
        public float SlamShake => slamShake;
        public float FollowUpShakeRatio => followUpShakeRatio;
        public IReadOnlyList<EffectLayer> SlamEffects => slamEffects ?? Array.Empty<EffectLayer>();
        public GameObject HitEffect => hitEffect;
        public float HitEffectScale => hitEffectScale;
        public AudioClip SwingSound => swingSound;
        public AudioClip HitSound => hitSound;
        public float SoundVolume => soundVolume;
    }
}
