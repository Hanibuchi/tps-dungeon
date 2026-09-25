using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// なめらかスクロールのホイールの値を、1 ノッチにつき 1 枠にまとめる。
    /// Mos などは 1 ノッチを「0.05 から上がって山を作り、また下がる」数百ミリ秒の値の列にして送ってくる。
    /// 続けて回すと前の山が下がりきる前に次の山が始まり、値は途切れない。
    /// そこで、回し始めに 1 枠送り、その後は値が一度下がってから再び上がりに転じるたびに 1 枠送る。
    /// 向きが変わったときと、値がしばらく途切れた後は、新しい回し始めとしてすぐ送る。
    /// </summary>
    public sealed class ScrollGesture
    {
        /// <summary>これより長く値が途切れたら、次の値は新しい回し始めとみなす（秒）。</summary>
        public const float DefaultGap = 0.15f;

        // 値は 0.05 刻みで届くので、半刻みより小さい上下は揺れとみなす。
        private const float Tolerance = 0.025f;

        // 谷から次の山とみなすまでの上がり幅。谷が深いほど少しの上がりで足りる。
        private const float RiseBase = 0.075f;
        private const float RiseRatio = 0.15f;

        private readonly float gap;
        private readonly float[] recent = new float[3];
        private int recentCount;
        private int lastDirection;
        private float lastTime;
        private float peak;
        private float trough;
        private bool falling;

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

            float magnitude = Mathf.Abs(value);
            bool continues = direction == lastDirection && time - lastTime <= gap;
            lastDirection = direction;
            lastTime = time;

            if (!continues)
            {
                recent[0] = magnitude;
                recentCount = 1;
                peak = magnitude;
                falling = false;
                return direction;
            }

            float level = Smooth(magnitude);
            if (!falling)
            {
                if (level > peak) peak = level;
                else if (level < peak - Tolerance)
                {
                    falling = true;
                    trough = level;
                }

                return 0;
            }

            if (level < trough)
            {
                trough = level;
                return 0;
            }

            if (level < trough + RiseBase + RiseRatio * trough - 1e-4f) return 0;

            // 谷から上がりに転じた。次のノッチの山が始まっている。
            peak = level;
            falling = false;
            return direction;
        }

        /// <summary>
        /// 直近 3 つの値の中央値。重いフレームに 2 回分がまとめて届くと 1 つだけ跳ねるので、それを消す。
        /// 2 つしか無いうちは小さいほうを使う。
        /// </summary>
        private float Smooth(float magnitude)
        {
            if (recentCount < recent.Length) recent[recentCount++] = magnitude;
            else
            {
                recent[0] = recent[1];
                recent[1] = recent[2];
                recent[2] = magnitude;
            }

            if (recentCount == 2) return Mathf.Min(recent[0], recent[1]);

            float a = recent[0], b = recent[1], c = recent[2];
            return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
        }
    }
}
