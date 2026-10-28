using System;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// カカシ（TrainingDummy）が受けたダメージから DPS を出す。UnityEngine に依存しない。時刻は呼ぶ側が渡す（秒）。
    ///
    /// 最初に当たってから、idleReset 秒より長く当たらない間が空くまでを 1 回の計測とする。間が空いたあとの当たりで計測をやり直す。
    ///   計測中   … DPS = 合計 ÷ (今 − 最初の当たり)
    ///   止めた後 … DPS = 合計 ÷ (最後の当たり − 最初の当たり ＋ 当たりの平均の間隔)
    ///             （最後の 1 撃のあとの 1 間隔ぶんも数える。繰り返し当てたときの 1 周の平均と揃うように）
    /// 当たりが 1 回だけで止めたときは間隔が分からないので DPS は 0。
    /// 直近の DPS は、今から window 秒前までに受けた量 ÷ window（計測をまたいでも数える）。
    /// </summary>
    public sealed class DpsMeter
    {
        private const int RecentCapacity = 512;

        // 直近の当たり（輪の形で古い物から上書き）。
        private readonly float[] recentTimes = new float[RecentCapacity];
        private readonly int[] recentAmounts = new int[RecentCapacity];
        private int recentStart;
        private int recentCount;

        public DpsMeter(float idleReset = 3f, float window = 5f)
        {
            IdleReset = Math.Max(0.1f, idleReset);
            Window = Math.Max(0.1f, window);
        }

        /// <summary>これより長く当たらなければ計測を止める（秒）。</summary>
        public float IdleReset { get; set; }

        /// <summary>直近の DPS を数える長さ（秒）。</summary>
        public float Window { get; set; }

        /// <summary>今の計測の合計ダメージ。</summary>
        public long Total { get; private set; }

        /// <summary>今の計測で当たった回数。</summary>
        public int Hits { get; private set; }

        /// <summary>今の計測でクリティカルだった回数。</summary>
        public int Criticals { get; private set; }

        /// <summary>今の計測でいちばん大きかった 1 撃。</summary>
        public int MaxHit { get; private set; }

        /// <summary>最後に受けた 1 撃。</summary>
        public int LastHit { get; private set; }

        public float FirstHitTime { get; private set; }
        public float LastHitTime { get; private set; }

        /// <summary>1 回でも当たったか。</summary>
        public bool HasHits => Hits > 0;

        /// <summary>今まだ計測しているか（最後の当たりから idleReset 秒以内）。</summary>
        public bool IsMeasuring(float now) => HasHits && now - LastHitTime <= IdleReset;

        public void Record(float time, int amount, bool critical = false)
        {
            if (amount <= 0) return;

            if (!IsMeasuring(time)) StartSession(time);
            Total += amount;
            Hits++;
            if (critical) Criticals++;
            if (amount > MaxHit) MaxHit = amount;
            LastHit = amount;
            LastHitTime = time;

            int at = (recentStart + recentCount) % RecentCapacity;
            if (recentCount == RecentCapacity) recentStart = (recentStart + 1) % RecentCapacity;
            else recentCount++;
            recentTimes[at] = time;
            recentAmounts[at] = amount;
        }

        /// <summary>計測の長さ（秒）。<see cref="DpsMeter"/> の説明の分母。</summary>
        public float Elapsed(float now)
        {
            if (!HasHits) return 0f;
            if (IsMeasuring(now)) return Math.Max(0f, now - FirstHitTime);
            if (Hits < 2) return 0f;

            float span = LastHitTime - FirstHitTime;
            return span + span / (Hits - 1);
        }

        /// <summary>今の計測の DPS。</summary>
        public float Dps(float now)
        {
            float elapsed = Elapsed(now);
            return elapsed > 1e-3f ? (float)(Total / (double)elapsed) : 0f;
        }

        /// <summary>直近 window 秒の DPS。</summary>
        public float RecentDps(float now)
        {
            long sum = 0;
            for (int i = 0; i < recentCount; i++)
            {
                int at = (recentStart + i) % RecentCapacity;
                float age = now - recentTimes[at];
                if (age >= 0f && age <= Window) sum += recentAmounts[at];
            }

            return (float)(sum / (double)Window);
        }

        /// <summary>計測を捨てる。次の当たりから数え直す。</summary>
        public void Reset()
        {
            Total = 0;
            Hits = 0;
            Criticals = 0;
            MaxHit = 0;
            LastHit = 0;
            FirstHitTime = 0f;
            LastHitTime = 0f;
            recentStart = 0;
            recentCount = 0;
        }

        private void StartSession(float time)
        {
            Total = 0;
            Hits = 0;
            Criticals = 0;
            MaxHit = 0;
            FirstHitTime = time;
        }
    }
}
