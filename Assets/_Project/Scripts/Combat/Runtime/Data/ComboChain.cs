using System;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// コンボボーナスの段数（続けて当てた段の数）の数え方。UnityEngine に依存しない。
    /// MeleeComboState の段は 1 周で 1 段目に戻るが、こちらは周をまたいで数え続ける。上限は無い。
    ///
    ///   段を振り終えて 1 体でも当てた → 1 増やす
    ///   段を振り終えて空振り         → 0 に戻す
    ///   コンボが切れて待機に戻った   → <see cref="StartTimeout"/> の秒数のうちに次の段を振り始めなければ 0 に戻す
    ///
    /// 段の 1 撃には、振り始めた時点の <see cref="Count"/> を上乗せの段数として使う（最初の段は 0）。
    /// </summary>
    public sealed class ComboChain
    {
        private float timeout;

        /// <summary>続けて当てた段の数。</summary>
        public int Count { get; private set; }

        /// <summary>時間切れを数えている最中か。</summary>
        public bool IsTimingOut => timeout > 0f;

        /// <summary>段を振り終えた。当てた数が 0 なら途切れる。数が変わったら真。</summary>
        public bool SwingFinished(int hits)
        {
            if (hits > 0)
            {
                Count++;
                return true;
            }

            return Reset();
        }

        /// <summary>seconds 秒のうちに次の段が始まらなければ途切れる。0 以下ならすぐ途切れる。数が変わったら真。</summary>
        public bool StartTimeout(float seconds)
        {
            if (seconds > 0f)
            {
                timeout = seconds;
                return false;
            }

            return Reset();
        }

        /// <summary>次の段が始まった。時間切れを取り消す。</summary>
        public void CancelTimeout() => timeout = 0f;

        /// <summary>時間を deltaTime 秒進める。時間切れで途切れたら真。</summary>
        public bool Tick(float deltaTime)
        {
            if (timeout <= 0f) return false;

            timeout -= Math.Max(0f, deltaTime);
            return timeout <= 0f && Reset();
        }

        /// <summary>0 に戻す（持ち替えたときなど）。数が変わったら真。</summary>
        public bool Reset()
        {
            timeout = 0f;
            if (Count == 0) return false;

            Count = 0;
            return true;
        }
    }
}
