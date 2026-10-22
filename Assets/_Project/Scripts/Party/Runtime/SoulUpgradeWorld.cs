using System.Collections.Generic;
using TpsDungeon.Items;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 魂の強化（<see cref="MetaUpgrades"/>）をゲームに当てる。<see cref="GameFlow"/> が自分の GameObject に付ける。
    /// - パーティーの補正（経験値・体力・クリティカル・ドロップ）は、起きたときと、強化を買うたび・経験値ボーナスが始まる・終わるたびに入れ直す。
    /// - 共有のバッグは、強化の分だけ枠を足す（増やした分を覚えておき、何度当てても増えすぎない）。
    /// - ショートカットは、解放した層を開ける（閉じるのは「階のデバッグ」ウィンドウに任せる）。
    /// - 地上の武器屋・銀行の扉（SceneGate の textId が weapon_shop / bank）は、解放してあれば開ける。中の画面はまだ無い。
    /// - 仲間の人数は、地上で始めたときと、転生して地上に着いたときに合わせる。連れて行けない仲間はパーティーから外して隠す。
    ///   転生で加わった仲間は Lv1 から。
    /// - 転生して地上に着いたら、経験値ボーナスを始める。時間は死後の場所の外でだけ進む（ポーズ中は止まる）。
    /// 死んだときにレベルをソウルに変えるのは <see cref="Harvest"/>、持ち物を手放すのは <see cref="DiscardItems"/>（どちらも GameFlow が呼ぶ）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoulUpgradeWorld : MonoBehaviour
    {
        public const string WeaponShopTextId = "weapon_shop";
        public const string BankTextId = "bank";

        private const string OpenShopLabel = "入る（中はまだ準備中）";

        private Party party;
        private readonly List<PartyMember> roster = new List<PartyMember>();
        private int addedBagSlots;

        public Party Party
        {
            get => party;
            set => party = value;
        }

        /// <summary>連れて行ける人数（先頭を含む）。</summary>
        public static int AllowedMembers => 1 + MetaUpgrades.ExtraMembers;

        private void OnEnable()
        {
            MetaUpgrades.Changed += OnUpgradesChanged;
            MetaUpgrades.ExpBoostChanged += ApplyModifiers;
        }

        private void OnDisable()
        {
            MetaUpgrades.Changed -= OnUpgradesChanged;
            MetaUpgrades.ExpBoostChanged -= ApplyModifiers;
        }

        private void Start()
        {
            CaptureRoster();
            ApplyPartySize(resetAdded: false);
            OnUpgradesChanged();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == SceneTravel.AfterlifeScene) return;
            MetaUpgrades.TickExpBoost(Time.deltaTime);
        }

        private void OnUpgradesChanged()
        {
            ApplyModifiers();
            ApplyBag();
            ApplyShortcuts();
            ApplyGates();
        }

        /// <summary>シーンに着いて先頭を並べる直前（GameFlow.Arrive から）。転生なら人数を合わせ、ボーナスを始める。</summary>
        public void OnArrived(bool rebirth)
        {
            ApplyGates();
            if (!rebirth) return;
            ApplyPartySize(resetAdded: true);
            MetaUpgrades.StartExpBoost();
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
            foreach (SceneGate gate in FindObjectsByType<SceneGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gate.TextId == WeaponShopTextId) Open(gate, shop);
                else if (gate.TextId == BankTextId) Open(gate, bank);
            }
        }

        private static void Open(SceneGate gate, bool open)
        {
            gate.IsOpen = open;
            if (open) gate.OpenLabel = OpenShopLabel;
        }

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

        // ---- 死んだとき・転生するとき ------------------------------------------

        /// <summary>全滅した。全員のレベルをソウルに変えて足し、Lv1 に戻す。足したソウルを返す。</summary>
        public static int Harvest(Party party)
        {
            var members = new List<SoulHarvestMember>();
            if (party != null)
            {
                foreach (PartyMember member in party.Members)
                {
                    if (member == null || member.Progression == null) continue;
                    members.Add(new SoulHarvestMember
                    {
                        id = member.Progression.ProgressId,
                        name = member.DisplayName,
                        level = member.Progression.Level,
                    });
                }
            }

            int gained = MetaUpgrades.Harvest(members);
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
