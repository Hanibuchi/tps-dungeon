using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 1 キャラ分のレベルと経験値。Exp は「今のレベルに入ってから貯めた分」で、ExpToNext に届くと次のレベルに繰り上がる。
    /// UnityEngine に依存しない。ステータスへの反映は CharacterProgression が受け持つ。
    /// </summary>
    public sealed class LevelProgress
    {
        public LevelProgress(GrowthCurve curve)
        {
            Curve = curve;
        }

        public GrowthCurve Curve { get; }

        public int Level { get; private set; } = GrowthCurve.MinLevel;

        /// <summary>今のレベルに入ってから貯めた経験値。最大レベルでは常に 0。</summary>
        public int Exp { get; private set; }

        /// <summary>次のレベルまでに要る経験値（今のレベル内の分は引いていない）。最大レベルでは 0。</summary>
        public int ExpToNext => Curve.ExpToNext(Level);

        public bool IsMaxLevel => Level >= Curve.MaxLevel;

        /// <summary>Lv1・経験値 0 から数えた累計。</summary>
        public long TotalExp => Curve.TotalExpToReach(Level) + Exp;

        /// <summary>レベルか経験値が変わった（獲得・リセット・復元のどれでも）。</summary>
        public event Action<LevelProgress> ExpChanged;

        /// <summary>獲得でレベルが上がった。1 回の獲得で何レベル上がっても 1 回だけ、(from, to) で知らせる。ExpChanged より先に飛ぶ。</summary>
        public event Action<LevelProgress, int, int> LeveledUp;

        /// <summary>
        /// 経験値を足す。届いた分だけ続けてレベルを上げ、余りは次のレベルに持ち越す。
        /// 最大レベルに着いたら余りは捨て、以後の獲得は受け付けない。上がったレベル数を返す。
        /// </summary>
        public int AddExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;

            int from = Level;
            long exp = (long)Exp + amount;
            while (!IsMaxLevel)
            {
                int need = ExpToNext;
                if (exp < need) break;
                exp -= need;
                Level++;
            }
            if (IsMaxLevel) exp = 0;
            Exp = (int)exp;

            if (Level != from) LeveledUp?.Invoke(this, from, Level);
            ExpChanged?.Invoke(this);
            return Level - from;
        }

        /// <summary>Lv1・経験値 0 に戻す（ゲームオーバー）。</summary>
        public void Reset()
        {
            Level = GrowthCurve.MinLevel;
            Exp = 0;
            ExpChanged?.Invoke(this);
        }

        /// <summary>保存した値に戻す。範囲外は丸める（レベルは 1〜最大、経験値は 0〜次に届く手前）。</summary>
        public void Restore(int level, int exp)
        {
            Level = Curve.ClampLevel(level);
            Exp = IsMaxLevel ? 0 : Math.Min(Math.Max(exp, 0), ExpToNext - 1);
            ExpChanged?.Invoke(this);
        }
    }
}
