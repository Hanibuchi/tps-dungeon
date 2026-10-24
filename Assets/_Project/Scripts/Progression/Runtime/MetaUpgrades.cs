using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 死んでも残る進み（ソウルと、魂の強化）の窓口。保存先は persistentDataPath の meta.json。最初に要ったときに読み、変えるたびに書く。
    /// - 強化の木は Resources の表（<see cref="SoulUpgradeTable"/>）から 1 度だけ作る。
    /// - 死んだときは <see cref="Harvest"/> でレベルをソウルに変えて足し、内訳を「まだ見せていない」として残す。
    ///   死後の場所で強化の画面を初めて開いたときに演出で見せ、<see cref="ClearHarvest"/> で消す。
    /// - 効果の量は <see cref="Total"/> で引く。パーティーの補正（経験値・体力・クリティカル・ドロップ）は <see cref="ApplyTo"/> で入れる。
    /// - レベルが上がってからしばらく経験値が増えるブーストは、ここで残り時間を持つ（<see cref="StartExpBoost"/> / <see cref="TickExpBoost"/>。保存はしない）。
    /// 変わったら <see cref="Changed"/> で知らせる。
    /// </summary>
    public static class MetaUpgrades
    {
        public const string FileName = "meta.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        private static string overridePath;
        private static MetaProgressSaveData current;
        private static HashSet<string> purchased;
        private static SoulUpgradeTree tree;
        private static SoulUpgradeTable table;
        private static bool tableLoaded;
        private static float expBoostRemaining;

        /// <summary>ソウル・解放した強化・まだ見せていない内訳のどれかが変わった。</summary>
        public static event Action Changed;

        /// <summary>経験値ブーストが始まった・終わった。</summary>
        public static event Action ExpBoostChanged;

        /// <summary>保存先を差し替える（テスト用）。null か空なら既定の場所に戻す。読み込んだ状態も捨てる。</summary>
        public static string PathOverride
        {
            get => overridePath;
            set
            {
                overridePath = value;
                current = null;
                purchased = null;
            }
        }

        /// <summary>表を差し替える（テスト用）。null なら Resources の表に戻す。</summary>
        public static SoulUpgradeTable TableOverride
        {
            set
            {
                table = value;
                tableLoaded = value != null;
                tree = null;
            }
        }

        // null も空文字も「既定の場所」。エディタのドメインリロードをまたぐと、null だった文字列が空文字になって戻ってくるため。
        private static string FilePath => string.IsNullOrEmpty(overridePath) ? DefaultPath : overridePath;

        public static SoulUpgradeTable Table
        {
            get
            {
                if (!tableLoaded)
                {
                    table = Resources.Load<SoulUpgradeTable>(SoulUpgradeTable.ResourcePath);
                    tableLoaded = true;
                }
                return table;
            }
        }

        /// <summary>
        /// 強化の木。表が無ければ空の木。
        /// エディタでは表を初めて読み込んだときに表の OnValidate が呼ばれて <see cref="InvalidateTree"/> が走るので、手元で作り終えてから入れる。
        /// </summary>
        public static SoulUpgradeTree Tree
        {
            get
            {
                if (tree != null) return tree;
                SoulUpgradeTable source = Table;
                var errors = new List<string>();
                var built = new SoulUpgradeTree(source != null ? source.Nodes : null, errors);
                foreach (string error in errors) Debug.LogWarning($"魂の強化の表: {error}");
                tree = built;
                return tree;
            }
        }

        /// <summary>表が書き換わった。次に引くときに木を作り直す。</summary>
        public static void InvalidateTree() => tree = null;

        public static long Souls => Load().souls;

        /// <summary>解放した強化の id。</summary>
        public static ICollection<string> Purchased
        {
            get
            {
                Load();
                return purchased;
            }
        }

        public static bool IsPurchased(string id) => id != null && Purchased.Contains(id);

        public static bool IsVisible(string id) => Tree.IsVisible(id, Purchased);

        public static SoulUpgradeCheck Check(string id) => Tree.Check(id, Purchased, Souls);

        /// <summary>強化を買う。買えたら（<see cref="SoulUpgradeCheck.Ok"/>）ソウルを引いて書き出し、知らせる。</summary>
        public static SoulUpgradeCheck TryBuy(string id)
        {
            SoulUpgradeCheck check = Check(id);
            if (check != SoulUpgradeCheck.Ok) return check;

            Tree.TryGet(id, out SoulUpgradeNode node);
            MetaProgressSaveData data = Load();
            data.souls -= node.cost;
            data.purchased.Add(id);
            purchased.Add(id);
            Commit();
            return SoulUpgradeCheck.Ok;
        }

        public static float Total(SoulUpgradeEffect effect) => Tree.Total(effect, Purchased);

        public static bool Has(SoulUpgradeEffect effect) => Tree.Has(effect, Purchased);

        public static List<float> Values(SoulUpgradeEffect effect) => Tree.Values(effect, Purchased);

        /// <summary>転生で持ち越せる武器の数。</summary>
        public static int CarryOverLimit => Mathf.Max(0, Mathf.RoundToInt(Total(SoulUpgradeEffect.CarryOver)));

        /// <summary>先頭のほかに連れて行ける仲間の数。</summary>
        public static int ExtraMembers => Mathf.Max(0, Mathf.RoundToInt(Total(SoulUpgradeEffect.PartySize)));

        /// <summary>共有のバッグに足す枠の数。</summary>
        public static int ExtraBagSlots => Mathf.Max(0, Mathf.RoundToInt(Total(SoulUpgradeEffect.BagSize)));

        /// <summary>武器屋に並ぶ武器の上乗せ（武器屋の画面ができたら読む）。</summary>
        public static int ShopStockBonus => Mathf.Max(0, Mathf.RoundToInt(Total(SoulUpgradeEffect.ShopStock)));

        /// <summary>武器の売値に掛ける倍率（武器屋の画面ができたら読む）。</summary>
        public static float SellPriceMultiplier => 1f + Mathf.Max(0f, Total(SoulUpgradeEffect.SellPrice));

        /// <summary>壊した箱から武器が出る重みに掛ける倍率。</summary>
        public static float BoxWeaponMultiplier => 1f + Mathf.Max(0f, Total(SoulUpgradeEffect.BoxWeaponRate));

        /// <summary>階に置く木箱の数・宝箱の確率に掛ける倍率。</summary>
        public static float BoxSpawnMultiplier => 1f + Mathf.Max(0f, Total(SoulUpgradeEffect.BoxSpawnRate));

        /// <summary>解放したガチャの台（1 で E〜B、2 で D〜A、3 で B〜S。ガチャ屋の画面ができたら読む）。</summary>
        public static List<int> GachaTiers => Values(SoulUpgradeEffect.Gacha).ConvertAll(Mathf.RoundToInt);

        /// <summary>倉庫に足す枠の数（倉庫の画面ができたら読む）。</summary>
        public static int ExtraBankSlots => Mathf.Max(0, Mathf.RoundToInt(Total(SoulUpgradeEffect.BankSize)));

        /// <summary>ソウルを足す（デバッグや、死んだとき）。</summary>
        public static void AddSouls(long amount)
        {
            if (amount == 0) return;
            MetaProgressSaveData data = Load();
            data.souls = Math.Max(0, data.souls + amount);
            Commit();
        }

        // ---- 死んだとき ------------------------------------------------------

        /// <summary>まだ見せていない死んだときの内訳があるか。</summary>
        public static bool HasHarvest => Load().hasHarvest;

        public static SoulHarvest PendingHarvest => Load().hasHarvest ? Load().harvest : null;

        /// <summary>1 人分のソウル（表の式）。</summary>
        public static int SoulsFor(int level)
        {
            SoulUpgradeTable source = Table;
            return source != null
                ? SoulConversion.SoulsFor(level, source.SoulsPerLevel, source.SoulExponent)
                : SoulConversion.SoulsFor(level);
        }

        /// <summary>
        /// 死んだ。members の各人のレベルをソウルに変えて足し、内訳を残す（前の内訳をまだ見せていなければ後ろに足す）。
        /// レベルを戻すのは呼ぶ側（PartyProgression.ResetForNewRun）。足したソウルを返す。
        /// </summary>
        public static int Harvest(IEnumerable<SoulHarvestMember> members)
        {
            MetaProgressSaveData data = Load();
            if (!data.hasHarvest) data.harvest.members.Clear();

            long gained = 0;
            if (members != null)
            {
                foreach (SoulHarvestMember member in members)
                {
                    if (member == null) continue;
                    member.souls = SoulsFor(member.level);
                    data.harvest.members.Add(member);
                    gained += member.souls;
                }
            }

            data.hasHarvest = true;
            data.souls += gained;
            Commit();
            return (int)Math.Min(int.MaxValue, gained);
        }

        /// <summary>死んだときの内訳を見せ終えた。</summary>
        public static void ClearHarvest()
        {
            MetaProgressSaveData data = Load();
            if (!data.hasHarvest) return;
            data.hasHarvest = false;
            data.harvest.members.Clear();
            Commit();
        }

        // ---- デバッグ（魂の燭台のデバッグの窓から） ---------------------------

        /// <summary>ソウルを value にする（デバッグ）。</summary>
        public static void SetSouls(long value)
        {
            MetaProgressSaveData data = Load();
            value = Math.Max(0, value);
            if (data.souls == value) return;
            data.souls = value;
            Commit();
        }

        /// <summary>
        /// ソウルを払わずに解放する・戻す（デバッグ）。解放するときは前の強化もたどって解放し、戻すときは先の強化も戻す（木がつながったままになるように）。
        /// </summary>
        public static void SetPurchased(string id, bool owned)
        {
            if (!Tree.Contains(id)) return;
            MetaProgressSaveData data = Load();
            if (owned)
            {
                for (string at = id; !string.IsNullOrEmpty(at) && Tree.TryGet(at, out SoulUpgradeNode node); at = node.parent)
                {
                    if (purchased.Add(at)) data.purchased.Add(at);
                }
            }
            else
            {
                var stack = new Stack<string>();
                stack.Push(id);
                while (stack.Count > 0)
                {
                    string at = stack.Pop();
                    if (purchased.Remove(at)) data.purchased.Remove(at);
                    foreach (string child in Tree.Children(at)) stack.Push(child);
                }
            }
            Commit();
        }

        /// <summary>全部の強化を、ソウルを払わずに解放する（デバッグ）。</summary>
        public static void PurchaseAll()
        {
            MetaProgressSaveData data = Load();
            foreach (SoulUpgradeNode node in Tree.Nodes)
            {
                if (purchased.Add(node.id)) data.purchased.Add(node.id);
            }
            Commit();
        }

        /// <summary>解放した強化を全部戻す。ソウルはそのまま（デバッグ）。</summary>
        public static void ClearPurchases()
        {
            MetaProgressSaveData data = Load();
            data.purchased.Clear();
            purchased.Clear();
            Commit();
        }

        /// <summary>全部消す（デバッグ）。</summary>
        public static void ResetAll()
        {
            current = new MetaProgressSaveData();
            purchased = new HashSet<string>(StringComparer.Ordinal);
            expBoostRemaining = 0f;
            Commit();
            ExpBoostChanged?.Invoke();
        }

        // ---- 補正 ------------------------------------------------------------

        /// <summary>経験値・体力・クリティカル・ドロップの補正を入れる。ブーストの間は経験値にブーストの倍率も掛ける。</summary>
        public static void ApplyTo(ProgressionModifiers modifiers)
        {
            if (modifiers == null) return;

            float boost = IsExpBoostActive ? ExpBoostMultiplier : 1f;
            modifiers.SetExpMultiplier((1f + Total(SoulUpgradeEffect.ExpRate)) * boost);
            modifiers.SetMaxHp(new StatModifier(0f, Total(SoulUpgradeEffect.MaxHp)));
            modifiers.SetCritChance(new StatModifier(Total(SoulUpgradeEffect.CritChance), 0f));
            modifiers.SetCritMultiplier(new StatModifier(Total(SoulUpgradeEffect.CritMultiplier), 0f));
            modifiers.SetDropRate(new StatModifier(0f, Total(SoulUpgradeEffect.DropRate)));
            modifiers.SetUniqueDropRate(new StatModifier(0f, Total(SoulUpgradeEffect.UniqueDropRate)));
        }

        // ---- レベルアップ後の経験値ブースト -------------------------------------

        /// <summary>ブーストの残り（秒）。</summary>
        public static float ExpBoostRemaining => expBoostRemaining;

        /// <summary>ブーストの長さ（秒。解放した一番先の段の値）。</summary>
        public static float ExpBoostDuration => Mathf.Max(0f, Total(SoulUpgradeEffect.ExpBoost));

        public static bool IsExpBoostActive => expBoostRemaining > 0f;

        /// <summary>ブーストの間、経験値に掛ける倍率。</summary>
        public static float ExpBoostMultiplier => Table != null ? Table.ExpBoostMultiplier : 2f;

        /// <summary>レベルが上がった。ブーストを強化の長さで始める（途中なら長さを満タンに戻す。強化が無ければ何もしない）。</summary>
        public static void StartExpBoost()
        {
            float duration = ExpBoostDuration;
            if (duration <= 0f) return;
            bool started = expBoostRemaining <= 0f;
            expBoostRemaining = duration;
            if (started) ExpBoostChanged?.Invoke();
        }

        /// <summary>ブーストの時間を進める。終わったら知らせる。</summary>
        public static void TickExpBoost(float deltaTime)
        {
            if (expBoostRemaining <= 0f || deltaTime <= 0f) return;
            expBoostRemaining = Mathf.Max(0f, expBoostRemaining - deltaTime);
            if (expBoostRemaining <= 0f) ExpBoostChanged?.Invoke();
        }

        // ---- 保存 ------------------------------------------------------------

        /// <summary>読み込み直す（ファイルを外で書き換えたとき）。</summary>
        public static void Reload()
        {
            current = null;
            purchased = null;
        }

        private static MetaProgressSaveData Load()
        {
            if (current != null) return current;
            current = new MetaProgressSaveData();
            string path = FilePath;
            if (File.Exists(path))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<MetaProgressSaveData>(File.ReadAllText(path));
                    if (loaded != null) current = loaded;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException)
                {
                    Debug.LogWarning($"魂の強化の保存を読めなかった: {path}\n{e.Message}");
                }
            }
            current.Normalize();
            purchased = new HashSet<string>(current.purchased, StringComparer.Ordinal);
            return current;
        }

        private static void Commit()
        {
            Save(Load());
            Changed?.Invoke();
        }

        /// <summary>書き出す。途中で落ちても前の保存が壊れないよう、一時ファイルに書いてから置き換える。</summary>
        private static void Save(MetaProgressSaveData data)
        {
            string path = FilePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の再生の購読を持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            ExpBoostChanged = null;
            current = null;
            purchased = null;
            tree = null;
            table = null;
            tableLoaded = false;
            expBoostRemaining = 0f;
        }
    }
}
