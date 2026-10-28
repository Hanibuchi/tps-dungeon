using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// キャラ 1 人分の成長パラメータ。主人公用と仲間ごとに別のアセットを作って CharacterProgression に割り当てる。
    /// 式は <see cref="GrowthCurve"/> を参照。既定値は仮の値で、GrowthCurve.Default と揃えてある。
    /// 必要経験値と最大レベルは全員共通で、<see cref="LevelBalance"/> にある。
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

        [Header("基礎攻撃力 = ATK_base + C_atk × (L−1)^p_atk（スプレッドシート「バランス」の攻撃力シート B 列）")]
        [SerializeField, Min(0f), Tooltip("Lv1 の基礎攻撃力（ATK_base）。")]
        private float attackBase = 20f;

        [SerializeField, Min(0f), Tooltip("レベルごとの伸びの係数（C_atk）。")]
        private float attackCoefficient = 2f;

        [SerializeField, Min(0f), Tooltip("伸びの指数（p_atk）。HP とは別に持つ。")]
        private float attackExponent = 1f;

        [Header("その他")]
        [SerializeField, Tooltip("レベルアップで MaxHP が増えたときの現在 HP。増えた分だけ足すか、全回復か。")]
        private LevelUpHpMode levelUpHp = LevelUpHpMode.AddIncrease;

        public LevelUpHpMode LevelUpHp => levelUpHp;

        public GrowthCurve Curve => new GrowthCurve(
            hpBase, hpCoefficient, hpExponent,
            attackBase, attackCoefficient, attackExponent);

        /// <summary>コードから作るとき（テストや仲間の動的生成）用。</summary>
        public void Configure(GrowthCurve curve, LevelUpHpMode hpMode = LevelUpHpMode.AddIncrease)
        {
            hpBase = (float)curve.HpBase;
            hpCoefficient = (float)curve.HpCoefficient;
            hpExponent = (float)curve.HpExponent;
            attackBase = (float)curve.AttackBase;
            attackCoefficient = (float)curve.AttackCoefficient;
            attackExponent = (float)curve.AttackExponent;
            levelUpHp = hpMode;
        }
    }
}
