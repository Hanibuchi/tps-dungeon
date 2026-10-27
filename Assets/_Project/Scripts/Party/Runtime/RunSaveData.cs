using System;
using System.Collections.Generic;
using TpsDungeon.Items;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Party
{
    /// <summary>1 人分の手持ち（ホットバーの枠）。</summary>
    [Serializable]
    public sealed class MemberItemsSaveData
    {
        /// <summary>CharacterProgression.ProgressId。</summary>
        public string id;
        public List<ItemSaveData> slots = new List<ItemSaveData>();
    }

    /// <summary>
    /// ラン中の状態（居場所・パーティーの並び・持ち物）。JsonUtility で書き出せる形にしてある。
    /// レベル・経験値・HP は PartyProgression（progress.json）、ソウルと強化は MetaUpgrades（meta.json）が別に持つ。
    /// </summary>
    [Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>居たシーン（Surface・Afterlife・階のシーン名）。</summary>
        public string scene;

        /// <summary>階に居たか。居たら layer・floor の階の入口から始める（JsonUtility は null を書けないので印で持つ）。</summary>
        public bool hasFloor;
        public int layer;
        public int floor;

        /// <summary>パーティーの並び（ProgressId）。先頭が操作するキャラ。</summary>
        public List<string> order = new List<string>();

        /// <summary>人ごとの手持ち。パーティーから外れている仲間の分も持つ。</summary>
        public List<MemberItemsSaveData> hands = new List<MemberItemsSaveData>();

        /// <summary>共有のバッグの全部の枠。</summary>
        public List<ItemSaveData> bag = new List<ItemSaveData>();

        /// <summary>全滅の演出の途中で閉じた。次に始めたら全滅として扱う。</summary>
        public bool wipePending;

        public DungeonFloor? Floor
        {
            get => hasFloor ? new DungeonFloor(layer, floor) : (DungeonFloor?)null;
            set
            {
                hasFloor = value.HasValue;
                layer = value?.Layer ?? 0;
                floor = value?.Floor ?? 0;
            }
        }

        public MemberItemsSaveData FindHands(string id)
        {
            if (hands == null) return null;
            foreach (MemberItemsSaveData entry in hands)
            {
                if (entry != null && entry.id == id) return entry;
            }
            return null;
        }

        public void Normalize()
        {
            order ??= new List<string>();
            order.RemoveAll(string.IsNullOrEmpty);
            hands ??= new List<MemberItemsSaveData>();
            hands.RemoveAll(h => h == null || string.IsNullOrEmpty(h.id));
            foreach (MemberItemsSaveData entry in hands) entry.slots ??= new List<ItemSaveData>();
            bag ??= new List<ItemSaveData>();
        }
    }
}
