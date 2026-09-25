namespace TpsDungeon.Player
{
    /// <summary>
    /// ホイールの値を「ひと続きの回しで 1 枠」にまとめる。
    /// Mos などのなめらかスクロールは 1 ノッチを数百ミリ秒の小さな値の列に分けて送ってくるので、
    /// 値が来るたびに 1 枠送ると何枠も飛ぶ。値が途切れずに続いている間は同じ回しとみなし、最初の 1 回だけ送る。
    /// 向きが変わったら、途切れていなくても新しい回しとして送る。
    /// </summary>
    public sealed class ScrollGesture
    {
        /// <summary>これより長く値が途切れたら、次の値は新しい回しとみなす（秒）。</summary>
        public const float DefaultGap = 0.15f;

        private readonly float gap;
        private int lastDirection;
        private float lastTime;

        public ScrollGesture(float gap = DefaultGap)
        {
            this.gap = gap;
        }

        /// <summary>
        /// 毎フレームのホイールの値と時刻を渡すと、送る枠の数（-1 / 0 / 1）を返す。
        /// 値が 0 のフレームも渡してよい（回しの続きとは数えない）。
        /// </summary>
        public int Step(float value, float time)
        {
            int direction = PlayerHotbar.ScrollStep(value);
            if (direction == 0) return 0;

            bool continues = direction == lastDirection && time - lastTime <= gap;
            lastDirection = direction;
            lastTime = time;
            return continues ? 0 : direction;
        }
    }
}
