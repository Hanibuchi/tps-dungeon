using System.Collections.Generic;
using UnityEngine;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 部屋に置く物のプレハブの一覧（種類ごと）。FloorConfig から差し、層ごとに差し替えられる。
    /// どこに何を置くかは RoomPropPlanner が決め、ここからは種類ごとに 1 つを乱数で選ぶ。
    /// プレハブは原点が床（壁掛けは壁の面）で、正面（部屋の内側を向く面）が +Z になるように作る（BreakablePrefabBuilder が作る）。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Room Prop Set", fileName = "RoomPropSet")]
    public sealed class RoomPropSet : ScriptableObject
    {
        [Tooltip("壁掛けの松明。原点が壁の面に付く所。")]
        public List<GameObject> torches = new List<GameObject>();
        [Tooltip("床から天井までの柱。")]
        public List<GameObject> pillars = new List<GameObject>();
        [Tooltip("机。長い辺がローカルの Z。")]
        public List<GameObject> tables = new List<GameObject>();
        [Tooltip("椅子。正面（座る人が向く方）が +Z。")]
        public List<GameObject> chairs = new List<GameObject>();
        [Tooltip("机の上に載せる小物（ろうそく・本など）。")]
        public List<GameObject> tableTops = new List<GameObject>();
        [Tooltip("壁ぎわの床の小物（骨・本・薬瓶・ろうそくなど）。")]
        public List<GameObject> wallClutter = new List<GameObject>();
        [Tooltip("壊せる木箱。")]
        public List<GameObject> crates = new List<GameObject>();
        [Tooltip("壊せる宝箱。正面（錠の側）が +Z。")]
        public List<GameObject> chests = new List<GameObject>();

        [Header("大きさ（置き方を決めるときの床の上の四角、m）")]
        [Min(0.5f)] public float torchHeight = 2.6f;
        [Min(0.1f)] public float pillarSize = 1.1f;
        [Min(0.1f)] public float tableWidth = 1.5f;
        [Min(0.1f)] public float tableLength = 2.3f;
        [Min(0.1f), Tooltip("机の天板の高さ。机の上の小物をここに載せる。")] public float tableTopHeight = 1f;
        [Min(0.1f)] public float chairSize = 0.65f;
        [Min(0.1f)] public float crateSize = 0.85f;
        [Min(0.1f)] public float chestWidth = 1.2f;
        [Min(0.1f)] public float chestDepth = 0.7f;
        [Min(0.1f)] public float clutterSize = 0.6f;

        /// <summary>種類の一覧から variant で 1 つ。無ければ null。</summary>
        public GameObject Pick(RoomPropKind kind, int variant)
        {
            List<GameObject> list = ListOf(kind);
            if (list == null || list.Count == 0) return null;
            return list[(variant & int.MaxValue) % list.Count];
        }

        public List<GameObject> ListOf(RoomPropKind kind)
        {
            switch (kind)
            {
                case RoomPropKind.Torch: return torches;
                case RoomPropKind.Pillar: return pillars;
                case RoomPropKind.Table: return tables;
                case RoomPropKind.Chair: return chairs;
                case RoomPropKind.TableTop: return tableTops;
                case RoomPropKind.WallClutter: return wallClutter;
                case RoomPropKind.Crate: return crates;
                case RoomPropKind.Chest: return chests;
                default: return null;
            }
        }

        /// <summary>置き方を決めるための大きさを settings に入れる。</summary>
        public void FillSizes(RoomPropSettings settings)
        {
            settings.TorchHeight = torchHeight;
            settings.PillarSize = pillarSize;
            settings.TableWidth = tableWidth;
            settings.TableLength = tableLength;
            settings.TableTopHeight = tableTopHeight;
            settings.ChairSize = chairSize;
            settings.CrateSize = crateSize;
            settings.ChestWidth = chestWidth;
            settings.ChestDepth = chestDepth;
            settings.ClutterSize = clutterSize;
        }
    }
}
