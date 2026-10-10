using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>仲間に掛ける支援（ダメージ軽減・治癒）の相手の候補 1 人。</summary>
    public struct SupportCandidate
    {
        /// <summary>まだ掛かっていない（加護が無い・体力が減っている）か。先にこちらから選ぶ。</summary>
        public bool Fresh;

        /// <summary>もう掛かっている相手の残り時間（秒）。Fresh が足りないとき、短い順に掛け直す。</summary>
        public float Remaining;
    }

    /// <summary>
    /// 支援を掛ける相手を選ぶ。UnityEngine に依存しない。
    /// まだ掛かっていない候補からランダムに count 人選び、足りなければ（refill のとき）もう掛かっている候補を残り時間の短い順に足す。
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
    }
}
