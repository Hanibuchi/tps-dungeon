using System;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 遠距離武器の矢と雨の並べ方。UnityEngine に依存しない。
    ///
    ///   矢（弓の数）     … 1 本目は必ず照準へ。増えた分は照準の右・左・右に 2 つ目…と交互に spacing 度ずつ開く（偶数でも照準へ 1 本飛ぶ）
    ///   一斉射（弓の多重） … k 回目（1 始まり）は本撃から k × interval 秒あと、同じ向きへ
    ///   雨（持続弓の数）  … 1 つ目は必ず狙った所。増えた分は狙った所の付近（min〜max の距離、向きはランダム）
    ///   雨（持続弓の多重） … 同じ所に、k 回目は本撃から k × interval 秒あとに降り始める
    /// </summary>
    public static class RangedPattern
    {
        /// <summary>
        /// 矢の向き。照準からの水平の角度（度、右が正）を count 個。0, +s, −s, +2s, −2s, … の順。count が 0 以下なら空。
        /// </summary>
        public static float[] VolleyAngles(int count, float spacingDegrees)
        {
            if (count <= 0) return Array.Empty<float>();

            var angles = new float[count];
            for (int i = 1; i < count; i++)
            {
                int step = (i + 1) / 2;
                angles[i] = (i % 2 == 1 ? step : -step) * spacingDegrees;
            }

            return angles;
        }

        /// <summary>
        /// 雨の置き場所を count 個。水平のずれ（m）。0 番は狙った所（ずれ無し）。
        /// 1 番からは向きを一様に、距離を minDistance〜maxDistance の一様に引く。count が 0 以下なら空。
        /// </summary>
        public static (float x, float z)[] RainOffsets(int count, float minDistance, float maxDistance, Random random)
        {
            if (count <= 0) return Array.Empty<(float, float)>();

            var result = new (float x, float z)[count];
            for (int i = 1; i < count; i++) result[i] = RainOffset(minDistance, maxDistance, random);
            return result;
        }

        /// <summary>狙った所の付近の 1 か所。向きは一様、距離は minDistance〜maxDistance の一様。</summary>
        public static (float x, float z) RainOffset(float minDistance, float maxDistance, Random random)
        {
            float min = Math.Max(0f, Math.Min(minDistance, maxDistance));
            float max = Math.Max(min, maxDistance);
            double angle = 2.0 * Math.PI * random.NextDouble();
            double distance = min + (max - min) * random.NextDouble();
            return ((float)(Math.Sin(angle) * distance), (float)(Math.Cos(angle) * distance));
        }

        /// <summary>本撃と、多重で遅れて出す回数ぶんの遅れ（秒）。0 番は 0。repeats が負なら 0 として扱う。</summary>
        public static float[] RepeatDelays(int repeats, float interval)
        {
            int count = Math.Max(0, repeats) + 1;
            var result = new float[count];
            for (int k = 0; k < count; k++) result[k] = k * Math.Max(0f, interval);
            return result;
        }
    }
}
