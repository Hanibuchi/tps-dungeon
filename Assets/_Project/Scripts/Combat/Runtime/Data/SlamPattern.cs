using System;

namespace TpsDungeon.Combat
{
    /// <summary>追撃 1 回の、本撃から見た位置と時刻。</summary>
    public readonly struct SlamFollowUp
    {
        /// <summary>着弾点から前へずらす距離（m）。</summary>
        public readonly float ForwardOffset;

        /// <summary>本撃から落ちるまでの秒数。</summary>
        public readonly float Delay;

        public SlamFollowUp(float forwardOffset, float delay)
        {
            ForwardOffset = forwardOffset;
            Delay = delay;
        }
    }

    /// <summary>
    /// 叩きつけ（Slam の段）から出る衝撃波と追撃の並べ方。UnityEngine に依存しない。
    ///
    ///   衝撃波（数） … count 本を、前（0°）を中心に spacing 度ずつ左右対称に広げる。1 本なら真っ直ぐ前、2 本なら ±spacing/2
    ///   追撃（多重） … k 回目（1 始まり）は前へ k × spacing m、本撃から k × interval 秒あと
    /// </summary>
    public static class SlamPattern
    {
        /// <summary>衝撃波の向き。前からの水平の角度（度、右が正）を count 個。count が 0 以下なら空。</summary>
        public static float[] ShockwaveAngles(int count, float spacingDegrees)
        {
            if (count <= 0) return Array.Empty<float>();

            var angles = new float[count];
            float middle = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++) angles[i] = (i - middle) * spacingDegrees;
            return angles;
        }

        /// <summary>追撃の位置と時刻を count 回分。count が 0 以下なら空。</summary>
        public static SlamFollowUp[] FollowUps(int count, float spacing, float interval)
        {
            if (count <= 0) return Array.Empty<SlamFollowUp>();

            var result = new SlamFollowUp[count];
            for (int k = 1; k <= count; k++) result[k - 1] = new SlamFollowUp(k * Math.Max(0f, spacing), k * Math.Max(0f, interval));
            return result;
        }
    }
}
