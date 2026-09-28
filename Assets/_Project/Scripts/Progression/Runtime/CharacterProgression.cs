using System;
using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// キャラ 1 人分のレベル・経験値と、そこから決まる MaxHP・基礎攻撃力。主人公にも仲間にも同じものを付ける。
    /// MaxHP は同じ GameObject の <see cref="IHealthPool"/>（主人公なら PlayerHealth）に書き込むので、HP の表示はそちらの通知で追従する。
    /// 経験値はふつう PartyProgression.GrantExp から全員に同じ量が配られる（倍率はそこで掛かる）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Progression")]
    public sealed class CharacterProgression : MonoBehaviour
    {
        public const string PlayerId = "player";

        [SerializeField, Tooltip("成長パラメータ。主人公用・仲間ごとに別のアセットを割り当てる。未設定なら仮の初期値。")]
        private CharacterGrowthProfile profile;

        [SerializeField, Tooltip("保存データでこのキャラを見分ける名前。主人公は \"player\"。仲間ごとに重ならないようにする。")]
        private string progressId = PlayerId;

        private LevelProgress progress;
        private IHealthPool health;
        private ProgressionModifiers modifiers;
        private int maxHp;
        private float baseAttack;

        public string ProgressId => progressId;
        public CharacterGrowthProfile Profile => profile;

        public int Level => Progress.Level;
        public int Exp => Progress.Exp;
        public int ExpToNext => Progress.ExpToNext;
        public bool IsMaxLevel => Progress.IsMaxLevel;
        public long TotalExp => Progress.TotalExp;

        /// <summary>補正込みの MaxHP。</summary>
        public int MaxHp
        {
            get { EnsureInitialized(); return maxHp; }
        }

        /// <summary>補正込みの基礎攻撃力。</summary>
        public float BaseAttack
        {
            get { EnsureInitialized(); return baseAttack; }
        }

        /// <summary>今の現在 HP。体力を持たないキャラなら MaxHP。</summary>
        public int CurrentHp => Health != null ? Health.CurrentHp : MaxHp;

        public event Action<CharacterProgression> ExpChanged;

        /// <summary>(自分, 上がる前, 上がった後)。1 回の獲得で複数レベル上がっても 1 回。</summary>
        public event Action<CharacterProgression, int, int> LeveledUp;

        /// <summary>(自分, 前の MaxHP, 新しい MaxHP)。</summary>
        public event Action<CharacterProgression, int, int> MaxHpChanged;

        public event Action<CharacterProgression> BaseAttackChanged;

        private LevelProgress Progress
        {
            get { EnsureInitialized(); return progress; }
        }

        private IHealthPool Health
        {
            get
            {
                if (health == null) health = GetComponent<IHealthPool>();
                return health;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            PartyProgression.RegisterToCurrent(this);
        }

        private void OnDisable()
        {
            if (PartyProgression.Current != null) PartyProgression.Current.Unregister(this);
        }

        /// <summary>
        /// 成長パラメータと ID を差し替えて Lv1 からやり直す。仲間をコードで生成するときやテスト用。
        /// </summary>
        public void Configure(CharacterGrowthProfile newProfile, string newProgressId = null)
        {
            profile = newProfile;
            if (!string.IsNullOrEmpty(newProgressId)) progressId = newProgressId;
            progress = null;
            EnsureInitialized();
        }

        /// <summary>ダメージ計算から参照する基礎攻撃力。武器の攻撃力との合成はダメージ計算側で決める。</summary>
        public float GetBaseAttack() => BaseAttack;

        /// <summary>倍率を掛けずにそのまま経験値を足す。上がったレベル数を返す。ふつうは PartyProgression.GrantExp を使う。</summary>
        public int AddExp(int amount) => Progress.AddExp(amount);

        /// <summary>ちょうど levels レベル分の経験値を足す（デバッグ用）。最大レベルで止まる。</summary>
        public int AddLevels(int levels)
        {
            int from = Level;
            for (int i = 0; i < levels && !IsMaxLevel; i++) Progress.AddExp(ExpToNext - Exp);
            return Level - from;
        }

        /// <summary>Lv1・経験値 0・HP 満タンに戻す（ゲームオーバー）。</summary>
        public void ResetProgress()
        {
            Progress.Reset();
            ApplyStats(HpUpdate.Fill);
        }

        public CharacterProgressSaveData Capture()
        {
            return new CharacterProgressSaveData
            {
                id = progressId,
                level = Level,
                exp = Exp,
                currentHp = Health != null ? Health.CurrentHp : -1,
            };
        }

        /// <summary>保存データのレベル・経験値・現在 HP に戻す。ID は見ない（照合は PartyProgression が済ませる）。</summary>
        public void Restore(CharacterProgressSaveData data)
        {
            if (data == null) return;

            Progress.Restore(data.level, data.exp);
            ApplyStats(HpUpdate.Fill);
            if (Health != null && data.currentHp >= 0) Health.SetMaxAndCurrent(maxHp, data.currentHp);
        }

        /// <summary>パーティの補正を受ける。null で補正なしに戻す。PartyProgression が登録時に呼ぶ。</summary>
        public void SetModifiers(ProgressionModifiers value)
        {
            EnsureInitialized();
            if (modifiers == value) return;

            if (modifiers != null) modifiers.Changed -= OnModifiersChanged;
            modifiers = value;
            if (modifiers != null) modifiers.Changed += OnModifiersChanged;
            ApplyStats(HpUpdate.AddIncrease);
        }

        private void EnsureInitialized()
        {
            if (progress != null) return;

            progress = new LevelProgress(profile != null ? profile.Curve : GrowthCurve.Default);
            progress.LeveledUp += OnLeveledUp;
            progress.ExpChanged += OnExpChanged;
            maxHp = 0;
            ApplyStats(HpUpdate.Fill);
        }

        private void OnLeveledUp(LevelProgress _, int from, int to)
        {
            var mode = profile != null ? profile.LevelUpHp : LevelUpHpMode.AddIncrease;
            ApplyStats(mode == LevelUpHpMode.FullHeal ? HpUpdate.Fill : HpUpdate.AddIncrease);
            LeveledUp?.Invoke(this, from, to);
        }

        private void OnExpChanged(LevelProgress _) => ExpChanged?.Invoke(this);

        private void OnModifiersChanged(ProgressionModifiers _) => ApplyStats(HpUpdate.AddIncrease);

        private enum HpUpdate
        {
            /// <summary>MaxHP の増えた分だけ現在 HP を足す（減ったら最大に収めるだけ）。</summary>
            AddIncrease,

            /// <summary>現在 HP を最大にする。</summary>
            Fill,
        }

        /// <summary>今のレベルと補正から MaxHP・基礎攻撃力を出し直し、体力に書き込む。</summary>
        private void ApplyStats(HpUpdate hpUpdate)
        {
            int level = progress.Level;
            var curve = progress.Curve;
            StatModifier hpModifier = modifiers != null ? modifiers.MaxHp : StatModifier.None;
            StatModifier attackModifier = modifiers != null ? modifiers.BaseAttack : StatModifier.None;

            int oldMax = maxHp;
            int newMax = Mathf.Max(1, (int)Math.Round(hpModifier.Apply(curve.MaxHp(level)), MidpointRounding.AwayFromZero));
            float oldAttack = baseAttack;
            float newAttack = (float)attackModifier.Apply(curve.BaseAttack(level));

            maxHp = newMax;
            baseAttack = newAttack;

            if (Health != null)
            {
                int current = hpUpdate == HpUpdate.Fill
                    ? newMax
                    : Health.CurrentHp + Mathf.Max(0, newMax - Health.MaxHp);
                Health.SetMaxAndCurrent(newMax, current);
            }

            // 初期化の途中（oldMax == 0）は誰も購読していないので知らせない。
            if (oldMax != 0 && oldMax != newMax) MaxHpChanged?.Invoke(this, oldMax, newMax);
            if (oldMax != 0 && !oldAttack.Equals(newAttack)) BaseAttackChanged?.Invoke(this);
        }
    }
}
