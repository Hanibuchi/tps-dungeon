using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// キャラ 1 人分の成長パラメータ。主人公用と仲間ごとに別のアセットを作って CharacterProgression に割り当てる。
    /// 式は <see cref="GrowthCurve"/> を参照。既定値は仮の値で、GrowthCurve.Default と揃えてある。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterGrowth", menuName = "TPS Dungeon/Character Growth")]
    public sealed class CharacterGrowthProfile : ScriptableObject
    {
        [Header("MaxHP = HP_base + C_hp × (L−1)^p")]
        [SerializeField, Min(1f), Tooltip("Lv1 の MaxHP（HP_base）。")]
        private float hpBase = 100f;

        [SerializeField, Min(0f), Tooltip("レベルごとの伸びの係数（C_hp）。")]
        private float hpCoefficient = 2.6f;

        [SerializeField, Min(0f), Tooltip("伸びの指数（p）。1 で直線、大きいほど後半に伸びる。")]
        private float hpExponent = 1.1f;

        [Header("基礎攻撃力 = ATK_base + C_atk × (L−1)^p_atk")]
        [SerializeField, Min(0f), Tooltip("Lv1 の基礎攻撃力（ATK_base）。")]
        private float attackBase = 10f;

        [SerializeField, Min(0f), Tooltip("レベルごとの伸びの係数（C_atk）。")]
        private float attackCoefficient = 0.162f;

        [SerializeField, Min(0f), Tooltip("伸びの指数（p_atk）。HP とは別に持つ。")]
        private float attackExponent = 1.05f;

        [Header("次のレベルまでの経験値 = round(EXP_base × L^q)")]
        [SerializeField, Min(1f), Tooltip("Lv1→2 に要る経験値（EXP_base）。")]
        private float expBase = 10f;

        [SerializeField, Min(0f), Tooltip("必要経験値の指数（q）。")]
        private float expExponent = 2f;

        [Header("その他")]
        [SerializeField, Range(1, 99), Tooltip("最大レベル。")]
        private int maxLevel = 99;

        [SerializeField, Tooltip("レベルアップで MaxHP が増えたときの現在 HP。増えた分だけ足すか、全回復か。")]
        private LevelUpHpMode levelUpHp = LevelUpHpMode.AddIncrease;

        public LevelUpHpMode LevelUpHp => levelUpHp;

        public GrowthCurve Curve => new GrowthCurve(
            hpBase, hpCoefficient, hpExponent,
            attackBase, attackCoefficient, attackExponent,
            expBase, expExponent, maxLevel);

        /// <summary>コードから作るとき（テストや仲間の動的生成）用。</summary>
        public void Configure(GrowthCurve curve, LevelUpHpMode hpMode = LevelUpHpMode.AddIncrease)
        {
            hpBase = (float)curve.HpBase;
            hpCoefficient = (float)curve.HpCoefficient;
            hpExponent = (float)curve.HpExponent;
            attackBase = (float)curve.AttackBase;
            attackCoefficient = (float)curve.AttackCoefficient;
            attackExponent = (float)curve.AttackExponent;
            expBase = (float)curve.ExpBase;
            expExponent = (float)curve.ExpExponent;
            maxLevel = curve.MaxLevel;
            levelUpHp = hpMode;
        }
    }
}
