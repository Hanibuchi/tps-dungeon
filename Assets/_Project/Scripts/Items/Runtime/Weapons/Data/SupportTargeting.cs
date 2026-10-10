using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>仲間に掛ける支援（ダメージ軽減・治癒）の相手の候補 1 人。</summary>
    public struct SupportCandidate
    {
        /// <summary>その種類の加護がまだ掛かっていないか。加護は先にこちらから選ぶ。</summary>
        public bool Fresh;

        /// <summary>もう掛かっている相手の残り時間（秒）。Fresh が足りないとき、短い順に掛け直す。</summary>
        public float Remaining;

        /// <summary>最大に対する今の体力の割合（0〜1）と、今の体力。治癒で、割合の低い順（同じなら体力の少ない順）に選ぶ。</summary>
        public float HealthFraction;
        public int Health;
    }

    /// <summary>
    /// 支援を掛ける相手を選ぶ。UnityEngine に依存しない。
    ///   加護（<see cref="Pick"/>）… まだ掛かっていない候補からランダムに count 人選び、足りなければ（refill のとき）もう掛かっている候補を残り時間の短い順に足す
    ///   治癒（<see cref="PickMostHurt"/>）… 体力の割合の低い順（同じなら体力の少ない順）に count 人選ぶ。満タンの人も候補に入る
    /// </summary>
    public static class SupportTargeting
    {
        /// <summary>選んだ候補の番号（candidates の添え字）を results に入れる。前の中身は消す。</summary>
        public static void Pick(IReadOnlyList<SupportCandidate> candidates, int count, bool refill, Random random, List<int> results)
        {
            results.Clear();
            if (candidates == null || count <= 0) return;

            var fresh = new List<int>();
            var stale = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Fresh) fresh.Add(i);
                else stale.Add(i);
            }

            // 部分的なフィッシャー・イェーツで、まだの候補から count 人をランダムに取る。
            for (int i = 0; i < fresh.Count && results.Count < count; i++)
            {
                int j = random != null ? random.Next(i, fresh.Count) : i;
                (fresh[i], fresh[j]) = (fresh[j], fresh[i]);
                results.Add(fresh[i]);
            }

            if (!refill || results.Count >= count) return;

            stale.Sort((a, b) => candidates[a].Remaining.CompareTo(candidates[b].Remaining));
            for (int i = 0; i < stale.Count && results.Count < count; i++) results.Add(stale[i]);
        }

        /// <summary>体力の割合の低い順（同じなら体力の少ない順、それも同じなら並び順）に count 人の番号を results に入れる。前の中身は消す。</summary>
        public static void PickMostHurt(IReadOnlyList<SupportCandidate> candidates, int count, List<int> results)
        {
            results.Clear();
            if (candidates == null || count <= 0) return;

            for (int i = 0; i < candidates.Count; i++) results.Add(i);
            results.Sort((a, b) =>
            {
                int byFraction = candidates[a].HealthFraction.CompareTo(candidates[b].HealthFraction);
                if (byFraction != 0) return byFraction;
                int byHealth = candidates[a].Health.CompareTo(candidates[b].Health);
                return byHealth != 0 ? byHealth : a.CompareTo(b);
            });
            if (results.Count > count) results.RemoveRange(count, results.Count - count);
        }
    }
}
