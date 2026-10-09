using System;
using System.Collections.Generic;

namespace TpsDungeon.Party
{
    /// <summary>
    /// パーティーの並び順の規則。先頭（0 番）が操作しているキャラで、後ろが列の順。
    /// 人数の上限と並べ替えだけを持ち、キャラの切り替えや見た目は扱わない（Party が受けて当てる）。
    /// </summary>
    public sealed class PartyOrder<T> where T : class
    {
        private readonly List<T> members = new List<T>();

        public PartyOrder(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        /// <summary>入れられる人数（先頭を含む）。</summary>
        public int Capacity { get; }

        public int Count => members.Count;

        public bool IsFull => members.Count >= Capacity;

        public T this[int index] => index >= 0 && index < members.Count ? members[index] : null;

        /// <summary>先頭（操作しているキャラ）。居なければ null。</summary>
        public T Leader => this[0];

        public IReadOnlyList<T> Members => members;

        /// <summary>並び（人数・順番）が変わった。</summary>
        public event Action<PartyOrder<T>> Changed;

        /// <summary>先頭が替わった。(前の先頭, 新しい先頭)。Changed の後に飛ぶ。</summary>
        public event Action<T, T> LeaderChanged;

        public int IndexOf(T member) => member != null ? members.IndexOf(member) : -1;

        public bool Contains(T member) => IndexOf(member) >= 0;

        /// <summary>列の最後に入れる。満員・null・もう居るときは入れずに false。</summary>
        public bool Add(T member)
        {
            if (member == null || IsFull || Contains(member)) return false;

            T before = Leader;
            members.Add(member);
            Notify(before);
            return true;
        }

        /// <summary>抜ける。先頭が抜けたら 2 番目が先頭になる。居なければ false。</summary>
        public bool Remove(T member)
        {
            int index = IndexOf(member);
            if (index < 0) return false;

            T before = Leader;
            members.RemoveAt(index);
            Notify(before);
            return true;
        }

        /// <summary>
        /// from 番目の人を抜き出して to 番目に入れる（間の人は 1 つずつずれる）。to を 0 にすればその人が先頭になる。
        /// 範囲外や同じ位置なら何もしない。動いたら true。
        /// </summary>
        public bool Move(int from, int to)
        {
            if (from < 0 || from >= members.Count || to < 0 || to >= members.Count || from == to) return false;

            T before = Leader;
            T moving = members[from];
            members.RemoveAt(from);
            members.Insert(to, moving);
            Notify(before);
            return true;
        }

        private void Notify(T leaderBefore)
        {
            Changed?.Invoke(this);
            if (!ReferenceEquals(Leader, leaderBefore)) LeaderChanged?.Invoke(leaderBefore, Leader);
        }
    }
}
