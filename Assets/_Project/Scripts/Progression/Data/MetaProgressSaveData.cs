using System;
using System.Collections.Generic;

namespace TpsDungeon.Progression
{
    /// <summary>死んだとき 1 人分の、ソウルに変わったレベル。</summary>
    [Serializable]
    public sealed class SoulHarvestMember
    {
        /// <summary>CharacterProgression.ProgressId。</summary>
        public string id;
        public string name;
        public int level;
        public int souls;
    }

    /// <summary>死んで得たソウルの内訳。死後の場所で初めて強化の画面を開いたときに演出で見せ、見せたら消す。</summary>
    [Serializable]
    public sealed class SoulHarvest
    {
        public List<SoulHarvestMember> members = new List<SoulHarvestMember>();

        public int Total
        {
            get
            {
                long total = 0;
                foreach (SoulHarvestMember member in members) total += member.souls;
                return (int)Math.Min(int.MaxValue, total);
            }
        }
    }

    /// <summary>死んでも残る進み（ソウルと解放した強化）。JsonUtility で書き出せる形にしてある。</summary>
    [Serializable]
    public sealed class MetaProgressSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long souls;
        public List<string> purchased = new List<string>();

        /// <summary>まだ見せていない死んだときの内訳があるか（JsonUtility は null を書けないので印で持つ）。</summary>
        public bool hasHarvest;
        public SoulHarvest harvest = new SoulHarvest();

        public void Normalize()
        {
            if (souls < 0) souls = 0;
            purchased ??= new List<string>();
            purchased.RemoveAll(string.IsNullOrEmpty);
            harvest ??= new SoulHarvest();
            harvest.members ??= new List<SoulHarvestMember>();
            if (!hasHarvest) harvest.members.Clear();
        }
    }
}
