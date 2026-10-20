using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>敵 1 種類。Id は表の中の番号、Cost は 1 体あたりのコスト。</summary>
    public readonly struct EnemyKind
    {
        public readonly int Id;
        public readonly double Cost;

        public EnemyKind(int id, double cost)
        {
            Id = id;
            Cost = cost;
        }
    }

    /// <summary>編成タイプ 1 つ分の、種類ごとの予算の比率。</summary>
    public readonly struct EnemyShare
    {
        public readonly int EnemyId;
        public readonly double Share;

        public EnemyShare(int enemyId, double share)
        {
            EnemyId = enemyId;
            Share = share;
        }
    }

    /// <summary>編成タイプ（混成・主役＋脇役・単種など）。Weight は出現の重み、Shares は種類ごとの予算の比率（合計で割って使う）。</summary>
    public sealed class EnemyComposition
    {
        public double Weight;
        public List<EnemyShare> Shares = new List<EnemyShare>();
    }

    /// <summary>1 つの層の、部屋の敵の決め方。</summary>
    public sealed class EnemySpawnRules
    {
        public List<EnemyKind> Enemies = new List<EnemyKind>();
        public List<EnemyComposition> Compositions = new List<EnemyComposition>();
        /// <summary>部屋の予算の上限の係数。部屋の予算の上限はこれ × 部屋の大きさ（<see cref="BudgetBasis"/>）。</summary>
        public double BudgetPerCell;
        /// <summary>予算の上限を部屋の何に比例させるか（セル数か辺の平均か）。</summary>
        public RoomBudgetBasis BudgetBasis = RoomBudgetBasis.Area;
        /// <summary>普通の部屋がモンスターハウスになる確率。</summary>
        public double MonsterHouseChance;
        /// <summary>モンスターハウスの予算の上限に掛ける倍率。</summary>
        public double MonsterHouseBudgetMultiplier = 1d;
    }

    /// <summary>同じ種類の敵の 1 かたまり。部屋の中の 1 か所の周りに出す。</summary>
    public readonly struct EnemyGroup
    {
        public readonly int EnemyId;
        public readonly int Count;

        public EnemyGroup(int enemyId, int count)
        {
            EnemyId = enemyId;
            Count = count;
        }

        public override string ToString() => $"{EnemyId}x{Count}";
    }

    /// <summary>
    /// 部屋ごとに「どの敵が、何体ずつ、どんなグループで出るか」を決める。
    /// 予算を [0, 上限] から一様に引き → 編成タイプを重みで引き → 種類ごとの目標体数（予算 × 比率 ÷ コスト）を
    /// 最大剰余法で整数にし → 種類ごとに round(√N) 個のグループへ 1 体ずつ一様に割り振る。
    /// 配置（座標）は決めない。
    /// </summary>
    public static class RoomEnemyRoller
    {
        /// <summary>
        /// 部屋の予算の上限。セル数（1x1 なら 1、2x2 なら 4）か、辺の平均（2x2 なら 2）に比例する（<see cref="EnemySpawnRules.BudgetBasis"/>）。
        /// モンスターハウスなら倍率を掛ける。
        /// </summary>
        public static double MaxBudget(EnemySpawnRules rules, RoomInstance room, bool monsterHouse = false)
        {
            if (rules == null || room == null) return 0d;
            double max = Math.Max(0d, rules.BudgetPerCell) * BudgetSize(rules.BudgetBasis, room.Bounds);
            return monsterHouse ? max * Math.Max(0d, rules.MonsterHouseBudgetMultiplier) : max;
        }

        /// <summary>予算の上限に掛ける部屋の大きさ。</summary>
        public static double BudgetSize(RoomBudgetBasis basis, GridRect bounds) =>
            basis == RoomBudgetBasis.MeanSide ? (bounds.Width + bounds.Height) * 0.5d : bounds.Area;

        /// <summary>敵を出す部屋か。普通の部屋だけ（入口・出口・ショップ、ボス階の決まった部屋（Boss タグ）には出さない）。</summary>
        public static bool CanHostEnemies(RoomInstance room) =>
            room != null && room.Role == RoomRole.Normal && (room.Template == null || (room.Template.Tags & RoomTag.Boss) == 0);

        /// <summary>予算を [0, maxBudget] から引いて、その部屋に出すグループを決める。0 体なら空。</summary>
        public static List<EnemyGroup> Roll(EnemySpawnRules rules, double maxBudget, Random rng)
        {
            double budget = rng.NextDouble() * Math.Max(0d, maxBudget);
            return RollWithBudget(rules, budget, rng);
        }

        /// <summary>予算を決めた後の手順（編成タイプ → 体数 → グループ）。</summary>
        public static List<EnemyGroup> RollWithBudget(EnemySpawnRules rules, double budget, Random rng)
        {
            var groups = new List<EnemyGroup>();
            if (rules == null) return groups;

            var composition = PickComposition(rules.Compositions, rng);
            if (composition == null) return groups;

            var counts = Counts(rules, composition, budget);
            foreach (var (enemyId, count) in counts) Split(enemyId, count, rng, groups);
            return groups;
        }

        /// <summary>編成タイプを重みで引く。重みが 0 以下のものは出ない。引けるものが無ければ null。</summary>
        public static EnemyComposition PickComposition(IReadOnlyList<EnemyComposition> compositions, Random rng)
        {
            if (compositions == null) return null;

            double total = 0d;
            foreach (var c in compositions)
            {
                if (c != null && c.Weight > 0d) total += c.Weight;
            }
            if (total <= 0d) return null;

            double roll = rng.NextDouble() * total;
            EnemyComposition last = null;
            foreach (var c in compositions)
            {
                if (c == null || c.Weight <= 0d) continue;
                last = c;
                roll -= c.Weight;
                if (roll < 0d) return c;
            }
            return last;
        }

        /// <summary>
        /// 種類ごとの体数。目標体数（予算 × 比率 ÷ コスト。比率は合計で割る）の整数部分を確保し、
        /// 残りの予算で小数部分の大きい種類から順に、買えるなら 1 体ずつ足す。
        /// コストが 0 以下・表に無い種類は飛ばす。並びは編成タイプの比率の並び（同じ種類は 1 つにまとめる）。
        /// </summary>
        public static List<(int enemyId, int count)> Counts(EnemySpawnRules rules, EnemyComposition composition, double budget)
        {
            var result = new List<(int enemyId, int count)>();
            if (rules == null || composition == null || budget <= 0d) return result;

            var costs = new Dictionary<int, double>();
            foreach (var kind in rules.Enemies)
            {
                if (kind.Cost > 0d) costs[kind.Id] = kind.Cost;
            }

            // 同じ種類が 2 回書かれていたら比率を足す。
            var shares = new List<(int enemyId, double share)>();
            double shareTotal = 0d;
            foreach (var s in composition.Shares)
            {
                if (s.Share <= 0d || !costs.ContainsKey(s.EnemyId)) continue;
                int at = shares.FindIndex(x => x.enemyId == s.EnemyId);
                if (at >= 0) shares[at] = (s.EnemyId, shares[at].share + s.Share);
                else shares.Add((s.EnemyId, s.Share));
                shareTotal += s.Share;
            }
            if (shareTotal <= 0d) return result;

            var fractions = new double[shares.Count];
            double remaining = budget;
            foreach (var (enemyId, share) in shares)
            {
                double target = budget * (share / shareTotal) / costs[enemyId];
                int whole = (int)Math.Floor(target);
                fractions[result.Count] = target - whole;
                result.Add((enemyId, whole));
                remaining -= whole * costs[enemyId];
            }

            // 小数部分の大きい順（同じなら比率の並び順）。浮動小数の誤差で買えない判定にならないよう少しだけ甘くする。
            var order = new List<int>();
            for (int i = 0; i < result.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int byFraction = fractions[b].CompareTo(fractions[a]);
                return byFraction != 0 ? byFraction : a.CompareTo(b);
            });
            const double Epsilon = 1e-9;
            foreach (int i in order)
            {
                if (fractions[i] <= 0d) continue;
                double cost = costs[result[i].enemyId];
                if (cost > remaining + Epsilon) continue;
                result[i] = (result[i].enemyId, result[i].count + 1);
                remaining -= cost;
            }

            result.RemoveAll(x => x.count <= 0);
            return result;
        }

        /// <summary>そのグループ数。max(1, round(√N))。</summary>
        public static int GroupCount(int count) =>
            count <= 0 ? 0 : Math.Max(1, (int)Math.Round(Math.Sqrt(count), MidpointRounding.AwayFromZero));

        /// <summary>count 体を GroupCount 個のグループへ 1 体ずつ一様に割り振り、0 体でないグループを groups に足す。</summary>
        private static void Split(int enemyId, int count, Random rng, List<EnemyGroup> groups)
        {
            int groupCount = GroupCount(count);
            if (groupCount <= 0) return;

            var sizes = new int[groupCount];
            for (int i = 0; i < count; i++) sizes[rng.Next(groupCount)]++;
            foreach (int size in sizes)
            {
                if (size > 0) groups.Add(new EnemyGroup(enemyId, size));
            }
        }

        /// <summary>
        /// 部屋ごとの乱数の種。階のシードと部屋の番号から決めるので、ドアを開ける順に関係なく同じ部屋は同じ中身になる。
        /// salt で用途（中身・配置・モンスターハウスか）を分ける。
        /// </summary>
        public static int RoomSeed(int layoutSeed, int roomIndex, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)layoutSeed * 0x9E3779B1u;
                h ^= (uint)roomIndex * 0x85EBCA77u;
                h = (h << 13 | h >> 19) * 0xC2B2AE3Du;
                h ^= (uint)salt * 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return (int)h;
            }
        }

        /// <summary>その部屋に出すグループ（部屋ごとの種から引く）。敵を出さない部屋なら空。モンスターハウスなら予算の上限に倍率を掛ける。</summary>
        public static List<EnemyGroup> RollRoom(EnemySpawnRules rules, FloorLayout layout, int roomIndex, bool monsterHouse = false)
        {
            var room = layout?.RoomAt(roomIndex);
            if (rules == null || !CanHostEnemies(room)) return new List<EnemyGroup>();
            return Roll(rules, MaxBudget(rules, room, monsterHouse), new Random(RoomSeed(layout.Seed, roomIndex)));
        }

        /// <summary>モンスターハウスにする部屋の番号。普通の部屋ごとに MonsterHouseChance で決める。</summary>
        public static HashSet<int> PickMonsterHouses(EnemySpawnRules rules, FloorLayout layout)
        {
            var houses = new HashSet<int>();
            if (rules == null || layout == null || rules.MonsterHouseChance <= 0d) return houses;

            foreach (var room in layout.Rooms)
            {
                if (!CanHostEnemies(room)) continue;
                var rng = new Random(RoomSeed(layout.Seed, room.Index, MonsterHouseSalt));
                if (rng.NextDouble() < rules.MonsterHouseChance) houses.Add(room.Index);
            }
            return houses;
        }

        /// <summary>配置に使う乱数の種の salt。</summary>
        public const int PlacementSalt = 1;
        private const int MonsterHouseSalt = 2;
    }
}
