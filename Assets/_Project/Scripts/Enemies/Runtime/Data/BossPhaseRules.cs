using System;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// ボスの段階の決まり。UnityEngine に依存しない。
    ///   段階 1 … 残り HP の割合が phase2 より上
    ///   段階 2 … phase2 以下で phase3 より上
    ///   段階 3 … phase3 以下
    /// 段階は下がらない（回復しても戻らない）ので、呼ぶ側は今の段階との大きい方を取る。
    /// </summary>
    public static class BossPhaseRules
    {
        public const int MaxStage = 3;

        /// <summary>残り HP の割合（0〜1）から段階（1〜3）を決める。</summary>
        public static int StageFor(float hpFraction, float phase2, float phase3)
        {
            if (hpFraction <= phase3) return 3;
            if (hpFraction <= phase2) return 2;
            return 1;
        }

        /// <summary>段階のテンポ（攻撃の間・溜めを割り、歩く速さに掛ける倍率）。段階 1 は 1。</summary>
        public static float TempoFor(int stage, float tempo2, float tempo3)
        {
            float tempo = stage >= 3 ? tempo3 : stage == 2 ? tempo2 : 1f;
            return Math.Max(0.1f, tempo);
        }
    }
}
