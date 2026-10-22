using System;
using System.Collections.Generic;

namespace TpsDungeon.Progression
{
    /// <summary>強化を買えるか。</summary>
    public enum SoulUpgradeCheck
    {
        /// <summary>買える。</summary>
        Ok,
        /// <summary>表に無い。</summary>
        Unknown,
        /// <summary>前の強化をまだ解放していないので見えない。</summary>
        Hidden,
        /// <summary>もう解放してある。</summary>
        Owned,
        /// <summary>ソウルが足りない。</summary>
        NotEnough,
    }

    /// <summary>
    /// 魂の強化の木。UnityEngine に依存しない。根（parent が空の行）が 1 つあり、ほかはどれも前の強化（parent）を辿ると根に着く。
    /// - 見えるのは、根と、前の強化を解放してある強化だけ（<see cref="IsVisible"/>）。まだ見えない強化は画面に出さない。
    /// - 買えるのは、見えていて、まだ解放しておらず、ソウルが足りる強化（<see cref="Check"/>）。
    /// - 効果の量は、解放した強化のうち同じ効果の値を足したもの（<see cref="Total"/>）。
    /// 表がおかしいとき（根が無い・2 つ以上・前の強化が無い・輪になっている）は、根まで辿れない行を捨ててエラーに積む。
    /// </summary>
    public sealed class SoulUpgradeTree
    {
        private readonly List<SoulUpgradeNode> nodes = new List<SoulUpgradeNode>();
        private readonly Dictionary<string, int> indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> depth = new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly List<string> NoChildren = new List<string>();

        public SoulUpgradeTree(IEnumerable<SoulUpgradeNode> source, List<string> errors = null)
        {
            var all = new Dictionary<string, SoulUpgradeNode>(StringComparer.Ordinal);
            var order = new List<string>();
            if (source != null)
            {
                foreach (SoulUpgradeNode node in source)
                {
                    if (string.IsNullOrEmpty(node.id) || all.ContainsKey(node.id)) continue;
                    all[node.id] = node;
                    order.Add(node.id);
                }
            }

            string root = null;
            foreach (string id in order)
            {
                if (!string.IsNullOrEmpty(all[id].parent)) continue;
                if (root == null) root = id;
                else errors?.Add($"根（parent が空の行）が 2 つ以上ある。「{root}」を根にして「{id}」は捨てる");
            }
            if (root == null)
            {
                if (order.Count > 0) errors?.Add("根（parent が空の行）が無い");
                return;
            }

            // 根から順に、親が入ったものを入れていく。表の並びは保つ。
            depth[root] = 0;
            bool added = true;
            while (added)
            {
                added = false;
                foreach (string id in order)
                {
                    if (depth.ContainsKey(id)) continue;
                    string parent = all[id].parent;
                    if (parent == null || !depth.TryGetValue(parent, out int d)) continue;
                    depth[id] = d + 1;
                    added = true;
                }
            }

            foreach (string id in order)
            {
                if (!depth.ContainsKey(id))
                {
                    SoulUpgradeNode lost = all[id];
                    errors?.Add(all.ContainsKey(lost.parent)
                        ? $"「{id}」は前の強化を辿っても根に着かない（輪になっている）"
                        : $"「{id}」の前の強化「{lost.parent}」が表に無い");
                    continue;
                }

                indexById[id] = nodes.Count;
                nodes.Add(all[id]);
                string parent = all[id].parent;
                if (string.IsNullOrEmpty(parent)) continue;
                if (!children.TryGetValue(parent, out List<string> list)) children[parent] = list = new List<string>();
                list.Add(id);
            }

            Root = root;
        }

        /// <summary>根（真ん中の強化）の id。表が空なら null。</summary>
        public string Root { get; }

        /// <summary>全部の強化（表の並び）。</summary>
        public IReadOnlyList<SoulUpgradeNode> Nodes => nodes;

        public int Count => nodes.Count;

        public bool Contains(string id) => id != null && indexById.ContainsKey(id);

        public bool TryGet(string id, out SoulUpgradeNode node)
        {
            node = default;
            if (id == null || !indexById.TryGetValue(id, out int index)) return false;
            node = nodes[index];
            return true;
        }

        /// <summary>id のすぐ先の強化（表の並び）。</summary>
        public IReadOnlyList<string> Children(string id) => id != null && children.TryGetValue(id, out List<string> list) ? list : NoChildren;

        /// <summary>根からの段数（根が 0）。表に無ければ -1。</summary>
        public int Depth(string id) => id != null && depth.TryGetValue(id, out int d) && indexById.ContainsKey(id) ? d : -1;

        /// <summary>画面に出すか。根と、前の強化を解放してある強化。</summary>
        public bool IsVisible(string id, ICollection<string> purchased)
        {
            if (!TryGet(id, out SoulUpgradeNode node)) return false;
            return string.IsNullOrEmpty(node.parent) || (purchased != null && purchased.Contains(node.parent));
        }

        public SoulUpgradeCheck Check(string id, ICollection<string> purchased, long souls)
        {
            if (!TryGet(id, out SoulUpgradeNode node)) return SoulUpgradeCheck.Unknown;
            if (purchased != null && purchased.Contains(id)) return SoulUpgradeCheck.Owned;
            if (!IsVisible(id, purchased)) return SoulUpgradeCheck.Hidden;
            return souls >= node.cost ? SoulUpgradeCheck.Ok : SoulUpgradeCheck.NotEnough;
        }

        /// <summary>解放した強化のうち、effect の値の合計。</summary>
        public float Total(SoulUpgradeEffect effect, ICollection<string> purchased)
        {
            if (purchased == null) return 0f;
            float total = 0f;
            foreach (SoulUpgradeNode node in nodes)
            {
                if (node.effect == effect && purchased.Contains(node.id)) total += node.value;
            }
            return total;
        }

        /// <summary>解放した強化のうち、effect のものの値を 1 つずつ（ショートカットの層の番号など）。</summary>
        public List<float> Values(SoulUpgradeEffect effect, ICollection<string> purchased)
        {
            var values = new List<float>();
            if (purchased == null) return values;
            foreach (SoulUpgradeNode node in nodes)
            {
                if (node.effect == effect && purchased.Contains(node.id)) values.Add(node.value);
            }
            return values;
        }

        /// <summary>effect の強化を 1 つでも解放してあるか（武器屋・銀行・図鑑など）。</summary>
        public bool Has(SoulUpgradeEffect effect, ICollection<string> purchased)
        {
            if (purchased == null) return false;
            foreach (SoulUpgradeNode node in nodes)
            {
                if (node.effect == effect && purchased.Contains(node.id)) return true;
            }
            return false;
        }
    }
}
