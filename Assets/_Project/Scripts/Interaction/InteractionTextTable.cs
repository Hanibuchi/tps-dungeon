using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// インタラクトできる場所の文字の表。Settings/Tables/InteractionTexts.csv をエディタが取り込んで中身を入れる（InteractionTextCsvImporter）。
    /// Resources に置き、実行中は <see cref="InteractionTexts"/> から ID で引く。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Interaction Text Table", fileName = "InteractionTexts")]
    public sealed class InteractionTextTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            [Tooltip("通れるとき・触れるときの案内。")]
            public string prompt;
            [Tooltip("閉じているときの案内。空なら閉じている間は触れない。")]
            public string closedPrompt;
            [Tooltip("情報欄の見出し。")]
            public string title;
            [TextArea, Tooltip("情報欄の本文。")]
            public string description;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>中身を差し替える（取り込みから）。</summary>
        public void SetEntries(List<Entry> value)
        {
            entries = value ?? new List<Entry>();
            InteractionTexts.Invalidate();
        }

        private void OnValidate() => InteractionTexts.Invalidate();
    }

    /// <summary>
    /// インタラクトできる場所の文字を ID で引く窓口。表（<see cref="InteractionTextTable"/>）は Resources から 1 度だけ読む。
    /// ID が空か、表にその ID の行が無ければ見つからない（呼ぶ側は自分の持っている文字を使う）。
    /// </summary>
    public static class InteractionTexts
    {
        public const string ResourcePath = "Interaction/InteractionTexts";

        private static Dictionary<string, InteractionTextTable.Entry> byId;

        public static bool TryGet(string id, out InteractionTextTable.Entry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(id)) return false;
            if (byId == null) Build();
            return byId.TryGetValue(id, out entry);
        }

        /// <summary>表が書き換わった。次に引くときに読み直す。</summary>
        public static void Invalidate() => byId = null;

        /// <summary>
        /// 表を読んで辞書を作る。エディタでは初めて読み込んだときに表の OnValidate が呼ばれて <see cref="Invalidate"/> が走るので、
        /// 手元で作り終えてから入れる（先に byId へ入れておくと、読み込みの途中で null に戻されて落ちる）。
        /// </summary>
        private static void Build()
        {
            var built = new Dictionary<string, InteractionTextTable.Entry>(StringComparer.Ordinal);
            var table = Resources.Load<InteractionTextTable>(ResourcePath);
            if (table != null)
            {
                foreach (var entry in table.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.id)) built[entry.id] = entry;
                }
            }
            byId = built;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => byId = null;
    }
}
