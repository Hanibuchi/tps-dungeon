using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種（全 30 種のうちの 1 つ）の定義。挙動の数値と、付けられるエンチャントの一覧を持つ。
    /// 武器（WeaponDefinition）はこれを参照し、強さとランクだけを持つ。
    /// 今は近接（コンボ）の武器種だけを扱う。段ごとの当て方（振る・走る・叩きつける）は MeleeComboStep.motion で選ぶ。飛び道具などを足すときは、ここに設定を足すか派生させる。
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

        [Header("衝撃波（Slam の段。「数」のエンチャント 1 つで 1 本。値は仮）")]
        [SerializeField, Min(0f), Tooltip("1 本が当てるダメージ（その段の 1 撃に対する割合）。")]
        private float shockwaveDamageRatio = 0.5f;

        [SerializeField, Min(0f), Tooltip("着弾点から進む距離（m）。着弾の円の縁から走り出す。")]
        private float shockwaveRange = 6f;

        [SerializeField, Min(0.1f), Tooltip("進む速さ（m/s）。")]
        private float shockwaveSpeed = 14f;

        [SerializeField, Min(0.1f), Tooltip("判定の幅（m）。高さも同じ。")]
        private float shockwaveWidth = 1.2f;

        [SerializeField, Range(0f, 90f), Tooltip("隣り合う衝撃波の間の角度（度）。前を中心に左右対称に広げる。")]
        private float shockwaveSpacingAngle = 20f;

        [SerializeField, Tooltip("衝撃波の通り道に、一定の間隔で噴き上げる見た目（重ねて出す。任意）。")]
        private EffectLayer[] shockwaveEffects = Array.Empty<EffectLayer>();

        [SerializeField, Min(0.1f), Tooltip("通り道の見た目を出す間隔（m）。")]
        private float shockwaveEffectSpacing = 1f;

        [Header("追撃（Slam の段。「多重」のエンチャント 1 つで 1 回。値は仮）")]
        [SerializeField, Min(0f), Tooltip("1 回が当てるダメージ（その段の 1 撃に対する割合）。")]
        private float followUpDamageRatio = 0.6f;

        [SerializeField, Min(0f), Tooltip("k 回目は着弾点から前へ k × この距離（m）ずらして落とす。")]
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

        [Header("見た目")]
        [SerializeField, Tooltip("手に持ったときの見た目の位置合わせ（右手の骨から見たローカル）。" +
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
        [SerializeField, Tooltip("振りの判定の瞬間に、当たっても外れても鳴らす音（風切り）。段ごとに替えるなら MeleeComboStep.swingSound。")]
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
        public Vector3 HeldLocalPosition => heldLocalPosition;
        public Vector3 HeldLocalEuler => heldLocalEuler;
        public GameObject SwingEffect => swingEffect;
        public float SwingEffectScale => swingEffectScale;
        public float SwingEffectLeadTime => swingEffectLeadTime;
        public bool LungePassesThroughEnemies => lungePassesThroughEnemies;
        public bool InvulnerableDuringLunge => invulnerableDuringLunge;
        public float ShockwaveDamageRatio => shockwaveDamageRatio;
        public float ShockwaveRange => shockwaveRange;
        public float ShockwaveSpeed => shockwaveSpeed;
        public float ShockwaveWidth => shockwaveWidth;
        public float ShockwaveSpacingAngle => shockwaveSpacingAngle;
        public IReadOnlyList<EffectLayer> ShockwaveEffects => shockwaveEffects ?? Array.Empty<EffectLayer>();
        public float ShockwaveEffectSpacing => shockwaveEffectSpacing;
        public float FollowUpDamageRatio => followUpDamageRatio;
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
