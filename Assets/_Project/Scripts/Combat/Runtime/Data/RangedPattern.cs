using System;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 遠距離武器の矢と雨の並べ方。UnityEngine に依存しない。
    ///
    ///   矢（弓の数）     … 本撃と増えた分を、狙いを中心に左右対称の扇に並べる（叩きつけと同じ <see cref="SlamPattern.SpreadAngles"/>）
    ///   一斉射（弓の多重） … k 回目（1 始まり）は本撃から k × interval 秒あと、同じ向きへ
    ///   雨（持続弓の数）  … 1 つ目は狙った所。増えた分は狙った所から distance の円周に、撃った向き（前）から等間隔に並べる
    ///   雨（持続弓の多重） … 同じ所に、k 回目は本撃から k × interval 秒あとに降り始める
    /// </summary>
    public static class RangedPattern
    {
        /// <summary>
        /// 雨の置き場所を count 個。撃った向きを +Z とした水平のずれ（右が +X、m）。
        /// 0 番は狙った所（ずれ無し）。1 番からの count - 1 個は半径 distance の円周に、前から右回りに等間隔。count が 0 以下なら空。
        /// </summary>
        public static (float x, float z)[] RainOffsets(int count, float distance)
        {
            if (count <= 0) return Array.Empty<(float, float)>();

            var result = new (float x, float z)[count];
            int ring = count - 1;
            float radius = Math.Max(0f, distance);
            for (int i = 0; i < ring; i++)
            {
                double angle = 2.0 * Math.PI * i / ring;
                result[i + 1] = ((float)(Math.Sin(angle) * radius), (float)(Math.Cos(angle) * radius));
            }

            return result;
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
