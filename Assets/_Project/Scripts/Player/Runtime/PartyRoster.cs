using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// 今のパーティーの並び（先頭が操作しているキャラ）を、パーティーの仕組み（TpsDungeon.Party）に依存せずに読むための窓口。
    /// 並びを決めるのは Party で、ここは写しを持って知らせるだけ。HUD・画面・装備などはここから先頭を読む。
    /// パーティーが居ないシーン（1 人だけのテストなど）では空のまま。
    /// </summary>
    public static class PartyRoster
    {
        private static readonly List<GameObject> members = new List<GameObject>();

        /// <summary>並び順のメンバー。先頭が操作しているキャラ。</summary>
        public static IReadOnlyList<GameObject> Members => members;

        /// <summary>操作しているキャラ。居なければ null。</summary>
        public static GameObject Leader => members.Count > 0 ? members[0] : null;

        /// <summary>並び（人数・順番）が変わった。</summary>
        public static event Action Changed;

        /// <summary>先頭が替わった（並びの変化の後に飛ぶ）。</summary>
        public static event Action<GameObject> LeaderChanged;

        /// <summary>go がパーティーの先頭か。パーティーが無いとき（並びが空）は、1 人で操作しているとみなして真。</summary>
        public static bool IsLeader(GameObject go) => members.Count == 0 || members[0] == go;

        /// <summary>並びを差し替えて知らせる。Party が呼ぶ。</summary>
        public static void Set(IEnumerable<GameObject> order)
        {
            GameObject before = Leader;
            members.Clear();
            if (order != null)
            {
                foreach (GameObject go in order)
                {
                    if (go != null) members.Add(go);
                }
            }

            Changed?.Invoke();
            if (Leader != before) LeaderChanged?.Invoke(Leader);
        }

        /// <summary>並びを空にする。Party が消えるときに呼ぶ。</summary>
        public static void Clear() => Set(null);

        // ドメインリロードを切っているので、再生のたびに前回の並びを捨てる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            members.Clear();
            Changed = null;
            LeaderChanged = null;
        }
    }
}
