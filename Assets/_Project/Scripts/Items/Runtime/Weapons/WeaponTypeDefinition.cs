using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種（全 30 種のうちの 1 つ）の定義。挙動の数値と、付けられるエンチャントの一覧を持つ。
    /// 武器（WeaponDefinition）はこれを参照し、強さとランクだけを持つ。
    /// 今は近接（コンボ）の武器種だけを扱う。飛び道具などを足すときは、ここに設定を足すか派生させる。
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

        [Header("見た目")]
        [SerializeField, Tooltip("手に持ったときの見た目の位置合わせ（右手の骨から見たローカル）。")]
        private Vector3 heldLocalPosition;

        [SerializeField]
        private Vector3 heldLocalEuler;

        [Header("エフェクト")]
        [SerializeField, Tooltip("振りの判定の瞬間に出す（任意）。位置と向きは段ごとの swingEffectOffset / swingEffectEuler。")]
        private GameObject swingEffect;

        [SerializeField, Min(0.01f), Tooltip("振りのエフェクトの大きさの倍率。")]
        private float swingEffectScale = 1f;

        [SerializeField, Tooltip("敵に当たった所に出す（任意）。")]
        private GameObject hitEffect;

        [SerializeField, Min(0.01f), Tooltip("命中のエフェクトの大きさの倍率。")]
        private float hitEffectScale = 1f;

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
        public Quaternion HeldLocalRotation => Quaternion.Euler(heldLocalEuler);
        public GameObject SwingEffect => swingEffect;
        public float SwingEffectScale => swingEffectScale;
        public GameObject HitEffect => hitEffect;
        public float HitEffectScale => hitEffectScale;
        public AudioClip SwingSound => swingSound;
        public AudioClip HitSound => hitSound;
        public float SoundVolume => soundVolume;
    }
}
