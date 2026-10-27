using System;
using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Items;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 魂の強化（<see cref="MetaUpgrades"/>）をゲームに当てる。<see cref="GameFlow"/> が自分の GameObject に付ける。
    /// - パーティーの補正（経験値・体力・クリティカル・ドロップ）は、起きたときと、強化を買うたびに入れ直す。
    /// - 共有のバッグは、強化の分だけ枠を足す（増やした分を覚えておき、何度当てても増えすぎない）。
    /// - ショートカットは、解放した層を開ける（閉じるのは「階のデバッグ」ウィンドウに任せる）。
    /// - 地上の武器屋・銀行・ガチャ屋の扉（SceneGate の textId が weapon_shop / bank / gacha）は、解放してあれば開ける。中の画面はまだ無い。
    /// - 箱の数（FloorBuilder.BoxSpawnMultiplier）と箱から武器が出る重み（BreakableProp.WeaponWeightMultiplier）を入れる。
    /// - 仲間の人数は、地上で始めたときと、転生して地上に着いたときに合わせる。連れて行けない仲間はパーティーから外して隠す。
    ///   転生で加わった仲間は Lv1 から。
    /// - 階の部屋を全部攻略したら（<see cref="RoomEnemySpawner.AnyAllRoomsCleared"/>）、経験値ボーナスの強化の分だけ倍率を掛けずに経験値を配り、<see cref="FloorClearBonusGranted"/> で知らせる。
    /// 死んだときにレベルをソウルに変えるのは <see cref="Harvest"/>、持ち物を手放すのは <see cref="DiscardItems"/>（どちらも GameFlow が呼ぶ）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoulUpgradeWorld : MonoBehaviour
    {
        public const string WeaponShopTextId = "weapon_shop";
        public const string BankTextId = "bank";
        public const string GachaTextId = "gacha";

        private const string OpenShopLabel = "入る（中はまだ準備中）";

        private Party party;
        private readonly List<PartyMember> roster = new List<PartyMember>();
        private int addedBagSlots;

        public Party Party
        {
            get => party;
            set => party = value;
        }

        /// <summary>階の部屋を全部攻略して、経験値ボーナスを配った（配った量）。</summary>
        public static event Action<int> FloorClearBonusGranted;

        /// <summary>連れて行ける人数（先頭を含む）。</summary>
        public static int AllowedMembers => 1 + MetaUpgrades.ExtraMembers;

        private void OnEnable()
        {
            MetaUpgrades.Changed += OnUpgradesChanged;
            RoomEnemySpawner.AnyAllRoomsCleared += OnAllRoomsCleared;
        }

        private void OnDisable()
        {
            MetaUpgrades.Changed -= OnUpgradesChanged;
            RoomEnemySpawner.AnyAllRoomsCleared -= OnAllRoomsCleared;
        }

        private void Start()
        {
            CaptureRoster();
            ApplyPartySize(resetAdded: false);
            OnUpgradesChanged();
        }

        /// <summary>階の部屋を全部攻略した。経験値ボーナスを解放していれば、その量を倍率を掛けずに全員に配る。</summary>
        private static void OnAllRoomsCleared(RoomEnemySpawner _) => GrantFloorClearBonus();

        /// <summary>経験値ボーナスを配る（デバッグの窓からも呼ぶ）。配った量を返す。</summary>
        public static int GrantFloorClearBonus()
        {
            int amount = MetaUpgrades.FloorClearExp;
            PartyProgression progression = PartyProgression.Current;
            if (amount <= 0 || progression == null) return 0;
            int granted = progression.GrantFlatExp(amount);
            if (granted > 0) FloorClearBonusGranted?.Invoke(granted);
            return granted;
        }

        private void OnUpgradesChanged()
        {
            ApplyModifiers();
            ApplyBag();
            ApplyShortcuts();
            ApplyGates();
            ApplyBoxes();
        }

        /// <summary>シーンに着いて先頭を並べる直前（GameFlow.Arrive から）。転生なら人数を合わせる。</summary>
        public void OnArrived(bool rebirth)
        {
            ApplyGates();
            if (rebirth) ApplyPartySize(resetAdded: true);
        }

        private void ApplyModifiers()
        {
            PartyProgression progression = PartyProgression.Current;
            if (progression != null) MetaUpgrades.ApplyTo(progression.Modifiers);
        }

        private void ApplyBag()
        {
            if (party == null) return;
            int extra = MetaUpgrades.ExtraBagSlots - addedBagSlots;
            if (extra <= 0) return;
            party.Bag.ExpandBag(extra);
            addedBagSlots += extra;
        }

        /// <summary>箱の数（次に作る階から）と、箱から武器が出る重み。</summary>
        private static void ApplyBoxes()
        {
            FloorBuilder.BoxSpawnMultiplier = MetaUpgrades.BoxSpawnMultiplier;
            BreakableProp.WeaponWeightMultiplier = MetaUpgrades.BoxWeaponMultiplier;
        }

        private static void ApplyShortcuts()
        {
            foreach (float layer in MetaUpgrades.Values(SoulUpgradeEffect.Shortcut))
            {
                int index = Mathf.RoundToInt(layer) - 1;
                if (index >= 0 && !ShortcutUnlocks.IsUnlocked(index)) ShortcutUnlocks.SetUnlocked(index, true);
            }
        }

        private static void ApplyGates()
        {
            bool shop = MetaUpgrades.Has(SoulUpgradeEffect.WeaponShop);
            bool bank = MetaUpgrades.Has(SoulUpgradeEffect.Bank);
            bool gacha = MetaUpgrades.Has(SoulUpgradeEffect.Gacha);
            foreach (SceneGate gate in FindObjectsByType<SceneGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gate.TextId == WeaponShopTextId) Open(gate, shop);
                else if (gate.TextId == BankTextId) Open(gate, bank);
                else if (gate.TextId == GachaTextId) Open(gate, gacha);
            }
        }

        private static void Open(SceneGate gate, bool open)
        {
            gate.IsOpen = open;
            if (open) gate.OpenLabel = OpenShopLabel;
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の再生の購読を持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => FloorClearBonusGranted = null;

        // ---- 仲間の人数 ------------------------------------------------------

        /// <summary>最初に居た仲間を、並びの順に覚えておく（人数を増やすときはこの順に戻す）。</summary>
        private void CaptureRoster()
        {
            roster.Clear();
            if (party == null) return;
            foreach (PartyMember member in party.Members)
            {
                if (member != null) roster.Add(member);
            }
        }

        /// <summary>
        /// 連れて行ける人数に合わせる。先頭はいつも残し、ほかは最初の並びの順に選ぶ。
        /// resetAdded なら新しく加わった仲間を Lv1 に戻す（転生のとき）。
        /// </summary>
        private void ApplyPartySize(bool resetAdded)
        {
            if (party == null || roster.Count == 0) return;

            int allowed = Mathf.Clamp(AllowedMembers, 1, roster.Count);
            PartyMember leader = party.Leader;
            var keep = new HashSet<PartyMember>();
            if (leader != null) keep.Add(leader);
            foreach (PartyMember member in roster)
            {
                if (keep.Count >= allowed) break;
                keep.Add(member);
            }

            foreach (PartyMember member in roster)
            {
                if (member == null) continue;
                bool inParty = party.IndexOf(member) >= 0;
                if (keep.Contains(member))
                {
                    if (inParty) continue;
                    member.gameObject.SetActive(true);
                    party.Add(member);
                    // 抜けたときに止めた歩きと戦いを、後ろにつく側として動かし直す。
                    member.SetLeader(party.Leader == member, party.Input);
                    if (resetAdded && member.Progression != null) member.Progression.ResetProgress();
                    member.Revive();
                }
                else if (inParty)
                {
                    party.Remove(member);
                    member.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>最初に居た仲間（パーティーから外した人も）。セーブの手持ちを人ごとに当てるのに使う。</summary>
        public IReadOnlyList<PartyMember> Roster => roster;

        /// <summary>
        /// セーブした並び（ProgressId の列。先頭が操作するキャラ）に合わせる。最初に居た仲間から選び、連れて行ける人数までにする。
        /// 並びに無い仲間は外して隠す。並びの誰も見つからなければ何もしない。
        /// HP は戻さない（隠していた仲間は、出したときに PartyProgression がセーブの HP を当てる）。
        /// </summary>
        public void ApplyOrder(IReadOnlyList<string> ids)
        {
            if (party == null || ids == null) return;

            var wanted = new List<PartyMember>();
            int allowed = Mathf.Max(1, AllowedMembers);
            foreach (string id in ids)
            {
                if (wanted.Count >= allowed) break;
                PartyMember member = roster.Find(m => m != null && m.Progression != null && m.Progression.ProgressId == id);
                if (member != null && !wanted.Contains(member)) wanted.Add(member);
            }
            if (wanted.Count == 0) return;

            foreach (PartyMember member in wanted)
            {
                if (party.IndexOf(member) >= 0) continue;
                member.gameObject.SetActive(true);
                party.Add(member);
            }

            foreach (PartyMember member in roster)
            {
                if (member == null || wanted.Contains(member) || party.IndexOf(member) < 0) continue;
                party.Remove(member);
                member.gameObject.SetActive(false);
            }

            // 倒れた人と生きている人の前後は入れ替えられない（Party.Move が断る）ので、そのときは近い並びのまま。
            for (int i = 0; i < wanted.Count; i++)
            {
                int at = party.IndexOf(wanted[i]);
                if (at >= 0 && at != i) party.Move(at, i);
            }

            foreach (PartyMember member in party.Members)
            {
                if (member != null) member.SetLeader(party.Leader == member, party.Input);
            }
        }

        // ---- 死んだとき・転生するとき ------------------------------------------

        /// <summary>全滅した。操作していたメンバー（先頭）1 人のレベルだけをソウルに変えて足し、全員を Lv1 に戻す。足したソウルを返す。</summary>
        public static long Harvest(Party party)
        {
            var members = new List<SoulHarvestMember>();
            PartyMember leader = party != null ? party.Leader : null;
            if (leader != null && leader.Progression != null)
            {
                members.Add(new SoulHarvestMember
                {
                    id = leader.Progression.ProgressId,
                    name = leader.DisplayName,
                    level = leader.Progression.Level,
                });
            }

            long gained = MetaUpgrades.Harvest(members);
            PartyProgression progression = PartyProgression.Current;
            if (progression != null) progression.ResetForNewRun();
            return gained;
        }

        /// <summary>転生で持ち物を手放す。keep に入っている物だけ元の場所に残し、全員の手持ちと共有のバッグからほかを消す。</summary>
        public void DiscardItems(ICollection<ItemInstance> keep)
        {
            var seen = new HashSet<Inventory>();
            var members = new List<PartyMember>(roster);
            if (party != null)
            {
                foreach (PartyMember member in party.Members)
                {
                    if (!members.Contains(member)) members.Add(member);
                }
                Discard(party.Bag, keep, seen);
            }

            foreach (PartyMember member in members)
            {
                if (member != null && member.Inventory != null) Discard(member.Inventory.Inventory, keep, seen);
            }
        }

        private static void Discard(Inventory items, ICollection<ItemInstance> keep, HashSet<Inventory> seen)
        {
            if (items == null || !seen.Add(items)) return;
            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];
                if (item != null && (keep == null || !keep.Contains(item))) items.RemoveAt(i);
            }
        }
    }
}
